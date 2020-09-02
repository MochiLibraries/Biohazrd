using System.Collections.Generic;

namespace Biohazrd
{
    /// <summary>An enumerator that enumerates a <see cref="IEnumerator{TranslatedDeclaration}"/> recursively.</summary>
    /// <remarks>This enumerator is typically used via <see cref="TranslatedDeclarationEnumerableExtensions.EnumerateRecursively(IEnumerable{TranslatedDeclaration})"/>.</remarks>
    public ref struct RecursiveTranslatedDeclarationEnumerator
    {
        private TinyStack<IEnumerator<TranslatedDeclaration>> EnumeratorStack;
        public TranslatedDeclaration Current { get; private set; }

        public RecursiveTranslatedDeclarationEnumerator(IEnumerator<TranslatedDeclaration> rootEnumerator)
        {
            EnumeratorStack = new TinyStack<IEnumerator<TranslatedDeclaration>>();
            EnumeratorStack.Push(rootEnumerator);
            Current = null!;
        }

        public bool MoveNext()
        {
            while (EnumeratorStack.Count > 0)
            {
                IEnumerator<TranslatedDeclaration> enumerator = EnumeratorStack.Peek();

                if (enumerator.MoveNext())
                {
                    // Enumerator had another item, make it our current
                    Current = enumerator.Current;

                    // If the new item is non-empty, push it to the top of the neumerator stack
                    // (Most TranslatedDeclaration implementations are empty so we generally want to skip them.)
                    IEnumerator<TranslatedDeclaration> childEnumerator = Current.GetEnumerator();
                    if (childEnumerator is not EmptyEnumerator<TranslatedDeclaration>)
                    { EnumeratorStack.Push(childEnumerator); }

                    return true;
                }

                // The enumerator on the top of the stack was depleted, pop it and move to the next one
                EnumeratorStack.Pop();
            }

            // If we got this far, there are no more declarations to enumerate
            Current = null!;
            return false;
        }
    }
}
