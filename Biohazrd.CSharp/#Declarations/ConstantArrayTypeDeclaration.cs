using Biohazrd.OutputGeneration.Metadata;
using Biohazrd.Transformation;
using Biohazrd.Transformation.Infrastructure;
using ClangSharp;
using ClangType = ClangSharp.Type;

namespace Biohazrd.CSharp
{
    public record ConstantArrayTypeDeclaration : TranslatedDeclaration, ICustomTranslatedDeclaration
    {
        internal ClangType OriginalClangElementType { get; }
        internal TranslatedTypeReference ThisTypeReference { get; }

        public TypeReference Type { get; init; }
        public int ElementCount { get; init; }
        public int SizeBytes { get; init; }

        public ConstantArrayTypeDeclaration(ConstantArrayType clangType)
            : base(TranslatedFile.Synthesized)
        {
            Accessibility = AccessModifier.Public;
            Type = new ClangTypeReference(clangType.ElementType);
            ElementCount = checked((int)clangType.Size);
            SizeBytes = checked((int)clangType.Handle.SizeOf);
            Name = $"ConstantArray_{clangType.ElementType}_{ElementCount}";

            // These properties only exist to support CSharpTypeReductionTransformation
            OriginalClangElementType = clangType.ElementType;
            ThisTypeReference = TranslatedTypeReference.Create(this);

            // Place all constant array helpers into the same file
            // Note: This is actually _required_ until https://github.com/InfectedLibraries/Biohazrd/issues/63 is fixed.
            Metadata = Metadata.Add(new OutputFileName("ConstantArrayHelpers"));
        }

        public override string ToString()
            => $"Constant array {Type}[{ElementCount}]";

        TransformationResult ICustomTranslatedDeclaration.TransformChildren(ITransformation transformation, TransformationContext context)
            => this;

        TransformationResult ICustomTranslatedDeclaration.TransformTypeChildren(ITypeTransformation transformation, TransformationContext context)
        {
            DiagnosticAccumulator diagnostics = new();
            SingleTypeTransformHelper newType = new(Type, ref diagnostics);

            // Transform type
            newType.SetValue(transformation.TransformTypeRecursively(context, Type));

            // Create the result
            if (newType.WasChanged || diagnostics.HasDiagnostics)
            {
                return this with
                {
                    Type = newType.NewValue,
                    Diagnostics = Diagnostics.AddRange(diagnostics.MoveToImmutable())
                };
            }
            else
            { return this; }
        }
    }
}
