using System;

namespace Biohazrd
{
    /// <summary>A type reference which has already been resolved to a specific declaration.</summary>
    /// <remarks>
    /// This type reference will only resolve for the specified library, attempting to resolve it with other libraries will result in <see cref="InvalidOperationException"/>.
    ///
    /// This type reference is intended to simplify scenarios where you're going to consume the type reference immediately and then discard it.
    /// (Such as to reuse existing type formatting infrastructure in a context where you only have the declaration.)
    /// 
    /// You must never attach these references to actual declarations, they will almost certainly fail to resolve.
    /// </remarks>
    public sealed record PreResolvedTypeReference : TranslatedTypeReference
    {
        private readonly VisitorContext Context;
        private readonly TranslatedDeclaration Declaration;

        public PreResolvedTypeReference(VisitorContext context, TranslatedDeclaration declaration)
        {
            Context = context;
            Declaration = declaration;
        }

        public override TranslatedDeclaration? TryResolve(TranslatedLibrary library)
        {
            if (!ReferenceEquals(Context.Library, library))
            { throw new InvalidOperationException("Pre-resolved type references must not be permitted to be used with other libraries."); }

            return Declaration;
        }

        public override TranslatedDeclaration? TryResolve(TranslatedLibrary library, out VisitorContext context)
        {
            if (!ReferenceEquals(Context.Library, library))
            { throw new InvalidOperationException("Pre-resolved type references must not be permitted to be used with other libraries."); }

            context = Context;
            return Declaration;
        }

        public override string ToString()
            => $"`Pre-resolved reference to {Declaration.Name}`";

        internal override bool __HACK__CouldResolveTo(TranslatedDeclaration declaration)
            => ReferenceEquals(declaration, Declaration);
    }
}
