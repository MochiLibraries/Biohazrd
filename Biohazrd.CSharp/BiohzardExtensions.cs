using Biohazrd.Transformation;
using System;
using System.Collections.Generic;

namespace Biohazrd.CSharp
{
    public static class BiohzardExtensions
    {
        public static string ToCSharpKeyword(this AccessModifier modifier)
            => modifier switch
            {
                AccessModifier.Private => "private",
                AccessModifier.Protected => "protected",
                AccessModifier.Internal => "internal",
                AccessModifier.ProtectedOrInternal => "protected internal",
                AccessModifier.ProtectedAndInternal => "private protected",
                AccessModifier.Public => "public",
                _ => throw new ArgumentException("Invalid access modifier specified.", nameof(modifier))
            };

        /// <remarks>
        /// This method helps avoid CS1527: Elements defined in a namespace cannot be explicitly declared as private, protected, protected internal, or private protected.
        /// 
        /// It also applies to elements defined at file scope.
        /// </remarks>
        public static bool IsAllowedInNamespaceScope(this AccessModifier modifier)
            => modifier == AccessModifier.Internal || modifier == AccessModifier.Public;

        private static bool IsValidFieldOrMethodParent(this IEnumerable<TranslatedDeclaration> declaration)
            => declaration is TranslatedRecord or SynthesizedLooseDeclarationsType;

        public static bool IsValidFieldOrMethodContext(this TransformationContext context)
            => context.Parent.IsValidFieldOrMethodParent();

        public static bool IsValidFieldOrMethodContext(this VisitorContext context)
            => context.Parent.IsValidFieldOrMethodParent();

        // On the fence about this being built-in, so it's an extension method for now
        internal static TDeclaration WithError<TDeclaration>(this TDeclaration declaration, string errorMessage)
            where TDeclaration : TranslatedDeclaration
            => declaration with
            {
                Diagnostics = declaration.Diagnostics.Add(Severity.Error, errorMessage)
            };

        internal static TDeclaration WithWarning<TDeclaration>(this TDeclaration declaration, string warningMessage)
            where TDeclaration : TranslatedDeclaration
            => declaration with
            {
                Diagnostics = declaration.Diagnostics.Add(Severity.Warning, warningMessage)
            };
    }
}
