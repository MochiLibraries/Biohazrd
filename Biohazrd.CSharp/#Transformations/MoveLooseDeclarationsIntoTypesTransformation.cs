using Biohazrd.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace Biohazrd.CSharp
{
    public sealed class MoveLooseDeclarationsIntoTypesTransformation : TransformationBase
    {
        // containingTypeName => declarations
        private readonly Dictionary<string, List<TranslatedDeclaration>> LooseDeclarationsLookup = new();
        private readonly HashSet<TranslatedDeclaration> AllLooseDeclarations = new(ReferenceEqualityComparer.Instance);
        private readonly RemoveLooseDeclarationsTransformation RemovePass;

        protected override bool SupportsConcurrency => false;

        public MoveLooseDeclarationsIntoTypesTransformation()
            => RemovePass = new RemoveLooseDeclarationsTransformation(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool DeclarationCouldBeLoose(TranslatedDeclaration declaration)
            // Functions and fields must be nested under a type in C#, so they can be loose
            => declaration is TranslatedFunction or TranslatedStaticField or TranslatedField;

        protected override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
        {
            // Ensure our lookup is empty
            Debug.Assert(LooseDeclarationsLookup.Count == 0, "The state of this transformaiton should be empty at this point.");
            LooseDeclarationsLookup.Clear();

            // Enumerate all loose declarations
            foreach ((VisitorContext context, TranslatedDeclaration declaration) in library.EnumerateRecursivelyWithContext())
            {
                // Skip declarations which can't be loose
                if (!DeclarationCouldBeLoose(declaration))
                { continue; }

                // If the context allows functions or fields, the declaration is not loose
                if (context.IsValidFieldOrMethodContext())
                { continue; }

                // Determine the name for the containing type
                string looseDeclarationsTypeName = Path.GetFileNameWithoutExtension(declaration.File.FilePath);

                if (String.IsNullOrEmpty(looseDeclarationsTypeName))
                { looseDeclarationsTypeName = "LooseDeclarations"; }

                // Add the loose declaration to the lookup
                List<TranslatedDeclaration>? declarationsForName;
                if (!LooseDeclarationsLookup.TryGetValue(looseDeclarationsTypeName, out declarationsForName))
                {
                    declarationsForName = new List<TranslatedDeclaration>();
                    LooseDeclarationsLookup.Add(looseDeclarationsTypeName, declarationsForName);
                }

                // If the declaration has the same name as the type it will be contained in, rename it since that's not allowed
                // (We normally rely on C++ not allowing this either, but that doesn't work in this context since we're synthesizing types.)
                TranslatedDeclaration declarationToAdd = declaration;
                if (declaration.Name == looseDeclarationsTypeName)
                {
                    declarationToAdd = declaration with
                    {
                        Name = $"{declaration.Name}__",
                        Diagnostics = declaration.Diagnostics.Add(Severity.Warning, $"{declaration} automatically renamed to avoid collision with containing type.")
                    };
                }

                declarationsForName.Add(declarationToAdd);
                AllLooseDeclarations.Add(declaration); // This is intentionally declaration, since this set is used to remove the old declarations.
            }

            // Remove pass runs first so that removal and associating declarations with existing types don't interact
            // (If we do them in the same pass, the declarations will be removed immediately after adding them.)
            return RemovePass.Transform(library);
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            // Synthesize types to contain any remaining declarations
            if (LooseDeclarationsLookup.Count > 0)
            {
                ImmutableList<TranslatedDeclaration>.Builder synthesizedDeclarations = ImmutableList.CreateBuilder<TranslatedDeclaration>();
                foreach ((string typeName, List<TranslatedDeclaration> declarations) in LooseDeclarationsLookup)
                {
                    synthesizedDeclarations.Add(new SynthesizedLooseDeclarationsType(declarations[0].File)
                    {
                        Name = typeName,
                        Members = declarations.ToImmutableList()
                    });
                }

                // Add the synthesized types to the library
                library = library with
                { Declarations = library.Declarations.AddRange(synthesizedDeclarations) };
            }

            // Wipe out the state and return the modified library
            LooseDeclarationsLookup.Clear();
            AllLooseDeclarations.Clear();
            return library;
        }

        private sealed class RemoveLooseDeclarationsTransformation : TransformationBase
        {
            private readonly MoveLooseDeclarationsIntoTypesTransformation ParentTransformation;

            public RemoveLooseDeclarationsTransformation(MoveLooseDeclarationsIntoTypesTransformation parentTransformation)
                => ParentTransformation = parentTransformation;

            protected override TransformationResult TransformDeclaration(TransformationContext context, TranslatedDeclaration declaration)
            {
                // If this declaration was one of the enumerated loose declarations we remove it
                if (DeclarationCouldBeLoose(declaration) && ParentTransformation.AllLooseDeclarations.Remove(declaration))
                { return null; }
                else
                { return declaration; }
            }
        }

        protected override TransformationResult TransformRecord(TransformationContext context, TranslatedRecord declaration)
        {
            // If this record matches one of the container names for any loose declarations, add them to this record
            if (LooseDeclarationsLookup.Remove(declaration.Name, out List<TranslatedDeclaration>? looseDeclarations))
            {
                declaration = declaration with { Members = declaration.Members.AddRange(looseDeclarations) };
            }

            return declaration;
        }
    }
}
