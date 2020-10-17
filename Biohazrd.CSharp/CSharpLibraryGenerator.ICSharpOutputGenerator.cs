using Biohazrd.CSharp.Infrastructure;
using Biohazrd.Expressions;

namespace Biohazrd.CSharp
{
    partial class CSharpLibraryGenerator : ICSharpOutputGenerator
    {
        string ICSharpOutputGenerator.GetTypeAsString(VisitorContext context, TranslatedDeclaration declaration, TypeReference type)
            => GetTypeAsString(context, declaration, type);

        string ICSharpOutputGenerator.GetConstantAsString(VisitorContext context, TranslatedDeclaration declaration, ConstantValue constant, TypeReference targetType)
            => GetConstantAsString(context, declaration, constant, targetType);

        void ICSharpOutputGenerator.AddUsing(string @namespace)
            => Writer.Using(@namespace);
    }
}
