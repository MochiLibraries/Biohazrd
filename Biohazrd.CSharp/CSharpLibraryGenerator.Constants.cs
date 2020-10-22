using Biohazrd.CSharp.Infrastructure;
using Biohazrd.Expressions;
using System.Diagnostics;
using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp
{
    partial class CSharpLibraryGenerator
    {
        private string GetConstantAsString(VisitorContext context, TranslatedDeclaration declaration, ConstantValue constant, TypeReference targetType)
        {
            switch (constant)
            {
                case ICustomCSharpConstantValue cSharpConstantValue:
                    return cSharpConstantValue.GetConstantAsString(this, context, declaration);
                case NullPointerConstant:
                    return "null";
                case IntegerConstant integerConstant:
                {
                    // Bools come through as integer types
                    if (targetType is CSharpBuiltinTypeReference cSharpTypeReference && cSharpTypeReference.Type == CSharpBuiltinType.Bool)
                    {
                        if (integerConstant.Value == 0)
                        { return "false"; }
                        else if (integerConstant.Value == 1)
                        { return "true"; }

                        // Clang seems to always coerce constants with a target type of bool to be 1 or 0, so this probably won't happen for C++ bools,
                        // but in theory it could for older typedef-bools that got transformed to C# bools.
                        // (The C++ spec states abnormal bools are undefined behavior. https://timsong-cpp.github.io/cppwp/n4659/basic.fundamental#6)
                        return $"true /* {integerConstant.Value} */";
                    }

                    // Enums come through as integer types
                    if (targetType is TranslatedTypeReference translatedTypeReference && translatedTypeReference.TryResolve(context.Library, out VisitorContext targetEnumContext) is TranslatedEnum targetEnum)
                    { return GetEnumConstantAsString(context, declaration, integerConstant, targetEnum, targetEnumContext); }

                    return GetIntegerConstantAsStringDirect(context, declaration, integerConstant);
                }
                case DoubleConstant doubleConstant:
                {
                    double value = doubleConstant.Value;
                    if (double.IsNaN(value))
                    {
                        string ret = "double.NaN";

                        if (value.IsUnusualNaN())
                        { ret += $" /* 0x{value.GetBits():X16} */"; }

                        return ret;
                    }
                    else if (double.IsPositiveInfinity(value))
                    { return "double.PositiveInfinity"; }
                    else if (double.IsNegativeInfinity(value))
                    { return "double.NegativeInfinity"; }
                    // Edge case: Negative 0 will ToString as positive 0
                    else if (value == 0.0 && double.IsNegative(value))
                    { return "-0d"; }
                    // Emit friendly constants for MinValue/MaxValue
                    else if (value == double.MinValue)
                    { return "double.MinValue"; }
                    else if (value == double.MaxValue)
                    { return "double.MaxValue"; }
                    else if (value == double.Epsilon)
                    { return "double.Epsilon"; }
                    else
                    {
                        Debug.Assert(double.IsFinite(value), $"The double must be finite at this point!");
                        // G17 is the recommended format specifier for round-trippable doubles
                        // https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#the-round-trip-r-format-specifier
                        // The explicit d suffix is not commonly used, but it makes a double constant without the a decimal point.
                        // https://github.com/dotnet/csharplang/blob/8e7d390f6deaec0a01f690f9689ebf93903f4b00/spec/lexical-structure.md#real-literals
                        return $"{value:G17}d";
                    }
                }
                case FloatConstant floatConstant:
                {
                    float value = floatConstant.Value;
                    if (float.IsNaN(value))
                    {
                        string ret = "float.NaN";

                        if (value.IsUnusualNaN())
                        { ret += $" /* 0x{value.GetBits():X8} */"; }

                        return ret;
                    }
                    else if (float.IsPositiveInfinity(value))
                    { return "float.PositiveInfinity"; }
                    else if (float.IsNegativeInfinity(value))
                    { return "float.NegativeInfinity"; }
                    // Edge case: Negative 0 will ToString as positive 0
                    else if (value == 0f && float.IsNegative(value))
                    { return "-0f"; }
                    // Emit friendly constants for MinValue/MaxValue
                    else if (value == float.MinValue)
                    { return "float.MinValue"; }
                    else if (value == float.MaxValue)
                    { return "float.MaxValue"; }
                    else if (value == float.Epsilon)
                    { return "float.Epsilon"; }
                    else
                    {
                        Debug.Assert(float.IsFinite(value), $"The float must be finite at this point!");
                        // G9 is the recommended format specifier for round-trippable floats
                        // https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#the-round-trip-r-format-specifier
                        return $"{value:G9}f";
                    }
                }
                case StringConstant stringConstant:
                {
                    string ret = $"\"{SanitizeStringLiteral(stringConstant.Value)}\"";

                    // Default string parameter values require special translation that we don't handle in the general case because it's hard to objectively translate them.
                    if (declaration is TranslatedParameter)
                    {
                        Fatal(context, declaration, $"String constants are not supported for parameters.");
                        return $"default /* {SanitizeMultiLineComment(ret)} */";
                    }

                    return ret;
                }
                case UnsupportedConstantExpression unsupportedConstant:
                    Fatal(context, declaration, $"Cannot emit unsupported constant value: {unsupportedConstant.Message}");
                    return "default";
                default:
                    Fatal(context, declaration, $"{constant.GetType().Name} is not supported by the C# output generator.");
                    return "default";
            }
        }

        private string GetEnumConstantAsString(VisitorContext context, TranslatedDeclaration declaration, IntegerConstant constant, TranslatedEnum targetEnum, VisitorContext targetEnumContext)
        {
            // See if any enum values match the constant
            foreach (TranslatedEnumConstant enumConstant in targetEnum.Values)
            {
                if (enumConstant.Value == constant.Value)
                {
                    // This might look weird (because it does), but GetTypeAsString will properly emit the member access to the enum constant.
                    // We should probably add a separate method that handles this situation for us rather than abusing the type infrastructure.
                    return GetTypeAsString(context, declaration, new PreResolvedTypeReference(targetEnumContext.Add(targetEnum), enumConstant));
                }
            }

            // At this point we didn't find a specific enum constant that fits ours
            // We could try to infer a reasonable combination of flags for flags enums here, but we don't since the actual combination isn't available at this point and inferring a reasonable one is tedious.
            // For enums translated as loose constants, we just return the constant.
            string ret = GetIntegerConstantAsStringDirect(context, declaration, constant);

            // For non-loose constant enums, we have to cast
            if (!targetEnum.TranslateAsLooseConstants)
            {
                string cast = $"({GetTypeAsString(context, declaration, new PreResolvedTypeReference(targetEnumContext, targetEnum))})";

                // Casting a negative number requires parenthesis.
                if (constant.IsSigned && constant.SignedValue < 0)
                { ret = $"{cast}({ret})"; }
                else
                { ret = $"{cast}{ret}"; }
            }

            return ret;
        }

        private string GetIntegerConstantAsStringDirect(VisitorContext context, TranslatedDeclaration declaration, IntegerConstant constant)
            => constant.IsSigned ? constant.SignedValue.ToString() : constant.Value.ToString();
    }
}
