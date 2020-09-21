using Biohazrd;
using Biohazrd.Transformation;
using ClangSharp;
using System.Collections.Generic;
using System.Diagnostics;
using ClangType = ClangSharp.Type;

namespace ClangSharpTest2020
{
    //TODO: Add sanity checks that the transformations called for during initialization actually happened.
    public sealed class PhysXFlagsEnumTransformation : TransformationBase
    {
        // This transformation almost supports concurrency, but the fact that we remove elements from the sets as they are processed means that it is not
        // It should be relatively easy to adapt this type to support concurrency, it's just a matter of doing it.
        protected override bool SupportsConcurrency => false;

        private readonly HashSet<TranslatedTypedef> FlagsTypedefs = new(ReferenceEqualityComparer.Instance);
        private readonly Dictionary<EnumDecl, (TranslatedTypedef FlagsTypedef, ClangType UnderlyingType)> FlagsEnums = new();
        private readonly HashSet<ClangType> FlagsCanonicalTypes = new();

        //TODO: This transformation has a heavy reliance on Clang stuff. Ideally it should be able to work with what Biohazrd provides alone.
        protected override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
        {
            // These should generally be empty
            Debug.Assert(FlagsTypedefs.Count == 0, "The state of this transformaiton should be empty at this point.");
            Debug.Assert(FlagsEnums.Count == 0, "The state of this transformaiton should be empty at this point.");
            Debug.Assert(FlagsCanonicalTypes.Count == 0, "The state of this transformaiton should be empty at this point.");
            FlagsTypedefs.Clear();
            FlagsEnums.Clear();
            FlagsCanonicalTypes.Clear();

            // Run an initial pass through the library to identify PxFlags enums
            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                // Look for typedefs
                if (declaration is not TranslatedTypedef typedef)
                { continue; }

                // Look for typedefs that are template specializations
                if (typedef.UnderlyingType is not ClangTypeReference clangType || clangType.ClangType is not TemplateSpecializationType templateSpecialization)
                { continue; }

                // Get the declaration
                if (library.FindClangCursor(templateSpecialization.Handle.Declaration) is not ClassTemplateSpecializationDecl templateSpecializationDeclaration)
                {
                    Debug.Assert(false, "The declaration for a TemplateSpecializationType is expected to be a ClassTemplateSpecializationDecl.");
                    continue;
                }

                // Look for PxFlags
                if (templateSpecializationDeclaration.Name != "PxFlags")
                { continue; }

                // We expect there to be two template arguments: PxFlags<enumtype, storagetype>
                if (templateSpecializationDeclaration.TemplateArgs.Count != 2)
                {
                    Debug.Assert(false, "PxFlags should always have two template arguments.");
                    continue;
                }

                // Extract the arguments
                ClangType enumArgument = templateSpecializationDeclaration.TemplateArgs[0];
                ClangType storageType = templateSpecializationDeclaration.TemplateArgs[1];

                // The first argument should be an EnumType with a corresponding EnumDecl
                if (enumArgument is not EnumType { Decl: EnumDecl enumDecl })
                { continue; }

                // Record the relevant info needed to perform the transformation
                FlagsTypedefs.Add(typedef);
                FlagsEnums.Add(enumDecl, (typedef, storageType));
                FlagsCanonicalTypes.Add(templateSpecialization.CanonicalType); //TODO: Is this the same for all PxFlags?
            }

            return library;
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            FlagsTypedefs.Clear();
            FlagsEnums.Clear();
            FlagsCanonicalTypes.Clear();

            return library;
        }

        protected override TransformationResult TransformTypedef(TransformationContext context, TranslatedTypedef declaration)
        {
            // Delete the targeted typedefs
            if (FlagsTypedefs.Remove(declaration))
            { return null; }

            return declaration;
        }

        protected override TransformationResult TransformEnum(TransformationContext context, TranslatedEnum declaration)
        {
            // Update targeted enums
            if (declaration.Declaration is EnumDecl enumDecl && FlagsEnums.Remove(enumDecl, out (TranslatedTypedef FlagsTypedef, ClangType UnderlyingType) enumInfo))
            {
                return declaration with
                {
                    Name = enumInfo.FlagsTypedef.Name,
                    IsFlags = true,
                    UnderlyingType = new ClangTypeReference(enumInfo.UnderlyingType),
                    SecondaryDeclarations = declaration.SecondaryDeclarations.AddIfNotNull(enumInfo.FlagsTypedef.Declaration)
                };
            }

            return declaration;
        }

        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            // Remove the PX_FLAGS_OPERATORS, which we define as static operator overloads that return one of the enumerated PxFlags<,> types.
            if (declaration is { IsOperatorOverload: true, IsInstanceMethod: false, Declaration: FunctionDecl functionDecl } && FlagsCanonicalTypes.Contains(functionDecl.ReturnType.CanonicalType))
            { return null; }

            return declaration;
        }
    }
}
