using Biohazrd.Transformation.Common;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace Biohazrd.Utilities
{
    public sealed class DiagnosticWriter
    {
        private List<DiagnosticCategory> Categories = new();

        public static string NeverSkip => String.Empty;

        public void AddCategory(string categoryName, IEnumerable<DiagnosticOrSubcategory> diagnostics, string? skipMessage = null)
            => Categories.Add(new DiagnosticCategory(categoryName, diagnostics, skipMessage));

        public void AddCategory(string categoryName, IEnumerable<TranslationDiagnostic> diagnostics, string? skipMessage = null)
            => Categories.Add(new DiagnosticCategory(categoryName, diagnostics, skipMessage));

        public void AddFrom(TranslatedLibrary library, string? skipMessage = null)
        {
            AddCategory("Parsing Diagnostics", library.ParsingDiagnostics, skipMessage);

            ImmutableArray<DiagnosticOrSubcategory>.Builder translationDiagnostics = ImmutableArray.CreateBuilder<DiagnosticOrSubcategory>();
            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration.Diagnostics.Length > 0)
                {
                    translationDiagnostics.Add($"{declaration.GetType().Name} {declaration.Name}");

                    foreach (TranslationDiagnostic diagnostic in declaration.Diagnostics)
                    { translationDiagnostics.Add(diagnostic); }
                }
            }

            AddCategory("Translation Diagnostics", translationDiagnostics.MoveToImmutableSafe(), skipMessage);
        }

        public void AddFrom(BrokenDeclarationExtractor brokenDeclarationExtractor, string? skipMessage = null)
        {
            ImmutableArray<DiagnosticOrSubcategory>.Builder diagnostics = ImmutableArray.CreateBuilder<DiagnosticOrSubcategory>();

            foreach (TranslatedDeclaration declaration in brokenDeclarationExtractor.BrokenDeclarations)
            {
                diagnostics.Add($"{declaration.GetType().Name} {declaration.Name}");

                foreach (TranslationDiagnostic diagnostic in declaration.Diagnostics)
                { diagnostics.Add(diagnostic); }
            }

            AddCategory("Broken Declarations", diagnostics.MoveToImmutableSafe(), skipMessage);
        }

        public struct DiagnosticOrSubcategory
        {
            public bool IsSubcategory => _SubcategoryName is not null;
            private string? _SubcategoryName;
            private TranslationDiagnostic _Diagnostic;

            private DiagnosticOrSubcategory(string subcategoryName)
            {
                _SubcategoryName = subcategoryName;
                _Diagnostic = default;
            }

            private DiagnosticOrSubcategory(TranslationDiagnostic diagnostic)
            {
                _SubcategoryName = null;
                _Diagnostic = diagnostic;
            }

            public string SubcategoryName => _SubcategoryName ?? throw new InvalidOperationException("This diagnostic/subcategory is not a subcategory.");
            public TranslationDiagnostic Diagnostic => !IsSubcategory ? _Diagnostic : throw new InvalidOperationException("This diagnostic/subcategory is not a diagnostic.");

            public static implicit operator DiagnosticOrSubcategory(string subcategory)
                => new DiagnosticOrSubcategory(subcategory);

            public static implicit operator DiagnosticOrSubcategory(TranslationDiagnostic diagnostic)
                => new DiagnosticOrSubcategory(diagnostic);
        }

        public void WriteOutDiagnostics(TextWriter writer, bool writeToConsole)
        {
            bool firstCategory = true;
            bool categoryHeaderWritten;
            const string headerDivider = "==============================================================================";

            void WriteCategory(string categoryName)
            {
                if (firstCategory)
                { firstCategory = false; }
                else
                {
                    writer.WriteLine();

                    if (writeToConsole)
                    { Console.WriteLine(); }
                }

                writer.WriteLine(headerDivider);
                writer.WriteLine(categoryName);
                writer.WriteLine(headerDivider);

                if (writeToConsole)
                {
                    Console.WriteLine(headerDivider);
                    Console.WriteLine(categoryName);
                    Console.WriteLine(headerDivider);
                }

                categoryHeaderWritten = true;
            }

            void WriteSubcategory(string subcategoryName)
            {
                int paddingLength = headerDivider.Length - subcategoryName.Length - 2;
                int leftPad = paddingLength / 2;
                int rightPad = (paddingLength + 1) / 2;

                const int minPad = 7;
                if (leftPad < minPad)
                {
                    leftPad = minPad;
                    rightPad = minPad;
                }

                string leftPadString = new String('-', leftPad);
                string rightPadString = leftPad == rightPad ? leftPadString : new String('-', rightPad);
                string line = $"{leftPadString} {subcategoryName} {rightPadString}";

                writer.WriteLine(line);

                if (writeToConsole)
                {
                    Console.WriteLine(line);
                }
            }

            static void WriteDiagnosticToWriter(TextWriter output, in TranslationDiagnostic diagnostic)
            {
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

            static void WriteDiagnosticToConsole(in TranslationDiagnostic diagnostic)
            {
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

                    WriteDiagnosticToWriter(output, diagnostic);
                }
                finally
                {
                    Console.BackgroundColor = oldBackgroundColor;
                    Console.ForegroundColor = oldForegroundColor;
                }
            }

            void WriteDiagnostic(in TranslationDiagnostic diagnostic)
            {
                WriteDiagnosticToWriter(writer, diagnostic);

                if (writeToConsole)
                { WriteDiagnosticToConsole(diagnostic); }
            }

            foreach (DiagnosticCategory category in Categories)
            {
                categoryHeaderWritten = false;

                if (!category.SkipIfEmpty)
                { WriteCategory(category.Name); }

                bool hadOutput = false;
                if (category.Diagnostics is IEnumerable<DiagnosticOrSubcategory> diagnosticsOrSubcategories)
                {
                    foreach (DiagnosticOrSubcategory diagnosticOrSubcategory in diagnosticsOrSubcategories)
                    {
                        hadOutput = true;

                        if (!categoryHeaderWritten)
                        { WriteCategory(category.Name); }

                        if (diagnosticOrSubcategory.IsSubcategory)
                        { WriteSubcategory(diagnosticOrSubcategory.SubcategoryName); }
                        else
                        { WriteDiagnostic(diagnosticOrSubcategory.Diagnostic); }
                    }
                }
                else if (category.Diagnostics is IEnumerable<TranslationDiagnostic> diagnostics)
                {
                    foreach (TranslationDiagnostic diagnostic in diagnostics)
                    {
                        hadOutput = true;

                        if (!categoryHeaderWritten)
                        { WriteCategory(category.Name); }

                        WriteDiagnostic(diagnostic);
                    }
                }

                if (!hadOutput && category.SkipMessage is not null)
                {
                    writer.WriteLine(category.SkipMessage);

                    if (writeToConsole)
                    { Console.WriteLine(category.SkipMessage); }
                }
            }
        }

        private struct DiagnosticCategory
        {
            public string Name { get; }
            public IEnumerable Diagnostics { get; }
            public bool SkipIfEmpty => SkipMessage is null;
            public string? SkipMessage { get; }

            private DiagnosticCategory(string categoryName, IEnumerable diagnostics, string? skipMessage)
            {
                Name = categoryName;
                Diagnostics = diagnostics;
                SkipMessage = skipMessage;
            }

            public DiagnosticCategory(string categoryName, IEnumerable<TranslationDiagnostic> diagnostics, string? skipMessage)
                : this(categoryName, (IEnumerable)diagnostics, skipMessage)
            { }

            public DiagnosticCategory(string categoryName, IEnumerable<DiagnosticOrSubcategory> diagnostics, string? skipMessage)
                : this(categoryName, (IEnumerable)diagnostics, skipMessage)
            { }
        }
    }
}
