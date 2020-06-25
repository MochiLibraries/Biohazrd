using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClangSharpTest2020
{
    public sealed class TranslatedLibrary : IDisposable
    {
        private readonly List<TranslatedFile> Files = new List<TranslatedFile>();
        private readonly CXIndex Index;
        private readonly string[] ClangCommandLineArgumentsArray;

        internal ReadOnlySpan<string> ClangCommandLineArguments => ClangCommandLineArgumentsArray;
        
        /// <summary>True if any file in this library contains diagnostics with <see cref="TranslationDiagnostic.IsError"/> of true.</summary>
        public bool HasErrors { get; private set; }

        public TranslatedLibrary(IEnumerable<string> clangCommandLineArguments)
        {
            ClangCommandLineArgumentsArray = clangCommandLineArguments.ToArray();
            Index = CXIndex.Create(displayDiagnostics: true);
        }

        public TranslatedFile AddFile(string filePath)
        {
            TranslatedFile newFile;
            //using (new WorkingDirectoryScope(Path.GetFileNameWithoutExtension(filePath)))
            { newFile = new TranslatedFile(this, Index, filePath); }

            Files.Add(newFile);

            if (newFile.HasErrors)
            {
                Debug.Assert(HasErrors, "The library should already have errors if the new file does as well.");
                HasErrors = true;
            }

            return newFile;
        }

        public void Validate()
        {
            foreach (TranslatedFile file in Files)
            { file.Validate(); }
        }

        public void ApplyTransformation(TranslationTransformation.FactoryDelegate transformationFactory)
        {
            List<TranslationTransformation> transformations = new List<TranslationTransformation>();
            EnumerateTransformations(transformationFactory, transformations);

            foreach (TranslationTransformation transformation in transformations)
            {
                Console.WriteLine($"Applying transformation: {transformation}");
                transformation.Apply();
            }
        }

        private void EnumerateTransformations(TranslationTransformation.FactoryDelegate transformationFactory, List<TranslationTransformation> transformations)
        {
            foreach (TranslatedFile file in Files)
            { EnumerateTransformations(transformationFactory, transformations, file); }
        }

        private void EnumerateTransformations(TranslationTransformation.FactoryDelegate transformationFactory, List<TranslationTransformation> transformations, IDeclarationContainer container)
        {
            foreach (TranslatedDeclaration declaration in container)
            {
                TranslationTransformation transformation = transformationFactory(declaration);

                if (transformation != null)
                { transformations.Add(transformation); }

                if (declaration is IDeclarationContainer nestedContainer)
                { EnumerateTransformations(transformationFactory, transformations, nestedContainer); }
            }
        }

        public void Translate(LibraryTranslationMode mode)
        {
            switch (mode)
            {
                case LibraryTranslationMode.OneFilePerType:
                {
                    foreach (TranslatedFile file in Files)
                    { file.Translate(); }
                }
                break;
                case LibraryTranslationMode.OneFilePerInputFile:
                {
                    foreach (TranslatedFile file in Files)
                    {
                        if (file.IsEmptyTranslation)
                        { continue; }

                        using CodeWriter writer = new CodeWriter();
                        file.Translate(writer);
                        string outputFileName = Path.GetFileNameWithoutExtension(file.FilePath) + ".cs";
                        writer.WriteOut(outputFileName);
                    }
                }
                break;
                case LibraryTranslationMode.OneFile:
                {
                    using CodeWriter writer = new CodeWriter();

                    foreach (TranslatedFile file in Files)
                    { file.Translate(writer); }

                    writer.WriteOut("TranslatedLibrary.cs");
                }
                break;
                default:
                    throw new ArgumentException("The specified mode is invalid.", nameof(mode));
            }
        }

        private readonly UnnamedNamer UnnamedNamer = new UnnamedNamer();
        internal string GetNameForUnnamed(string category)
            => UnnamedNamer.GetName(category);

        internal void Diagnostic(in TranslationDiagnostic diagnostic)
        {
            if (diagnostic.IsError)
            { HasErrors = true; }

            TextWriter output;
            ConsoleColor oldForegroundColor = Console.ForegroundColor;
            ConsoleColor oldBackgroundColor = Console.BackgroundColor;

            try
            {
                switch (diagnostic.Severity)
                {
                    case Severity.Ignored:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        output = Console.Out;
                        break;
                    case Severity.Note:
                        output = Console.Out;
                        break;
                    case Severity.Warning:
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        output = Console.Error;
                        break;
                    case Severity.Error:
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        output = Console.Error;
                        break;
                    case Severity.Fatal:
                    default:
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.BackgroundColor = ConsoleColor.DarkRed;
                        output = Console.Error;
                        break;
                }

                if (!diagnostic.Location.IsNull)
                {
                    string fileName = Path.GetFileName(diagnostic.Location.SourceFile);
                    if (diagnostic.Location.Line != 0)
                    { output.WriteLine($"{diagnostic.Severity} at {fileName}:{diagnostic.Location.Line}: {diagnostic.Message}"); }
                    else
                    { output.WriteLine($"{diagnostic.Severity} at {fileName}: {diagnostic.Message}"); }
                }
                else
                { output.WriteLine($"{diagnostic.Severity}: {diagnostic.Message}"); }
            }
            finally
            {
                Console.BackgroundColor = oldBackgroundColor;
                Console.ForegroundColor = oldForegroundColor;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (TranslatedFile file in Files)
                { file.Dispose(); }
            }

            Index.Dispose();
        }

        ~TranslatedLibrary()
        {
            // Dispose order matters for the unmanaged resources indirectly managed by this class, so it's not ideal to allow it to be garbage collected.
            // As such, we complain if we're allowed to be collected.
            Console.Error.WriteLine("TranslatedLibrary must be disposed of explicitly!");
            Debugger.Break();
            Dispose(false);
        }
    }
}
