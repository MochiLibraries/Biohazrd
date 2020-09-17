using System.Collections.Generic;
using System.Diagnostics;

namespace Biohazrd.Transformation.Common
{
    public sealed class DeduplicateNamesTransformation : TransformationBase
    {
        private readonly Dictionary<(object Parent, string Name), int> NextSuffixNumber = new();

        protected override bool SupportsConcurrency => false;

        private class DuplicateNamesFinder : DeclarationVisitor
        {
            private readonly DeduplicateNamesTransformation Transformation;

            private void FindDuplicateNames(IEnumerable<TranslatedDeclaration> declarations)
            {
                HashSet<string> FoundNames = new();
                HashSet<string> FoundDuplicates = new();

                foreach (TranslatedDeclaration declaration in declarations)
                {
                    if (!FoundNames.Add(declaration.Name))
                    { FoundDuplicates.Add(declaration.Name); }
                }

                foreach (string duplicateName in FoundDuplicates)
                { Transformation.NextSuffixNumber.Add((declarations, duplicateName), 0); }
            }

            public DuplicateNamesFinder(DeduplicateNamesTransformation transformation, TranslatedLibrary library)
            {
                Transformation = transformation;
                FindDuplicateNames(library);
            }

            protected override void VisitDeclaration(VisitorContext context, TranslatedDeclaration declaration)
            {
                FindDuplicateNames(declaration);
                base.VisitDeclaration(context, declaration);
            }
        }

        protected override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
        {
            Debug.Assert(NextSuffixNumber.Count == 0, "The suffix number dictionary should be empty.");
            NextSuffixNumber.Clear();

            // Find all the the names we will deduplicate
            new DuplicateNamesFinder(this, library).Visit(library);

            return library;
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            NextSuffixNumber.Clear();
            return library;
        }

        protected override TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
        {
            // Don't rename functions since overloading allows duplicates
            if (declaration is TranslatedFunction)
            { return declaration; }

            (object, string) key = (context.Parent, declaration.Name);

            if (NextSuffixNumber.TryGetValue(key, out int suffixNumber))
            {
                NextSuffixNumber[key] = suffixNumber + 1;
                string newName = $"{declaration.Name}_{suffixNumber}";
                return declaration with
                {
                    Name = newName,
                    Diagnostics = declaration.Diagnostics.Add(Severity.Warning, $"Renamed duplicate declaration '{declaration.Name}' -> '{newName}'")
                };
            }

            return declaration;
        }
    }
}
