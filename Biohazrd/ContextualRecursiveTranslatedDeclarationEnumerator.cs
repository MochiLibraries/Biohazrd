using System.Collections.Generic;

namespace Biohazrd
{
    /// <summary>An enumerator that enumerates a <see cref="IEnumerator{TranslatedDeclaration}"/> recursively with context.</summary>
    /// <remarks>This enumerator is used via <see cref="TranslatedDeclarationEnumerableExtensions.EnumerateRecursivelyWithContext"/>.</remarks>
    public ref struct ContextualRecursiveTranslatedDeclarationEnumerator
    {
        private TinyStack<IEnumerator<TranslatedDeclaration>> EnumeratorStack;
        private TinyStack<VisitorContext> ContextStack;

        public (VisitorContext Context, TranslatedDeclaration Declaration) Current { get; private set; }

        internal ContextualRecursiveTranslatedDeclarationEnumerator(IEnumerator<TranslatedDeclaration> rootEnumerator, VisitorContext rootContext)
        {
            EnumeratorStack = new TinyStack<IEnumerator<TranslatedDeclaration>>();
            EnumeratorStack.Push(rootEnumerator);
            ContextStack = new TinyStack<VisitorContext>();
            ContextStack.Push(rootContext);
            Current = default!;
        }

        public bool MoveNext()
        {
            while (EnumeratorStack.Count > 0)
            {
                IEnumerator<TranslatedDeclaration> enumerator = EnumeratorStack.Peek();

                if (enumerator.MoveNext())
                {
                    // Enumerator had another item, make it our current
                    VisitorContext context = ContextStack.Peek();
                    TranslatedDeclaration declaration = enumerator.Current;
                    Current = (context, declaration);

                    // If the new item is non-empty, push it and its context to the top of the enumerator stack
                    // (Most TranslatedDeclaration implementations are empty so we generally want to skip them.)
                    IEnumerator<TranslatedDeclaration> childEnumerator = declaration.GetEnumerator();
                    if (childEnumerator is not EmptyEnumerator<TranslatedDeclaration>)
                    {
                        EnumeratorStack.Push(childEnumerator);
                        ContextStack.Push(context.Add(declaration));
                    }

                    return true;
                }

                // The enumerator on the top of the stack was depleted, pop it and move to the next one
                EnumeratorStack.Pop();
                ContextStack.Pop();
            }

            // If we got this far, there are no more declarations to enumerate
            Current = default!;
            return false;
        }
    }
}
