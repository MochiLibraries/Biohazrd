using Biohazrd.Transformation.Infrastructure;

namespace Biohazrd.Transformation
{
    public abstract partial class TransformationBase
    {
        public TranslatedLibrary Transform(TranslatedLibrary library)
        {
            TransformationContext context = new(library);
            using ListTransformHelper newDeclarations = new(library.Declarations);

            // Recursively transform each declaration in the library
            foreach (TranslatedDeclaration declaration in library.Declarations)
            {
                TransformationResult transformedDeclaration = TransformRecursively(context, declaration);
                newDeclarations.Add(transformedDeclaration);
            }

            // If the declarations list mutated, create a new library
            if (newDeclarations.WasChanged)
            {
                library = library with
                {
                    Declarations = newDeclarations.ToImmutable()
                };
            }

            return library;
        }

        private TransformationResult TransformRecursively(TransformationContext context, TranslatedDeclaration declaration)
        {
            // Transform this declaration
            TransformationResult result = Transform(context, declaration);

            // Recurse into children of applicable declarations
            // Note that we do this using the results of the previous transformation because the children of the old declaration might not necessarily exist on the new one(s)
            // Additionally, even if they did still exist, we'd have trouble correlating the children between the two declarations.
            switch (result.Count)
            {
                case 1:
                    result = TransformChildren(context, declaration);
                    break;
                case > 1:
                {
                    using ListTransformHelper newResults = new(result.Declarations);

                    foreach (TranslatedDeclaration resultDeclaration in result.Declarations)
                    {
                        TransformationResult subResult = TransformChildren(context, resultDeclaration);
                        newResults.Add(subResult);
                    }

                    if (newResults.WasChanged)
                    { result = new TransformationResult(newResults.ToImmutable()); }
                }
                break;
            }

            return result;
        }

        protected virtual TransformationResult Transform(TransformationContext context, TranslatedDeclaration declaration)
            => declaration switch
            {
                // Fields
                TranslatedVTableField vTableFieldDeclaration => TransformVTableField(context, vTableFieldDeclaration),
                TranslatedUnimplementedField unimplementedFieldDeclaration => TransformUnimplementedField(context, unimplementedFieldDeclaration),
                TranslatedNormalField normalFieldDeclaration => TransformNormalField(context, normalFieldDeclaration),
                TranslatedBaseField baseFieldDeclaration => TransformBaseField(context, baseFieldDeclaration),
                TranslatedField fieldDeclaration => TransformField(context, fieldDeclaration),
                // Sealed children of TranslatedDeclaration
                TranslatedVTable vTableDeclaration => TransformVTable(context, vTableDeclaration),
                TranslatedUnsupportedDeclaration unsupportedDeclarationDeclaration => TransformUnsupportedDeclaration(context, unsupportedDeclarationDeclaration),
                TranslatedUndefinedRecord undefinedRecordDeclaration => TransformUndefinedRecord(context, undefinedRecordDeclaration),
                TranslatedTypedef typedefDeclaration => TransformTypedef(context, typedefDeclaration),
                TranslatedStaticField staticFieldDeclaration => TransformStaticField(context, staticFieldDeclaration),
                TranslatedRecord recordDeclaration => TransformRecord(context, recordDeclaration),
                TranslatedParameter parameterDeclaration => TransformParameter(context, parameterDeclaration),
                TranslatedFunction functionDeclaration => TransformFunction(context, functionDeclaration),
                TranslatedEnumConstant enumConstantDeclaration => TransformEnumConstant(context, enumConstantDeclaration),
                TranslatedEnum enumDeclaration => TransformEnum(context, enumDeclaration),
                // Fallback declaration
                TranslatedDeclaration => TransformUnknownDeclarationType(context, declaration)
            };

        protected virtual TransformationResult TransformChildren(TransformationContext context, TranslatedDeclaration declaration)
            => declaration switch
            {
                TranslatedRecord recordDeclaration => TransformRecordChildren(context.Add(declaration), recordDeclaration),
                TranslatedFunction functionDeclaration => TransformFunctionChildren(context.Add(declaration), functionDeclaration),
                TranslatedEnum enumDeclaration => TransformEnumChildren(context.Add(declaration), enumDeclaration),
                // In the default case, the declaration has no children:
                TranslatedDeclaration => declaration
            };

        protected virtual TransformationResult TransformUnknownDeclarationType(TransformationContext context, TranslatedDeclaration declaration)
            => TransformDeclaration(context, declaration);
    }
}
