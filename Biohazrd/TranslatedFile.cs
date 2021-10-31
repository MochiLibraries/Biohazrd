using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Biohazrd
{
    /// <summary>A reference to a single C++ header used as input to translation.</summary>
    public sealed class TranslatedFile : IEquatable<TranslatedFile>
    {
        /// <remarks>
        /// This will be the path provided to <see cref="TranslatedLibraryBuilder"/> during library construction.
        ///
        /// In the case of in-memory files, this path will be the automatically-generated name used for Clang.
        /// </remarks>
        public string FilePath { get; }

        /// <summary>The libclang handle associated with this file.</summary>
        /// <remarks>
        /// May be <see cref="IntPtr.Zero"/> if this file was not found while processing the translation unit.
        ///
        /// This handle uniquely identifies this file within a libclang translation unit.
        /// Internally, it is the <see cref="ClangSharp.Interop.CXFile.Handle"/> associated with the file.
        /// </remarks>
        internal IntPtr Handle { get; }

        /// <summary>Whether this file was specified as in-scope to <see cref="TranslatedLibraryBuilder"/>.</summary>
        /// <remarks>Out-of-scope files may appear on declarations in certain situations. (Such as when an in-scope declaration contains a nested include to an out-of-scope file.)</remarks>
        public bool WasInScope { get; }

        /// <summary>True if this file was specified as in-scope to <see cref="TranslatedLibraryBuilder"/> but was not encountered during parsing.</summary>
        /// <remarks>A file being unused does not necessarily indicate it was not included. For instance, file containing only comments will be considered unused.</remarks>
        public bool WasNotUsed => WasInScope && Handle == IntPtr.Zero;

        /// <summary>A dummy translated file which represents synthesized declarations that don't directly correspond to a translated file.</summary>
        public static readonly TranslatedFile Synthesized = new TranslatedFile("<>Synthesized", new IntPtr(-1), false);

        internal TranslatedFile(string filePath, IntPtr handle, bool wasInScope)
        {
            FilePath = filePath;
            Handle = handle;
            WasInScope = wasInScope;
        }

        public static bool operator ==(TranslatedFile? a, TranslatedFile? b)
            => ReferenceEquals(a, b) || (a is not null && a.Equals(b));

        public static bool operator !=(TranslatedFile? a, TranslatedFile? b)
            => !(a == b);

        public override bool Equals(object? obj)
            => obj is TranslatedFile other && Equals(other);

        public bool Equals([AllowNull] TranslatedFile other)
            => other is not null && this.Handle == other.Handle;

        public override int GetHashCode()
            => Handle.GetHashCode();

        public override string ToString()
            => Path.GetFileName(FilePath);
    }
}
