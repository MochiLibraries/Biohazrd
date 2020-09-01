using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Biohazrd.Transformation
{
    public ref struct TransformationResult
    {
        private object? NewDeclarationOrDeclarations;

        public int Count => NewDeclarationOrDeclarations switch
        {
            TranslatedDeclaration => 1,
            ImmutableList<TranslatedDeclaration> declarations => declarations.Count,
            _ => 0,
        };

        public TranslatedDeclaration SingleDeclaration
            => NewDeclarationOrDeclarations as TranslatedDeclaration ??
            throw new InvalidOperationException($"The result contains {(Count == 0 ? "no" : "more than one")} declarations.");

        public ImmutableList<TranslatedDeclaration> Declarations => NewDeclarationOrDeclarations switch
        {
            TranslatedDeclaration declaration => ImmutableList.Create(declaration),
            ImmutableList<TranslatedDeclaration> declarations => declarations,
            _ => ImmutableList<TranslatedDeclaration>.Empty
        };

        public bool IsChange(TranslatedDeclaration originalDeclaration)
            => !ReferenceEquals(NewDeclarationOrDeclarations, originalDeclaration);

        public TransformationResult Add(TranslatedDeclaration newDeclaration)
        {
            switch (NewDeclarationOrDeclarations)
            {
                case TranslatedDeclaration declaration:
                    NewDeclarationOrDeclarations = ImmutableList.Create(declaration, newDeclaration);
                    break;
                case ImmutableList<TranslatedDeclaration> declarations:
                    NewDeclarationOrDeclarations = declarations.Add(newDeclaration);
                    break;
                default:
                    Debug.Assert(NewDeclarationOrDeclarations is null);
                    NewDeclarationOrDeclarations = newDeclaration;
                    break;
            }

            return this;
        }

        public TransformationResult AddRange(ImmutableList<TranslatedDeclaration> newDeclarations)
        {
            if (newDeclarations.Count == 0)
            { return this; }
            else if (newDeclarations.Count == 1)
            { return Add(newDeclarations[0]); }

            switch (NewDeclarationOrDeclarations)
            {
                case TranslatedDeclaration declaration:
                    NewDeclarationOrDeclarations = newDeclarations.Insert(0, declaration);
                    break;
                case ImmutableList<TranslatedDeclaration> declarations:
                    NewDeclarationOrDeclarations = declarations.AddRange(newDeclarations);
                    break;
                default:
                    Debug.Assert(NewDeclarationOrDeclarations is null);
                    NewDeclarationOrDeclarations = newDeclarations;
                    break;
            }

            return this;
        }

        public static implicit operator TransformationResult(TranslatedDeclaration? declaration)
            => new TransformationResult() { NewDeclarationOrDeclarations = declaration };

        public TransformationResult(ImmutableList<TranslatedDeclaration> declarations)
            => NewDeclarationOrDeclarations = declarations.Count switch
            {
                0 => null,
                1 => declarations[0],
                _ => declarations
            };
    }
}
