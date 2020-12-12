using ClangSharp.Interop;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Biohazrd
{
    /// <summary>Represents a C/C++ source file to be processed by Biohazrd.</summary>
    public sealed record SourceFile
    {
        /// <summary>The path to the file</summary>
        public string FilePath { get; }

        /// <summary>Whether the file is real or in-memory-only</summary>
        /// <remarks>
        /// An in-memory-only file does not actually exist and can only be indexed.
        ///
        /// Due to a Clang limitation, you cannot specify virtual files which can be used in an <c>#include</c> directive unless that directive references the file by an absolute path.
        /// </remarks>
        public bool IsVirtual { get; }

        /// <summary>Whether the file is considered in-scope or not</summary>
        /// <remarks>
        /// If a file is considered in-scope by Biohazrd, it will emit <see cref="TranslatedDeclaration"/>s during the translation phase.
        ///
        /// It generally only makes sense to speicify out-of-scope files if you're going to specify <see cref="IndexDirectly"/> or <see cref="Contents"/>.
        ///
        /// <c>true</c> by default.
        /// </remarks>
        public bool IsInScope { get; init; } = true;

        /// <summary>Whether or not Biohazrd will include this file in the translation library index.</summary>
        /// <remarks>
        /// This should generally be true if <see cref="IsInScope"/> is true.
        ///
        /// Set this to false if this file should only be included indirectly by another source file.
        ///
        /// <c>true</c> by default.
        /// </remarks>
        public bool IndexDirectly { get; init; } = true;

        /// <summary>The contents of the file.</summary>
        /// <remarks>
        /// This property can be used to override the contents of an existing file, or specify a file which doesn't actually exist on disk.
        ///
        /// If this property is <c>null</c>, it is expected that the file exists on disk and is accessible by Clang.
        ///
        /// If this property is specified, <see cref="ContentBytes"/> must be empty.
        /// </remarks>
        public string? Contents { get; init; } = null;

        /// <summary>The contents of the file, encoded as UTF8 bytes.</summary>
        /// <remarks>
        /// This property can be used to override the contents of an existing file, or specify a file which doesn't actually exist on disk.
        ///
        /// If this property is <c>null</c>, it is expected that the file exists on disk and is accessible by Clang.
        ///
        /// If this property is non-empty, <see cref="Contents"/> must be null.
        /// </remarks>
        public ReadOnlyMemory<byte> ContentBytes { get; init; } = ReadOnlyMemory<byte>.Empty;

        /// <summary>True if this file has <see cref="Contents"/> or <see cref="ContentBytes" /></summary>
        public bool HasContents => Contents is not null || !ContentBytes.IsEmpty;

        /// <summary>Creates a new file</summary>
        /// <param name="filePath">The path to the file</param>
        /// <param name="isVirtual">Indicates if the file actually exists or only exists in memory</param>
        /// <remarks>
        /// <paramref name="filePath"/> will be converted to a fully qualified path and normalized.
        /// 
        /// If <paramref name="isVirtual"/> is <c>false</c>, the file will be treated as if it were relative to the current working directory.
        /// 
        /// If <paramref name="isVirtual"/> is <c>true</c>, the file will be treated as if it were in a fake directory with an illegal name.
        /// </remarks>
        public SourceFile(string filePath, bool isVirtual = false)
        {
            FilePath = filePath;
            IsVirtual = isVirtual;

            if (!Path.IsPathFullyQualified(filePath))
            {
                if (IsVirtual)
                {
                    if (OperatingSystem.IsWindows())
                    { FilePath = Path.Combine(@"C:\<>MemoryFiles", FilePath); }
                    else
                    { FilePath = Path.Combine(@"/<>MemoryFiles", FilePath); }
                }
                else
                { FilePath = Path.GetFullPath(FilePath); }
            }
        }

        internal unsafe CXUnsavedFile CreateUnsavedFile(List<MemoryHandle> memoryHandles)
        {
            if (!HasContents)
            { throw new InvalidOperationException("This method can only be called on files with contnets"); }

            if (memoryHandles is null)
            { throw new ArgumentNullException(nameof(memoryHandles)); }

            MemoryHandle filePathHandle = default;
            MemoryHandle contentsHandle = default;
            try
            {
                byte[] filePathBytes = Encoding.UTF8.GetBytesNullTerminated(FilePath);
                filePathHandle = filePathBytes.AsMemory().Pin();

                ReadOnlyMemory<byte> contents = Contents is not null ? Encoding.UTF8.GetBytes(Contents) : ContentBytes;
                contentsHandle = contents.Pin();

                return new CXUnsavedFile()
                {
                    Filename = (sbyte*)filePathHandle.Pointer,
                    Contents = (sbyte*)contentsHandle.Pointer,
                    Length = (nuint)contents.Length
                };
            }
            catch
            {
                // Disposing of a defaulted MemoryHandle is safe
                contentsHandle.Dispose();
                filePathHandle.Dispose();
                throw;
            }
        }
    }
}
