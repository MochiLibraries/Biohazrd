using ClangSharp.Pathogen;
using System.Text;
using System.Runtime.InteropServices;

namespace Biohazrd.Expressions
{
    public abstract record ConstantValue
    { }

    public static class PathogenConstantValueInfoEx
    {
        public unsafe static ConstantValue ToConstantExpression(this in PathogenConstantValueInfo info)
        {
            switch (info.Kind)
            {
                case PathogenConstantValueKind.Unknown:
                    ClangConstantValueKind clangKind = (ClangConstantValueKind)info.SubKind;
                    return new UnsupportedConstantExpression($"Unsupported {clangKind} constant");
                case PathogenConstantValueKind.NullPointer:
                    return NullPointerConstant.Instance;
                case PathogenConstantValueKind.UnsignedInteger:
                    return new IntegerConstant()
                    {
                        SizeBits = info.SubKind,
                        Value = info.Value,
                        IsSigned = false
                    };
                case PathogenConstantValueKind.SignedInteger:
                    return new IntegerConstant()
                    {
                        SizeBits = info.SubKind,
                        Value = info.Value,
                        IsSigned = true
                    };
                case PathogenConstantValueKind.FloatingPoint:
                {
                    ulong value = info.Value;
                    switch (info.SubKind)
                    {
                        case sizeof(float) * 8:
                            return new FloatConstant(*(float*)&value);
                        case sizeof(double) * 8:
                            return new DoubleConstant(*(double*)&value);
                        default:
                            return new UnsupportedConstantExpression($"Unsupported {info.SubKind} bit floating point constant");
                    }
                }
                case PathogenConstantValueKind.String:
                {
                    PathogenStringConstantKind encodingKind = (PathogenStringConstantKind)info.SubKind;
                    PathogenConstantString* rawStringValue = (PathogenConstantString*)info.Value;

                    Encoding? encoding = encodingKind switch
                    {
                        PathogenStringConstantKind.Ascii => Encoding.ASCII,
                        //TODO: Need to query the actual width rather than relying on the OS the generator is running under.
                        PathogenStringConstantKind.WideChar => !RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Encoding.UTF32 : Encoding.Unicode,
                        PathogenStringConstantKind.Utf8 => Encoding.UTF8,
                        PathogenStringConstantKind.Utf16 => Encoding.Unicode,
                        PathogenStringConstantKind.Utf32 => Encoding.UTF32,
                        _ => null
                    };

                    if (encoding is null)
                    { return new UnsupportedConstantExpression($"Unsupported string encoding {encodingKind}"); }

                    string stringValue = encoding.GetString(&rawStringValue->FirstByte, checked((int)rawStringValue->SizeBytes));
                    return new StringConstant(stringValue);
                }
                default:
                    return new UnsupportedConstantExpression($"Unsupported constant kind {info.Kind}");
            }
        }
    }
}
