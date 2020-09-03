using Biohazrd;
using Biohazrd.Transformation;

namespace ClangSharpTest2020
{
    /// <summary>Transforms a PhysX-style scoped enum into a modern scoped enum.</summary>
    /// <remarks>
    /// In PhysX, enums are scoped by placing them into an empty struct:
    /// <code>
    /// struct PxExampleEnum
    /// {
    ///     enum Enum
    ///     {
    ///         eEXAMPLE_ONE,
    ///         eEXAMPLE_TWO,
    ///         eEXAMPLE_THREE
    ///     };
    /// };
    /// </code>
    ///
    /// This transform effectively replaces the scoping struct with the enum its self:
    /// <code>
    /// enum class PxExampleEnum
    /// {
    ///     eEXAMPLE_ONE,
    ///     eEXAMPLE_TWO,
    ///     eEXAMPLE_THREE
    /// };
    /// </code>
    /// </remarks>
    public sealed class PhysXEnumTransformation : TransformationBase
    {
        protected override TransformationResult TransformRecord(TransformationContext context, TranslatedRecord declaration)
        {
            // A PhysX enum record is one that has no members except a single EnumDeclaration.
            if (declaration.Members.Count == 1 && declaration.TotalMemberCount == 1 && declaration.Members[0] is TranslatedEnum enumDeclaration)
            {
                return enumDeclaration with
                {
                    Name = declaration.Name,
                    TranslateAsLooseConstants = false
                };
            }

            return declaration;
        }
    }
}
