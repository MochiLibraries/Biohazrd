#if false
using System.Linq;

namespace Biohazrd.Transformations
{
    public sealed class ConstOverloadRenamer : TranslationTransformation
    {
        private readonly TranslatedFunction ConstMethod;

        private ConstOverloadRenamer(TranslatedFunction constMethod)
            => ConstMethod = constMethod;

        public override void Apply()
        {
            ConstMethod.TranslatedName += "_Const";
            ConstMethod.HideFromIntellisense = true;
        }

        public override string ToString()
            => $"Const overload {ConstMethod.Record}::{ConstMethod}";

        public static TranslationTransformation Factory(TranslatedDeclaration declaration)
        {
            // This translation only concerns const methods
            if (declaration is TranslatedFunction function && function.IsConst)
            {
                // If the method has a non-const sibling with the same name, create a transformation
                if (function.Parent.OfType<TranslatedFunction>().Any(other => !other.IsConst && other.TranslatedName == function.TranslatedName))
                { return new ConstOverloadRenamer(function); }
            }

            return null;
        }
    }
}
#endif
