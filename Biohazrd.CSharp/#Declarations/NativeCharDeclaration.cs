using Biohazrd.CSharp.Infrastructure;
using Biohazrd.Transformation;
using Biohazrd.Transformation.Infrastructure;
using System;
using System.ComponentModel;
using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp
{
    /// <summary>This type works around the fact that you can't specify marshaling for chars on function pointers.</summary>
    /// <remarks>See https://github.com/InfectedLibraries/Biohazrd/issues/99 for details.</remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete($"This declaration is only ever added by {nameof(WrapNonBlittableTypesWhereNecessaryTransformation)}, which is deprecated.")]
    public sealed record NativeCharDeclaration : TranslatedDeclaration, ICustomTranslatedDeclaration, ICustomCSharpTranslatedDeclaration
    {
        public NativeCharDeclaration()
            : base(TranslatedFile.Synthesized)
            => Name = "NativeChar";

        TransformationResult ICustomTranslatedDeclaration.TransformChildren(ITransformation transformation, TransformationContext context)
            => this;

        TransformationResult ICustomTranslatedDeclaration.TransformTypeChildren(ITypeTransformation transformation, TransformationContext context)
            => this;

        void ICustomCSharpTranslatedDeclaration.GenerateOutput(ICSharpOutputGenerator outputGenerator, VisitorContext context, CSharpCodeWriter writer)
            => Emit(Name, writer);

        internal static void Emit(CSharpCodeWriter writer)
            => Emit("NativeChar", writer);

        private static void Emit(string name, CSharpCodeWriter writer)
        {
            writer.Using("System"); // IComparable, IComparable<T>, IEquatable<T>
            writer.Using("System.Runtime.InteropServices"); // StructLayoutAttribute, LayoutKind
            writer.Using("System.Runtime.CompilerServices"); // MethodImplAttribute, MethodImplOptions, Unsafe
            string sanitizedName = SanitizeIdentifier(name);

            // Developers should typically not use this type directly anyway, but we provide the same instance methods as System.Char
            // for scenarios where the return type of a native function is immeidately consumed.
            // IE: Console.WriteLine(MyNativeFunction().ToString());
            //
            // Note that we do not bother implementing IConvertible since it is not likely to be used and
            // and all but one of its methods are explicit interface implementations anyway.
            // That one method is seemingly never used directly: https://apisof.net/catalog/System.Boolean.System.IConvertible.GetTypeCode%28%29
            //
            // Using CharSet.Unicode is the important part here. The CLR will treat the `Value` field as blittable when the struct is marked as Unicode.
            // https://github.com/dotnet/runtime/blob/29e9b5b7fd95231d9cd9d3ae351404e63cbb6d5a/src/coreclr/src/vm/fieldmarshaler.cpp#L233-L235
            writer.EnsureSeparation();
            writer.WriteLine("[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]");
            writer.WriteLine($"public readonly partial struct {sanitizedName} : IComparable, IComparable<char>, IEquatable<char>, IComparable<{sanitizedName}>, IEquatable<{sanitizedName}>");
            using (writer.Block())
            {
                writer.WriteLine("private readonly char Value;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"private {sanitizedName}(char value)");
                writer.WriteLineIndented("=> Value = value;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public static implicit operator char({sanitizedName} c)");
                writer.WriteLineIndented("=> c.Value;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public static implicit operator {sanitizedName}(char c)");
                writer.WriteLineIndented($"=> new {sanitizedName}(c);");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public static bool operator ==({sanitizedName} a, {sanitizedName} b)");
                writer.WriteLineIndented("=> a.Value == b.Value;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public static bool operator !=({sanitizedName} a, {sanitizedName} b)");
                writer.WriteLineIndented("=> a.Value != b.Value;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public static bool operator ==(char a, {sanitizedName} b)");
                writer.WriteLineIndented("=> a == b.Value;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public static bool operator !=(char a, {sanitizedName} b)");
                writer.WriteLineIndented("=> a != b.Value;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public static bool operator ==({sanitizedName} a, char b)");
                writer.WriteLineIndented("=> a.Value == b;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public static bool operator !=({sanitizedName} a, char b)");
                writer.WriteLineIndented("=> a.Value != b;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public override int GetHashCode()");
                writer.WriteLineIndented("=> Value.GetHashCode();");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public override bool Equals(object? obj)");
                using (writer.Indent())
                {
                    writer.WriteLine("=> obj switch");
                    writer.WriteLine('{');
                    using (writer.Indent())
                    {
                        writer.WriteLine("char character => this == character,");
                        writer.WriteLine($"{sanitizedName} nativeChar => this == nativeChar,");
                        writer.WriteLine("_ => false");
                    }
                    writer.WriteLine("};");
                }
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public bool Equals(char other)");
                writer.WriteLineIndented("=> this == other;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public bool Equals({sanitizedName} other)");
                writer.WriteLineIndented("=> this == other;");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public int CompareTo(object? obj)");
                writer.WriteLineIndented("=> Value.CompareTo(obj);");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public int CompareTo(char other)");
                writer.WriteLineIndented("=> Value.CompareTo(other);");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine($"public int CompareTo({sanitizedName} value)");
                writer.WriteLineIndented("=> Value.CompareTo(value.Value);");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public override string ToString()");
                writer.WriteLineIndented("=> Value.ToString();");
                writer.WriteLine();
                writer.WriteLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
                writer.WriteLine("public string ToString(IFormatProvider? provider)");
                writer.WriteLineIndented("=> Value.ToString(provider);");
            }
        }

        public override string ToString()
            => base.ToString();
    }
}
