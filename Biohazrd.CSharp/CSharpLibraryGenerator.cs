using Biohazrd.CSharp.Infrastructure;
using Biohazrd.OutputGeneration;
using Biohazrd.OutputGeneration.Metadata;
using ClangSharp.Pathogen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp
{
    public sealed partial class CSharpLibraryGenerator : CSharpDeclarationVisitor
    {
        private readonly CSharpCodeWriter Writer;
        private readonly CSharpGenerationOptions Options;

        private readonly List<TranslationDiagnostic> Diagnostics = new();

        private CSharpLibraryGenerator(CSharpGenerationOptions options, OutputSession session, string filePath)
        {
            Options = options;
            Writer = session.Open<CSharpCodeWriter>(filePath);
        }

        public static ImmutableArray<TranslationDiagnostic> Generate(CSharpGenerationOptions options, OutputSession session, TranslatedLibrary library)
        {
            ImmutableArray<TranslationDiagnostic>.Builder diagnosticsBuilder = ImmutableArray.CreateBuilder<TranslationDiagnostic>();

            // path => generator
            Dictionary<string, CSharpLibraryGenerator> generators = new();

            // For each declaration at the root, create a generator
            VisitorContext rootVisitorContext = new(library);
            foreach (TranslatedDeclaration declaration in library.Declarations)
            {
                // Skip declarations with no output
                if (declaration is ICustomCSharpTranslatedDeclaration cSharpDeclaration && !cSharpDeclaration.HasOutput)
                { continue; }

                // Determine the file for the declaration
                string outputFileName;
                if (declaration.Metadata.TryGet(out OutputFileName metadataFileName))
                {
                    outputFileName = metadataFileName.FileName;

                    if (Path.GetExtension(outputFileName) is not ".cs")
                    { outputFileName += ".cs"; }
                }
                else
                { outputFileName = $"{SanitizeIdentifier(declaration.Name)}.cs"; }

                // Get or create the generator for this file
                CSharpLibraryGenerator? generator;
                if (!generators.TryGetValue(outputFileName, out generator))
                {
                    generator = new CSharpLibraryGenerator(options, session, outputFileName);
                    generators.Add(outputFileName, generator);
                }

                // Add this declaration to the generator
                generator.Visit(rootVisitorContext, declaration);
            }

            // Finish all writers and collect diagnostics
            foreach (CSharpLibraryGenerator generator in generators.Values)
            {
                generator.Writer.Finish();
                diagnosticsBuilder.AddRange(generator.Diagnostics);
            }

            return diagnosticsBuilder.MoveToImmutableSafe();
        }

        protected override void Visit(VisitorContext context, TranslatedDeclaration declaration)
        {
            // Skip declarations with no output
            if (declaration is ICustomCSharpTranslatedDeclaration cSharpDeclaration && !cSharpDeclaration.HasOutput)
            {
                Debug.Assert(context.Parents.Length > 0, "This case should've been handled in Generate for root declarations.");
                return;
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

            // Emit the namespace (if at root) and declaration
            string? namespaceName = declaration.Namespace;
            if (context.Parents.Length > 0)
            { namespaceName = null; }

            using (Writer.Namespace(namespaceName))
            {
                if (declaration is ICustomCSharpTranslatedDeclaration customDeclaration)
                { customDeclaration.GenerateOutput(this, context, Writer); }
                else
                { base.Visit(context, declaration); }
            }
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
            Writer.Write($"/* Failed to emit {declaration.GetType().Name} {SanitizeMultiLineComment(declaration.Name)}");

            if (extraDescription is not null)
            { Writer.Write($" {SanitizeMultiLineComment(extraDescription)}"); }

            if (reason is not null)
            { Writer.Write($": {SanitizeMultiLineComment(reason)}"); }

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
