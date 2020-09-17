using Biohazrd;
using Biohazrd.Transformation;
using ClangSharp;
using ClangSharp.Interop;

namespace ClangSharpTest2020
{
    public sealed class PhysXRemovePaddingFieldsTransformation : TransformationBase
    {
        protected override TransformationResult TransformNormalField(TransformationContext context, TranslatedNormalField declaration)
        {
            //TODO: Ideally this should not need to touch Clang stuff so much
            //TODO: Can the new Desugar function added to unreleased versions of ClangSharp help here?
            // Look for fields of type PxPadding and delete them (somewhat involved since the information we need isn't exposed on ClangSharp as cleanly as we'd like.)
            return declaration.Declaration switch
            {
                FieldDecl
                {
                    Type: TemplateSpecializationType
                    {
                        Handle:
                        {
                            Declaration:
                            {
                                IsNull: false,
                                DeclKind: CX_DeclKind.CX_DeclKind_ClassTemplateSpecialization,
                            } fieldTypeDeclaration
                        }
                    }
                } => fieldTypeDeclaration.Spelling.ToString() == "PxPadding" ? null : declaration,
                _ => declaration
            };
        }
    }
}
