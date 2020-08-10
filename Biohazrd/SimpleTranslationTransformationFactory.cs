using System;

namespace ClangSharpTest2020
{
    internal sealed class SimpleTranslationTransformationFactory : TranslationTransformationFactory
    {
        private Func<TranslatedDeclaration, TranslationTransformation> FactoryMethod;

        internal SimpleTranslationTransformationFactory(Func<TranslatedDeclaration, TranslationTransformation> factoryMethod)
            => FactoryMethod = factoryMethod;

        protected override TranslationTransformation Create(TranslatedDeclaration declaration)
            => FactoryMethod(declaration);
    }
}
