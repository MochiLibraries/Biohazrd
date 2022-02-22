using Biohazrd.CSharp.Infrastructure;
using Biohazrd.Transformation;
using Biohazrd.Transformation.Infrastructure;
using System;
using System.ComponentModel;
using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp
{
    /// <summary>This type works around the fact that .NET does not have a way to use booleans in P/Invokes without marshaling.</summary>
    /// <remarks>
    /// This type is also required to use 1-byte booleans in the context of function pointers, which cannot express marshaling semantics.
    ///
    /// See https://github.com/InfectedLibraries/Biohazrd/issues/99 for details.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete($"This declaration is only ever added by {nameof(WrapNonBlittableTypesWhereNecessaryTransformation)}, which is deprecated.")]
    public sealed record NativeBooleanDeclaration : TranslatedDeclaration, ICustomTranslatedDeclaration, ICustomCSharpTranslatedDeclaration
    {
        public NativeBooleanDeclaration()
            : base(TranslatedFile.Synthesized)
            => Name = "NativeBoolean";

        TransformationResult ICustomTranslatedDeclaration.TransformChildren(ITransformation transformation, TransformationContext context)
            => this;

        TransformationResult ICustomTranslatedDeclaration.TransformTypeChildren(ITypeTransformation transformation, TransformationContext context)
            => this;

        void ICustomCSharpTranslatedDeclaration.GenerateOutput(ICSharpOutputGenerator outputGenerator, VisitorContext context, CSharpCodeWriter writer)
            => Emit(Name, writer);

        internal static void Emit(CSharpCodeWriter writer)
            => Emit("NativeBoolean", writer);

        private static void Emit(string name, CSharpCodeWriter writer)
        {
            writer.Using("System"); // IComprable, IComparable<T>, IEquatable<T>
            writer.Using("System.Runtime.InteropServices"); // StructLayoutAttribute, LayoutKind
            writer.Using("System.Runtime.CompilerServices"); // MethodImplAttribute, MethodImplOptions, Unsafe
            string sanitizedName = SanitizeIdentifier(name);

            // Developers should typically not use this type directly anyway, but we provide the same instance methods as System.Boolean
            // for scenarios where the return type of a native function is immeidately consumed.
            // IE: Console.WriteLine(MyNativeFunction().ToString());
            //
            // Note that we do not bother implementing IConvertible since it is not likely to be used and
            // and all but one of its methods are explicit interface implementations anyway.
            // That one method is seemingly never used directly: https://apisof.net/catalog/System.Boolean.System.IConvertible.GetTypeCode%28%29
            writer.EnsureSeparation();
            writer.WriteLine("[StructLayout(LayoutKind.Sequential)]"); // This prevents a warning on the Value field.
            writer.WriteLine($"public readonly partial struct {sanitizedName} : IComparable, IComparable<bool>, IEquatable<bool>, IComparable<{sanitizedName}>, IEquatable<{sanitizedName}>");
            using (writer.Block())
            {
                // Note: You get slightly better codegen in some scenarios if this is a bool with MarshalAs attached to it,
                //  but obviously at the expense of having the marshaler touch it. This probably only moves the cost,
                //  so let's avoid getting the marshaler involed at all.
                writer.WriteLine("private readonly byte Value;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public static implicit operator bool({sanitizedName} b)");
                writer.WriteLineIndented($"=> Unsafe.As<{sanitizedName}, bool>(ref b);");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public static implicit operator {sanitizedName}(bool b)");
                writer.WriteLineIndented($"=> Unsafe.As<bool, {sanitizedName}>(ref b);");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public override int GetHashCode()");
                writer.WriteLineIndented("=> Unsafe.As<byte, bool>(ref Unsafe.AsRef(in Value)).GetHashCode();");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public override string ToString()");
                writer.WriteLineIndented("=> Unsafe.As<byte, bool>(ref Unsafe.AsRef(in Value)).ToString();");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public string ToString(IFormatProvider? provider)");
                writer.WriteLineIndented("=> Unsafe.As<byte, bool>(ref Unsafe.AsRef(in Value)).ToString(provider);");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public bool TryFormat(Span<char> destination, out int charsWritten)");
                writer.WriteLineIndented("=> Unsafe.As<byte, bool>(ref Unsafe.AsRef(in Value)).TryFormat(destination, out charsWritten);");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public override bool Equals(object? obj)");
                using (writer.Indent())
                {
                    writer.WriteLine("=> obj switch");
                    writer.WriteLine('{');
                    using (writer.Indent())
                    {
                        writer.WriteLine("bool boolean => this == boolean,");
                        writer.WriteLine($"{sanitizedName} nativeBool => this == nativeBool,");
                        writer.WriteLine("_ => false");
                    }
                    writer.WriteLine("};");
                }
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public bool Equals(bool other)");
                writer.WriteLineIndented("=> this == other;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public bool Equals({sanitizedName} other)");
                writer.WriteLineIndented("=> this == other;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public int CompareTo(object? obj)");
                writer.WriteLineIndented("=> Unsafe.As<byte, bool>(ref Unsafe.AsRef(in Value)).CompareTo(obj);");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public int CompareTo(bool value)");
                writer.WriteLineIndented("=> Unsafe.As<byte, bool>(ref Unsafe.AsRef(in Value)).CompareTo(value);");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public int CompareTo({sanitizedName} value)");
                writer.WriteLineIndented($"=> CompareTo(Unsafe.As<{sanitizedName}, bool>(ref value));");
            }
        }

        public override string ToString()
            => base.ToString();
    }
}
