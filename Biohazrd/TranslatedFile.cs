using System;

namespace Biohazrd
{
    /// <summary>A reference to a single C++ header used as input to translation.</summary>
    public sealed class TranslatedFile
    {
        /// <remarks>
        /// This will be the path provided to <see cref="TranslatedLibraryBuilder"/> during library construction.
        ///
        /// In the case of in-memory files, this path will be the automatically-generated name used for Clang.
        /// </remarks>
        public string FilePath { get;  }

        /// <summary>The libclang handle associated with this file.</summary>
        /// <remarks>
        /// May be <see cref="IntPtr.Zero"/> if this file was not found while processing the translation unit.
        ///
        /// This handle uniquely identifies this file within a libclang translation unit.
        /// Internally, it is the <see cref="ClangSharp.Interop.CXFile.Handle"/> associated with the file.
        /// </remarks>
        internal IntPtr Handle { get; }

        internal TranslatedFile(string filePath, IntPtr handle)
        {
            FilePath = filePath;
            Handle = handle;
        }
    }
}
