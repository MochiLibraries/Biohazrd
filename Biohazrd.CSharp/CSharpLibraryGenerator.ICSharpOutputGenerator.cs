using Biohazrd.CSharp.Infrastructure;
using Biohazrd.Expressions;
using System;

namespace Biohazrd.CSharp
{
    partial class CSharpLibraryGenerator : ICSharpOutputGenerator, ICSharpOutputGeneratorInternal
    {
        CSharpGenerationOptions ICSharpOutputGenerator.Options => Options;

        string ICSharpOutputGenerator.GetTypeAsString(VisitorContext context, TranslatedDeclaration declaration, TypeReference type)
            => GetTypeAsString(context, declaration, type);

        string ICSharpOutputGenerator.GetConstantAsString(VisitorContext context, TranslatedDeclaration declaration, ConstantValue constant, TypeReference targetType)
            => GetConstantAsString(context, declaration, constant, targetType);

        void ICSharpOutputGenerator.AddUsing(string @namespace)
            => Writer.Using(@namespace);

        void ICSharpOutputGenerator.Visit(VisitorContext context, TranslatedDeclaration declaration)
            => Visit(context, declaration);

        void ICSharpOutputGenerator.AddDiagnostic(TranslationDiagnostic diagnostic)
            => Diagnostics.Add(diagnostic);

        void ICSharpOutputGeneratorInternal.Fatal(VisitorContext context, TranslatedDeclaration declaration, string? reason)
            => Fatal(context, declaration, reason);

        void ICSharpOutputGeneratorInternal.Fatal(VisitorContext context, TranslatedDeclaration declaration, string? reason, string? extraDescription)
            => Fatal(context, declaration, reason, extraDescription);

        // These are used for a dirty hack to work around lack of proper support for late-generated infrastructure types
        private bool __NeedsNativeBoolean;
        private bool __NeedsNativeChar;
        void ICSharpOutputGeneratorInternal.__IndicateInfrastructureTypeDependency(NonBlittableTypeKind kind)
        {
            switch (kind)
            {
                case NonBlittableTypeKind.NativeBoolean:
                    __NeedsNativeBoolean = true;
                    break;
                case NonBlittableTypeKind.NativeChar:
                    __NeedsNativeChar = true;
                    break;
                default:
                    throw new NotSupportedException();
            }
        }
    }
}
