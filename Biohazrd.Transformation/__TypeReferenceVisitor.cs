using System.Collections.Immutable;

namespace Biohazrd.Transformation
{
    /// <summary>
    /// Rough prototype for https://github.com/InfectedLibraries/Biohazrd/issues/189
    /// </summary>
    internal abstract class __TypeReferenceVisitor : DeclarationVisitor
    {
        protected override void VisitEnum(VisitorContext context, TranslatedEnum declaration)
        {
            VisitTypeReference(context, declaration, declaration.UnderlyingType);
            base.VisitEnum(context, declaration);
        }

        protected override void VisitFunction(VisitorContext context, TranslatedFunction declaration)
        {
            VisitTypeReference(context, declaration, declaration.ReturnType);
            base.VisitFunction(context, declaration);
        }

        protected override void VisitParameter(VisitorContext context, TranslatedParameter declaration)
        {
            VisitTypeReference(context, declaration, declaration.Type);
            base.VisitParameter(context, declaration);
        }

        protected override void VisitStaticField(VisitorContext context, TranslatedStaticField declaration)
        {
            VisitTypeReference(context, declaration, declaration.Type);
            base.VisitStaticField(context, declaration);
        }

        protected override void VisitBaseField(VisitorContext context, TranslatedBaseField declaration)
        {
            VisitTypeReference(context, declaration, declaration.Type);
            base.VisitBaseField(context, declaration);
        }

        protected override void VisitNormalField(VisitorContext context, TranslatedNormalField declaration)
        {
            VisitTypeReference(context, declaration, declaration.Type);
            base.VisitNormalField(context, declaration);
        }

        protected override void VisitTypedef(VisitorContext context, TranslatedTypedef declaration)
        {
            VisitTypeReference(context, declaration, declaration.UnderlyingType);
            base.VisitTypedef(context, declaration);
        }

        protected override void VisitVTableEntry(VisitorContext context, TranslatedVTableEntry declaration)
        {
            VisitTypeReference(context, declaration, declaration.Type);
            base.VisitVTableEntry(context, declaration);
        }


        private void VisitTypeReference(VisitorContext parentDeclarationContext, TranslatedDeclaration parentDeclaration, TypeReference typeReference)
            => VisitTypeReference(parentDeclarationContext.Add(parentDeclaration), ImmutableArray<TypeReference>.Empty, typeReference);

        protected virtual void VisitTypeReference(VisitorContext context, ImmutableArray<TypeReference> parentTypeReferences, TypeReference typeReference)
        {
            switch (typeReference)
            {
                case PointerTypeReference pointerTypeReference:
                {
                    parentTypeReferences = parentTypeReferences.Add(typeReference);
                    VisitTypeReference(context, parentTypeReferences, pointerTypeReference.Inner);
                    return;
                }
                case FunctionPointerTypeReference functionPointerTypeReference:
                {
                    parentTypeReferences = parentTypeReferences.Add(typeReference);
                    VisitTypeReference(context, parentTypeReferences, functionPointerTypeReference.ReturnType);

                    foreach (TypeReference parameterTypeReference in functionPointerTypeReference.ParameterTypes)
                    { VisitTypeReference(context, parentTypeReferences, parameterTypeReference); }

                    return;
                }
            }
        }
    }
}
