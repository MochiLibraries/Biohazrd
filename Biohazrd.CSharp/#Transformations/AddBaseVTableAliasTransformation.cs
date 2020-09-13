using Biohazrd.Transformation;

namespace Biohazrd.CSharp
{
    public sealed class AddBaseVTableAliasTransformation : TransformationBase
    {
        protected override TransformationResult TransformRecord(TransformationContext context, TranslatedRecord declaration)
        {
            // Synthesize a VTable field for types which have a base type, a VTable, but no VTable field
            // It is assumed the base type directly or indirectly contains the VTable pointer in C++
            //TODO: Verify the base type actually has a VTable pointer
            if (declaration.VTableField is null && declaration.VTable is not null && declaration.NonVirtualBaseField is not null)
            {
                // We expect the base field to be at 0
                if (declaration.NonVirtualBaseField.Offset != 0)
                { return declaration.WithWarning("Record has VTable without a VTable field and a base which is not at offset 0."); }

                return declaration with { VTableField = new TranslatedVTableField(declaration.NonVirtualBaseField) };
            }

            return declaration;
        }
    }
}
