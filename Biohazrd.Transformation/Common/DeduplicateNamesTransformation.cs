using Biohazrd.Transformation.Infrastructure;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Biohazrd.Transformation.Common
{
    public sealed class DeduplicateNamesTransformation : TransformationBase
    {
        private readonly ConcurrentDictionary<TranslatedDeclaration, string> DeduplicatedNames = new(ReferenceEqualityComparer.Instance);
        private TranslatedLibrary? OriginalInputLibrary;
        private TranslatedLibrary? DeduplicatedReferencesLibrary;

        private void LogForDeduplicate(TranslatedDeclaration declaration, int id)
        {
            // The exception below should never be thrown because we pre-process with DeduplicateReferencesTransformation
            if (!DeduplicatedNames.TryAdd(declaration, $"{declaration.Name}_{id}"))
            { throw new ArgumentException($"The {declaration.GetType().FullName} '{declaration}' was encountered more than once!", nameof(declaration)); }
        }

        private void CheckForDuplicates<TKey>(IEnumerable<TranslatedDeclaration> parent, Func<TranslatedDeclaration, TKey> keySelector)
            where TKey : notnull
        {
            Dictionary<TKey, TranslatedDeclaration> foundNames = new();
            Dictionary<TKey, int> nextId = new();
            foreach (TranslatedDeclaration declaration in parent)
            {
                TKey key = keySelector(declaration);

                // If there's a key conflict we found a duplicate
                if (!foundNames.TryAdd(key, declaration))
                {
                    int id;

                    // If there is no "next" id, this is the first duplicate found so we need to mark the original to be de-duplicated too
                    // (In theory we could leave the original alone, but this makes it more obvious declarations are part of the same de-duplicated group.)
                    // (This also allows us to properly rename non-functions which conflict with functions.)
                    if (!nextId.TryGetValue(key, out id))
                    {
                        LogForDeduplicate(foundNames[key], 0);
                        id = 1;
                    }

                    LogForDeduplicate(declaration, id);
                    nextId[key] = id + 1;
                }
            }
        }

        protected override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
        {
            Debug.Assert(DeduplicatedNames.Count == 0);
            Debug.Assert(OriginalInputLibrary is null);
            Debug.Assert(DeduplicatedReferencesLibrary is null);
            DeduplicatedNames.Clear();

            OriginalInputLibrary = library;

            // Preprocess the library to deduplicate all declarations referenced more than once
            // This is required to avoid some nasty edge cases (such as the DuplicateParentWithDuplicateChildTest1 test.)
            library = new DeduplicateReferencesTransformation().Transform(library);
            DeduplicatedReferencesLibrary = library;

            // Check for duplicates at the library level
            // (At the library level, declarations can be disambiguated by namespace)
            CheckForDuplicates(library, d => (d.Namespace, d.Name));

            return library;
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            // Edge case: If the library contained duplicate references but no duplicate names we can end up causing it to appear to have been modified even though it effectively wasn't
            // It's not a huge deal that we do this, but we avoid the unecessary "change"
            if (ReferenceEquals(library, DeduplicatedReferencesLibrary))
            {
                Debug.Assert(OriginalInputLibrary is not null);
                library = OriginalInputLibrary;
            }

            DeduplicatedNames.Clear();
            OriginalInputLibrary = null;
            DeduplicatedReferencesLibrary = null;

            return library;
        }

        protected override TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
        {
            // Check if any children of this transformation will need to be de-duplicated
            if (declaration.Any())
            { CheckForDuplicates(declaration, d => d.Name); }

            // Don't rename functions since overloading allows duplicates
            if (declaration is TranslatedFunction)
            { return declaration; }

            // Deduplicate this declaration if needed
            if (DeduplicatedNames.TryGetValue(declaration, out string? newName))
            {
                declaration = declaration with
                {
                    Name = newName,
                    Diagnostics = declaration.Diagnostics.Add(Severity.Warning, $"Renamed duplicate {declaration.GetType()} declaration '{declaration.Name}' -> '{newName}'")
                };
            }

            return declaration;
        }
    }
}
