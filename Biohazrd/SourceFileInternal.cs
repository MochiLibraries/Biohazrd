using ClangSharp.Interop;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Biohazrd
{
    internal sealed class SourceFileInternal
    {
        // It's absolutely critical that this buffer is stored even though it isn't referenced after construction
        // because we save pointers from it and it'd be bad if it got garbage collected.
        private byte[]? UnsavedFileBuffer { get; }
        public bool HasUnsavedFile => UnsavedFileBuffer is not null;
        private CXUnsavedFile _UnsavedFile;
        internal CXUnsavedFile UnsavedFile
        {
            get
            {
                if (!HasUnsavedFile)
                { throw new InvalidOperationException("This source file does not represent an unsaved file."); }

                return _UnsavedFile;
            }
        }

        public string FilePath { get; }
        public bool IsInScope { get; }
        public bool IndexDirectly { get; }

        internal SourceFileInternal(SourceFile sourceFile)
        {
            FilePath = sourceFile.FilePath;
            IsInScope = sourceFile.IsInScope;
            IndexDirectly = sourceFile.IndexDirectly;

            if (sourceFile.Contents is not null)
            {
                Encoding encoding = Encoding.UTF8;
                int filePathByteCount = encoding.GetByteCount(FilePath);
                int contentsByteCount = encoding.GetByteCount(sourceFile.Contents);
                int bufferSize = filePathByteCount + 1 + contentsByteCount; // +1 is the null terminator

                UnsavedFileBuffer = GC.AllocateUninitializedArray<byte>(bufferSize, pinned: true);

                int bytesWritten = encoding.GetBytes(FilePath.AsSpan(), UnsavedFileBuffer.AsSpan().Slice(0, filePathByteCount));
                Debug.Assert(bytesWritten == filePathByteCount, "It's expected the encoder fills the expected portion of the buffer.");
                UnsavedFileBuffer[filePathByteCount] = 0; // Null terminator
                bytesWritten = encoding.GetBytes(sourceFile.Contents.AsSpan(), UnsavedFileBuffer.AsSpan().Slice(filePathByteCount + 1, contentsByteCount));
                Debug.Assert(bytesWritten == contentsByteCount, "It's expected the encoder fills the expected portion of the buffer.");

                unsafe
                {
                    sbyte* bufferPointer = (sbyte*)Unsafe.AsPointer(ref UnsavedFileBuffer[0]);
                    _UnsavedFile = new CXUnsavedFile()
                    {
                        Filename = bufferPointer,
                        Contents = bufferPointer + filePathByteCount + 1,
                        Length = (nuint)contentsByteCount
                    };
                }
            }
        }
    }
}
