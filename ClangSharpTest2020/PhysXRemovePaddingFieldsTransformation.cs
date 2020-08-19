using Biohazrd;
using ClangSharp;

namespace ClangSharpTest2020
{
    public sealed class PhysXRemovePaddingFieldsTransformation : TranslationTransformation
    {
        private readonly TranslatedField TargetField;

        private PhysXRemovePaddingFieldsTransformation(TranslatedField targetField)
            => TargetField = targetField;

        public override void Apply()
            => TargetField.Parent = null;

        public override string ToString()
            => $"PhysX padding field {TargetField}";

        public sealed class Factory : TranslationTransformationFactory
        {
            protected override TranslationTransformation Create(TranslatedDeclaration declaration)
            {
                // Look for fields
                if (!(declaration is TranslatedField field) || !(field.Declaration is FieldDecl clangField))
                { return null; }

                // Check if the field's type is PxPadding
                if (!(clangField.Type is TemplateSpecializationType templateSpecialization))
                { return null; }

                // Get the declaration
                if (!(FindCursor(templateSpecialization.Handle.Declaration) is ClassTemplateSpecializationDecl templateSpecializationDeclaration))
                { return null; }

                // Make sure this declaration is for PxPadding
                if (templateSpecializationDeclaration.Name != "PxPadding")
                { return null; }

                // Create the transformation
                return new PhysXRemovePaddingFieldsTransformation(field);
            }
        }
    }
}
