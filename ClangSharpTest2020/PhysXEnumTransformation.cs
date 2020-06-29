using System.Diagnostics;

namespace ClangSharpTest2020
{
    public sealed class PhysXEnumTransformation : TranslationTransformation
    {
        private readonly TranslatedEnum TargetEnum;
        private readonly TranslatedRecord ContainingRecord;

        private PhysXEnumTransformation(TranslatedEnum targetEnum, TranslatedRecord containingRecord)
        {
            TargetEnum = targetEnum;
            ContainingRecord = containingRecord;
        }

        public override void Apply()
        {
            TargetEnum.TranslatedName = ContainingRecord.TranslatedName;
            ContainingRecord.ReplaceWith(TargetEnum);
        }

        public override string ToString()
            => $"PhysX-style scoped enum {ContainingRecord}::{TargetEnum}";

        public static TranslationTransformation Factory(TranslatedDeclaration declaration)
        {
            if (declaration is TranslatedEnum translatedEnum && translatedEnum.Parent is TranslatedRecord containingRecord && containingRecord.Members.Count == 1)
            {
                Debug.Assert(containingRecord.Members[0] == translatedEnum);
                return new PhysXEnumTransformation(translatedEnum, containingRecord);
            }

            return null;
        }
    }
}
