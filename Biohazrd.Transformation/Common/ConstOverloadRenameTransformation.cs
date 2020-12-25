using Biohazrd.Transformation.Common.Metadata;
using System.Linq;

namespace Biohazrd.Transformation.Common
{
    public sealed class ConstOverloadRenameTransformation : TransformationBase
    {
        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            // If the function is const and has any non-const siblings have the same name, add a suffix to this function and hide it from Intellisense
            if (declaration.IsConst && context.Parent.OfType<TranslatedFunction>().Any(other => !other.IsConst && other.Name == declaration.Name))
            {
                return declaration with
                {
                    Name = declaration.Name + "_Const",
                    Metadata = declaration.Metadata.Add<HideDeclarationFromCodeCompletion>()
                };
            }

            return declaration;
        }
    }
}
