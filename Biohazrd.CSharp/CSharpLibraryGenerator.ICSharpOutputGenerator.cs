using Biohazrd.CSharp.Infrastructure;

namespace Biohazrd.CSharp
{
    partial class CSharpLibraryGenerator : ICSharpOutputGenerator
    {
        string ICSharpOutputGenerator.GetTypeAsString(VisitorContext context, TranslatedDeclaration declaration, TypeReference type)
            => GetTypeAsString(context, declaration, type);

        void ICSharpOutputGenerator.AddUsing(string @namespace)
            => Writer.Using(@namespace);
    }
}
