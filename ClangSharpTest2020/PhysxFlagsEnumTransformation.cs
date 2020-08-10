using ClangSharp;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ClangType = ClangSharp.Type;

namespace ClangSharpTest2020
{
    public sealed class PhysxFlagsEnumTransformation : TranslationTransformation
    {
        private readonly TranslatedEnum TargetEnum;
        private readonly TranslatedTypedef FlagsTypedef;
        private readonly UnderlyingEnumType UnderlyingType;
        private readonly List<TranslatedFunction> OperatorOverloads;

        private PhysxFlagsEnumTransformation(TranslatedEnum targetEnum, TranslatedTypedef flagsTypedef, UnderlyingEnumType underlyingType, List<TranslatedFunction> operatorOverloads)
        {
            TargetEnum = targetEnum;
            FlagsTypedef = flagsTypedef;
            UnderlyingType = underlyingType;
            OperatorOverloads = operatorOverloads;
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

            // Delete the PX_FLAGS_OPERATORS operator overloads
            foreach (TranslatedFunction operatorOverload in OperatorOverloads)
            { operatorOverload.Parent = null; }
        }

        public override string ToString()
            => $"PhysX-style flags enum {FlagsTypedef.TranslatedName}";

        public sealed class Factory : TranslationTransformationFactory
        {
            protected override TranslationTransformation Create(TranslatedDeclaration declaration)
            {
                // Look for typedefs
                if (!(declaration is TranslatedTypedef typedef))
                { return null; }

                // Look for typedefs that are template specializations
                if (!(typedef.Typedef.UnderlyingType is TemplateSpecializationType templateSpecialization))
                { return null; }

                // Get the declaration
                if (!(FindCursor(templateSpecialization.Handle.Declaration) is ClassTemplateSpecializationDecl templateSpecializationDeclaration))
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
                    Diagnostic(Severity.Warning, typedef.Typedef, "The first argument of PxFlags is expected to be an enum.");
                    return null;
                }

                // Get the translation of the enum
                TranslatedDeclaration translatedDeclaration = TryFindTranslation(enumType.Decl);
                if (translatedDeclaration is null)
                {
                    Diagnostic(Severity.Warning, typedef.Typedef, $"Could not find translation of enum '{enumType}'");
                    return null;
                }

                if (!(translatedDeclaration is TranslatedEnum translatedEnum))
                {
                    Diagnostic(Severity.Warning, typedef.Typedef, $"The translation of the enum '{enumType}' is not a {nameof(TranslatedEnum)}");
                    return null;
                }

                // Get the translation of the storage type
                UnderlyingEnumType underlyingType = storageType.ToUnderlyingEnumType(typedef.Typedef, declaration.File);

                // Find the PX_FLAGS_OPERATORS operator overloads
                List<TranslatedFunction> operatorOverloads = new List<TranslatedFunction>(capacity: 3);
                //HACK: We need to get the loose functions, but when transformations run they might already be moved to a container
                // We either need a purpose-built API for enumerating loose declarations or we need to make sure this runs before loose declarations are associated.
                IDeclarationContainer container = declaration.File.__HACK__LooseDeclarationsContainer ?? declaration.File;

                foreach (TranslatedFunction operatorOverload in container.OfType<TranslatedFunction>())
                {
                    // Only consider operator overloads
                    if (!operatorOverload.IsOperatorOverload)
                    { continue; }

                    // Only consider static operator overloads (This is a workaround to make sure the function was a loose function)
                    if (operatorOverload.IsInstanceMethod)
                    { continue; }

                    // Only consider functions which have a return type of PxFlags<,>
                    if (operatorOverload.Function.ReturnType.CanonicalType != templateSpecialization.CanonicalType)
                    { continue; }

                    operatorOverloads.Add(operatorOverload);
                }

                // Create the transformation
                return new PhysxFlagsEnumTransformation(translatedEnum, typedef, underlyingType, operatorOverloads);
            }
        }
    }
}
