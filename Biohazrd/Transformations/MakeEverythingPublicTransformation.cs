#if false
namespace Biohazrd.Transformations
{
    public sealed class MakeEverythingPublicTransformation : TranslationTransformation
    {
        private readonly TranslatedDeclaration Target;

        private MakeEverythingPublicTransformation(TranslatedDeclaration target)
            => Target = target;

        public override void Apply()
            => Target.Accessibility = AccessModifier.Public;

        public static TranslationTransformation Factory(TranslatedDeclaration declaration)
            => new MakeEverythingPublicTransformation(declaration);
    }
}
#endif
