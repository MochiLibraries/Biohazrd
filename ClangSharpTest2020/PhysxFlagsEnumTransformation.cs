using ClangSharp;
using System.Diagnostics;
using ClangType = ClangSharp.Type;

namespace ClangSharpTest2020
{
    public sealed class PhysxFlagsEnumTransformation : TranslationTransformation
    {
        private readonly TranslatedEnum TargetEnum;
        private readonly TranslatedTypedef FlagsTypedef;
        private readonly UnderlyingEnumType UnderlyingType;

        private PhysxFlagsEnumTransformation(TranslatedEnum targetEnum, TranslatedTypedef flagsTypedef, UnderlyingEnumType underlyingType)
        {
            TargetEnum = targetEnum;
            FlagsTypedef = flagsTypedef;
            UnderlyingType = underlyingType;
        }

        public override void Apply()
        {
            // Update the target enum
            TargetEnum.TranslatedName = FlagsTypedef.TranslatedName;
            TargetEnum.IsFlags = true;
            TargetEnum.UnderlyingType = UnderlyingType;

            // Delete the typedef
            FlagsTypedef.Parent = null;
            TargetEnum.AddSecondaryDeclaration(FlagsTypedef.Declaration);
        }

        public static TranslationTransformation Factory(TranslatedDeclaration declaration)
        {
            //TODO: It should be easier to look up declarations and emit diagnostics.
            TranslatedLibrary library = declaration.Library;
            TranslatedFile file = declaration.File;

            // Look for typedefs
            if (!(declaration is TranslatedTypedef typedef))
            { return null; }

            // Look for typedefs that are template specializations
            if (!(typedef.Typedef.UnderlyingType is TemplateSpecializationType templateSpecialization))
            { return null; }

            // Get the declaration
            if (!(library.FindCursor(templateSpecialization.Handle.Declaration) is ClassTemplateSpecializationDecl templateSpecializationDeclaration))
            {
                Debug.Assert(false, "The declaration for a TemplateSpecializationType is expected to be a ClassTemplateSpecializationDecl.");
                return null;
            }

            // Make sure this declaration is for PxFlags
            if (templateSpecializationDeclaration.Name != "PxFlags")
            { return null; }

            // We expect there to be two template arguments: PxFlags<enumtype, storagetype>
            // (There will always be 2 arguments even if the default value for storagetype is used.)
            if (templateSpecializationDeclaration.TemplateArgs.Count != 2)
            {
                Debug.Assert(false, "PxFlags should always have two template arguments.");
                return null;
            }

            // Extract the arguments
            ClangType enumArgument = templateSpecializationDeclaration.TemplateArgs[0];
            ClangType storageType = templateSpecializationDeclaration.TemplateArgs[1];

            // Make sure the type referred to by enumType is actually an enum
            if (!(enumArgument is EnumType enumType))
            {
                file.Diagnostic(Severity.Warning, typedef.Typedef, "The first argument of PxFlags is expected to be an enum.");
                return null;
            }

            // Get the translation of the enum
            TranslatedDeclaration translatedDeclaration = library.TryFindTranslation(enumType.Decl);
            if (translatedDeclaration is null)
            {
                file.Diagnostic(Severity.Warning, typedef.Typedef, $"Could not find translation of enum '{enumType}'");
                return null;
            }

            if (!(translatedDeclaration is TranslatedEnum translatedEnum))
            {
                file.Diagnostic(Severity.Warning, typedef.Typedef, $"The translation of the enum '{enumType}' is not a {nameof(TranslatedEnum)}");
                return null;
            }

            // Get the translation of the storage type
            UnderlyingEnumType underlyingType = storageType.ToUnderlyingEnumType(typedef.Typedef, file);

            // Create the transformation
            return new PhysxFlagsEnumTransformation(translatedEnum, typedef, underlyingType);
        }
    }
}
