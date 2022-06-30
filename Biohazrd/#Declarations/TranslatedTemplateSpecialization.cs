using ClangSharp;

namespace Biohazrd
{
    public sealed record TranslatedTemplateSpecialization : TranslatedRecord
    {
        //TODO: Provide an array of types/constants for the template arguments so that they can be reasoned about.

        internal TranslatedTemplateSpecialization(TranslationUnitParser parsingContext, TranslatedFile file, ClassTemplateSpecializationDecl specialization)
            : base(parsingContext, file, specialization)
        {
            // Override the name with the specialized string
            // (This won't result in a name that's very friendly for emit, but it makes it easier to tell what this specialization actually is.)
            Name = specialization.TypeForDecl.ToString();
        }

        public override string ToString()
            => base.ToString();
    }
}
