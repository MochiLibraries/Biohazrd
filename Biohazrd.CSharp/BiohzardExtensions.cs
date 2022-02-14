using Biohazrd.CSharp.Infrastructure;
using Biohazrd.CSharp.Metadata;
using Biohazrd.CSharp.Trampolines;
using Biohazrd.Expressions;
using Biohazrd.Transformation;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

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
            => declaration is TranslatedRecord or SynthesizedLooseDeclarationsTypeDeclaration;

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

        public static TypeReference? InferType(this ConstantValue value)
            => value switch
            {
                IntegerConstant integer => integer.SizeBits switch
                {
                    8 => integer.IsSigned ? CSharpBuiltinType.SByte : CSharpBuiltinType.Byte,
                    16 => integer.IsSigned ? CSharpBuiltinType.Short : CSharpBuiltinType.UShort,
                    32 => integer.IsSigned ? CSharpBuiltinType.Int : CSharpBuiltinType.UInt,
                    64 => integer.IsSigned ? CSharpBuiltinType.Long : CSharpBuiltinType.ULong,
                    _ => (TypeReference?)null
                },
                FloatConstant => CSharpBuiltinType.Float,
                DoubleConstant => CSharpBuiltinType.Double,
                StringConstant => CSharpBuiltinType.String,
                NullPointerConstant => CSharpBuiltinType.NativeInt,
                _ => null
            };

        public static Trampoline? TryGetPrimaryTrampoline(this TranslatedFunction function)
            => function.Metadata.TryGet(out TrampolineCollection trampolines) ? trampolines.PrimaryTrampoline : null;

        public static Trampoline GetPrimaryTrampoline(this TranslatedFunction function)
            => function.TryGetPrimaryTrampoline() ?? throw new InvalidOperationException("Tried to get the primary trampoline of a function with no trampoline metadata.");

        public static TranslatedFunction WithSecondaryTrampoline(this TranslatedFunction function, Trampoline secondaryTrampoline)
        {
            if (!function.Metadata.TryGet(out TrampolineCollection trampolines))
            { throw new InvalidOperationException("Cannot add a secondary trampoline to a function which has no trampolines."); }

            trampolines = trampolines.WithTrampoline(secondaryTrampoline);
            return function with
            {
                Metadata = function.Metadata.Set(trampolines)
            };
        }
    }
}
