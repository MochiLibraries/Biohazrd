using ClangSharp;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Biohazrd
{
    partial class TranslationUnitParser
    {
        internal abstract class CreateDeclarationsEnumerator : IEnumerator<TranslatedDeclaration>, IEnumerable<TranslatedDeclaration>
        {
            public TranslatedDeclaration Current { get; protected set; }
            object IEnumerator.Current => Current;

            public abstract bool MoveNext();

            void IEnumerator.Reset()
                => throw new NotSupportedException();

            IEnumerator<TranslatedDeclaration> IEnumerable<TranslatedDeclaration>.GetEnumerator()
                => this;

            IEnumerator IEnumerable.GetEnumerator()
                => this;

            void IDisposable.Dispose()
            { }

            private CreateDeclarationsEnumerator()
                => Current = null!;

            private class NoResultsEnumerator : CreateDeclarationsEnumerator
            {
                public override bool MoveNext()
                    => false;
            }

            private class SingleResultEnumerator : CreateDeclarationsEnumerator
            {
                internal TranslatedDeclaration? Declaration { get; private set; }

                public SingleResultEnumerator(TranslatedDeclaration? declaration)
                    => Declaration = declaration;

                public override bool MoveNext()
                {
                    if (Declaration is null)
                    { return false; }
                    else
                    {
                        Current = Declaration;
                        Declaration = null;
                        return true;
                    }
                }
            }

            private class MultipleResultsEnumerator : CreateDeclarationsEnumerator
            {
                private readonly TranslationUnitParser Parser;
                private readonly TranslatedFile File;
                private readonly Cursor ParentCursor;
                private int ChildIndex;
                private CreateDeclarationsEnumerator? NestedEnumerator;

                public MultipleResultsEnumerator(TranslationUnitParser parser, Cursor parentCursor, TranslatedFile file)
                {
                    Parser = parser;
                    File = file;
                    ParentCursor = parentCursor;
                    ChildIndex = 0;
                    NestedEnumerator = null;
                }

                public override bool MoveNext()
                {
                    // Handle having a nested enumerator
                    if (NestedEnumerator is not null)
                    {
                        // Pass values from the nested enumerator
                        if (NestedEnumerator.MoveNext())
                        {
                            Current = NestedEnumerator.Current;
                            return true;
                        }

                        // Once the nested enumerator runs out we don't need it anymore
                        NestedEnumerator = null;
                    }

                    // Create nested declarations for the cursor
                    for (; ChildIndex < ParentCursor.CursorChildren.Count; ChildIndex++)
                    {
                        Cursor childCursor = ParentCursor.CursorChildren[ChildIndex];
                        CreateDeclarationsEnumerator childEnumerator = Parser.CreateDeclarations(childCursor, File);
                        switch (childEnumerator)
                        {
                            case NoResultsEnumerator:
                                // No results, try next child
                                continue;
                            case SingleResultEnumerator singleResult:
                                // Handle typical case of single child
                                Current = singleResult.Declaration!;
                                return true;
                            default:
                                // Handle nested enumerator by using nested enumerator logic
                                NestedEnumerator = childEnumerator;
                                return MoveNext();
                        }
                    }

                    // If we got this far, we ran out of children
                    Current = null!;
                    return false;
                }
            }

            public static readonly CreateDeclarationsEnumerator None = new NoResultsEnumerator();

            public static implicit operator CreateDeclarationsEnumerator(TranslatedDeclaration declaration)
                => new SingleResultEnumerator(declaration);

            public static CreateDeclarationsEnumerator CreateChildDeclarations(TranslationUnitParser parser, Cursor parentCursor, TranslatedFile file)
                => new MultipleResultsEnumerator(parser, parentCursor, file);
        }
    }
}
