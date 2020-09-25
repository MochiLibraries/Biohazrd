using Biohazrd.OutputGeneration;
using ClangSharp.Pathogen;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp
{
    public sealed partial class CSharpLibraryGenerator : CSharpDeclarationVisitor
    {
        private readonly TranslatedFile? FileFilter;
        private readonly TranslatedDeclaration? DeclarationFilter;
        private readonly CSharpCodeWriter Writer;
        private readonly CSharpGenerationOptions Options;

        private readonly List<TranslationDiagnostic> Diagnostics = new();

        private CSharpLibraryGenerator(CSharpGenerationOptions options, OutputSession session, string filePath)
        {
            Options = options;
            Writer = session.Open<CSharpCodeWriter>(filePath);
        }

        private CSharpLibraryGenerator(CSharpGenerationOptions options, OutputSession session, string filePath, TranslatedFile filter)
            : this(options, session, filePath)
            => FileFilter = filter;

        private CSharpLibraryGenerator(CSharpGenerationOptions options, OutputSession session, string filePath, TranslatedDeclaration filter)
            : this(options, session, filePath)
            => DeclarationFilter = filter;

        public static ImmutableArray<TranslationDiagnostic> Generate(CSharpGenerationOptions options, OutputSession session, TranslatedLibrary library, LibraryTranslationMode mode)
        {
            ImmutableArray<TranslationDiagnostic>.Builder diagnosticsBuilder = ImmutableArray.CreateBuilder<TranslationDiagnostic>();

            void DoGenerate(CSharpLibraryGenerator generator)
            {
                generator.Visit(library);
                generator.Writer.Finish();
                diagnosticsBuilder.AddRange(generator.Diagnostics);
            }

            switch (mode)
            {
                case LibraryTranslationMode.OneFilePerType:
                {
                    FindAllNonNestedTypeDeclarationsVisitor allNonNestedTypes = new();
                    allNonNestedTypes.Visit(library);

                    foreach (TranslatedDeclaration nonNestedType in allNonNestedTypes.NonNestedTypes)
                    { DoGenerate(new CSharpLibraryGenerator(options, session, $"{nonNestedType.Name}.cs", filter: nonNestedType)); }
                }
                break;
                case LibraryTranslationMode.OneFilePerInputFile:
                {
                    // Enumerate all of the files which have at least one declaration
                    HashSet<TranslatedFile> filesWithDeclarations = new();
                    foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
                    { filesWithDeclarations.Add(declaration.File); }

                    // Generate an output file for every input file that has declarations
                    foreach (TranslatedFile file in filesWithDeclarations)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file.FilePath) + ".cs";

                        if (file == TranslatedFile.Synthesized)
                        { fileName = "SynthesizedDeclarations.cs"; }

                        DoGenerate(new CSharpLibraryGenerator(options, session, fileName, filter: file));
                    }
                }
                break;
                case LibraryTranslationMode.OneFile:
                {
                    DoGenerate(new CSharpLibraryGenerator(options, session, "TranslatedLibrary.cs"));
                }
                break;
                default:
                    throw new ArgumentException("The specified mode is invalid.", nameof(mode));
            }

            return diagnosticsBuilder.MoveToImmutableSafe();
        }

        protected override void Visit(VisitorContext context, TranslatedDeclaration declaration)
        {
            if (context.Parents.Length == 0)
            {
                if (DeclarationFilter is not null && !ReferenceEquals(declaration, DeclarationFilter))
                { return; }
                else if (FileFilter is not null && declaration.File != FileFilter)
                { return; }
            }

            // Dump Clang information
            if (Options.DumpClangInfo && declaration.Declaration is not null)
            {
                Writer.EnsureSeparation();
                Writer.WriteLineLeftAdjusted($"#region {declaration.Declaration.CursorKindDetailed()} {declaration.Name} Dump");

                using (Writer.Prefix("// "))
                { ClangSharpInfoDumper.Dump(Writer, declaration.Declaration, Options.DumpOptions); }

                Writer.WriteLineLeftAdjusted("#endregion");
                Writer.NoSeparationNeededBeforeNextLine();
            }

            base.Visit(context, declaration);
        }

        protected override void VisitDeclaration(VisitorContext context, TranslatedDeclaration declaration)
        {
            string errorMessage = $"Unsupported {declaration.GetType().Name} declaration {declaration.Name}!";
            Diagnostics.Add(Severity.Error, errorMessage);

            // If this declaration has children, visit them in a disabled scope.
            if (declaration.GetEnumerator().MoveNext())
            {
                using (Writer.DisableScope(errorMessage))
                { base.VisitDeclaration(context, declaration); }
            }
        }

        protected override void VisitTypedef(VisitorContext context, TranslatedTypedef declaration)
        {
            Fatal(context, declaration, "Typedefs cannot be directly represented in C#.");
            Writer.Write("// ");

            if (context.ParentDeclaration is not null)
            { Writer.Write($"{declaration.Accessibility.ToCSharpKeyword()} "); }

            Writer.WriteLine($"typedef '{declaration.UnderlyingType}' '{declaration.Name}'");
        }

        protected override void VisitUndefinedRecord(VisitorContext context, TranslatedUndefinedRecord declaration)
        {
            Writer.EnsureSeparation();
            Writer.WriteLine("/// <remarks>This type was forward-declared but never defined. Do not dereference.</remarks>");
            Writer.WriteLine($"{declaration.Accessibility.ToCSharpKeyword()} ref partial struct {SanitizeIdentifier(declaration.Name)}");
            using (Writer.Block())
            {
            }
        }

        protected override void VisitUnsupportedDeclaration(VisitorContext context, TranslatedUnsupportedDeclaration declaration)
            => Fatal(context, declaration, $"{declaration.Declaration.CursorKindDetailed()} Clang declarations are not supported.");

        protected override void VisitUnknownDeclarationType(VisitorContext context, TranslatedDeclaration declaration)
            => Fatal(context, declaration, $"Declarations of this type are not supported by the C# output generator.");

        private void Fatal(VisitorContext context, TranslatedDeclaration declaration, string? reason, string? extraDescription)
        {
            Writer.Write($"/* Failed top emit {declaration.GetType().Name} {declaration.Name}");

            if (extraDescription is not null)
            { Writer.Write($" {extraDescription}"); }

            if (reason is not null)
            { Writer.Write($": {reason}"); }

            Writer.WriteLine(" */");

            reason ??= "Unknown error.";
            Diagnostics.Add(Severity.Error, $"Could not emit {declaration.GetType().Name} {declaration.Name} @ {context}: {reason}");
        }

        private void Fatal(VisitorContext context, TranslatedDeclaration declaration, string? reason)
            => Fatal(context, declaration, reason, null);

        private void FatalContext(VisitorContext context, TranslatedDeclaration declaration, string? extraDescription)
            => Fatal(context, declaration, $"Declarations of this type not expected in this context.", extraDescription);

        private void FatalContext(VisitorContext context, TranslatedDeclaration declaration)
            => FatalContext(context, declaration, null);
    }
}
