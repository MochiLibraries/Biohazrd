#if false
using Biohazrd.Expressions;

namespace Biohazrd;

public struct TemplateArgument
{
    //TODO: Reference to the parameter decl?
    // Adding that makes this struct expensive to construct. Maybe we should just remove it from the base template parameter decl
    private object RawValue;

    public TypeReference? Type => RawValue as TypeReference;
    public ConstantValue? Value => RawValue as ConstantValue;

    public TemplateArgument(TypeReference type)
        => RawValue = type;

    public TemplateArgument(ConstantValue value)
        => RawValue = value;

    public static implicit operator TemplateArgument?(TypeReference? type)
        => type is null ? null : new TemplateArgument(type);

    public static implicit operator TemplateArgument?(ConstantValue? value)
        => value is null ? null : new TemplateArgument(value);
}
#endif
