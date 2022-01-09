using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp
{
    partial class CSharpLibraryGenerator
    {
        private bool HasWrittenAnyConstantArrayTypeDeclarations = false;
        private const string ConstantArrayEnumeratorName = "ConstantArrayEnumerator";
        private const string ConstantArrayOfPointersEnumeratorName = "ConstantArrayOfPointersEnumerator";

        protected override void VisitConstantArrayType(VisitorContext context, ConstantArrayTypeDeclaration declaration)
        {
            const string element0Name = "Element0";
            const string element0PointerName = "Element0Pointer";

            // If this is the first constant array type we've written out, write out the enumerator helpers
            if (!HasWrittenAnyConstantArrayTypeDeclarations)
            {
                HasWrittenAnyConstantArrayTypeDeclarations = true;
                WriteOutConstantArrayEnumerators();
            }

            Writer.Using("System"); // IndexOutOfRangeException, IntPtr
            Writer.Using("System.Runtime.InteropServices"); // StructLayoutAttribute, FieldOffsetAttribute

            Writer.EnsureSeparation();
            Writer.WriteLine($"[StructLayout(LayoutKind.Explicit, Size = {declaration.SizeBytes})]");
            Writer.WriteLine($"{declaration.Accessibility.ToCSharpKeyword()} unsafe partial struct {SanitizeIdentifier(declaration.Name)}");
            using (Writer.Block())
            {
                // Write out the 0th element reference field
                string elementType = GetTypeAsString(context, declaration, declaration.Type);
                int elementCount = declaration.ElementCount;
                Writer.EnsureSeparation();
                Writer.WriteLine($"[FieldOffset(0)] private {elementType} {element0Name};");

                // Write out the 0th element pointer getter
                // (This assumes that these unmanaged constant arrays are never stored on the managed heap.)
                Writer.EnsureSeparation();
                Writer.WriteLine($"private {elementType}* {element0PointerName}");
                using (Writer.Block())
                {
                    Writer.WriteLine("get");
                    using (Writer.Block())
                    {
                        Writer.WriteLine($"fixed ({elementType}* p{element0Name} = &{element0Name})");
                        Writer.WriteLine($"{{ return p{element0Name}; }}");
                    }
                }

                // Write out the indexer
                Writer.EnsureSeparation();
                Writer.WriteLine($"public ref {elementType} this[int index]");
                using (Writer.Block())
                {
                    Writer.WriteLine("get");
                    using (Writer.Block())
                    {
                        Writer.WriteLine($"if ((uint)index < {elementCount})");
                        Writer.WriteLine($"{{ return ref {element0PointerName}[index]; }}");
                        Writer.WriteLine("else");
                        Writer.WriteLine("{ throw new IndexOutOfRangeException(); }");
                    }
                }

                // Write out the length
                // This used to be a constant, but C# doesn't let you reference constants as instance members and these infrastructure types shouldn't really ever be referenced directly.
                Writer.EnsureSeparation();
                Writer.WriteLine($"public int Length => {elementCount};");

                // Write out ToString implementaiton
                Writer.EnsureSeparation();
                Writer.WriteLine($"public override string ToString()");
                Writer.WriteLineIndented($"=> $\"{{typeof({elementType})}}[{elementCount}]\";");

                // Write out ToArray
                Writer.EnsureSeparation();
                Writer.WriteLine($"public {elementType}[] ToArray()");
                using (Writer.Block())
                {
                    Writer.WriteLine($"{elementType}[] result = new {elementType}[{elementCount}];");
                    Writer.WriteLine();
                    Writer.WriteLine($"for (int i = 0; i < {elementCount}; i++)");
                    Writer.WriteLine("{ result[i] = this[i]; }");
                    Writer.WriteLine();
                    Writer.WriteLine("return result;");
                }

                // Write out AsSpan
                // (Can't do this when the element type is a pointer because you can't use pointers as generic type arguments.)
                if (declaration.Type is not PointerTypeReference)
                {
                    Writer.EnsureSeparation();
                    Writer.WriteLine($"public Span<{elementType}> AsSpan()");
                    Writer.WriteLineIndented($"=> new Span<{elementType}>({element0PointerName}, {elementCount});");
                }

                // Write out GetEnumerator
                // (Can't do this when the element type is a double pointer)
                if (declaration.Type is not PointerTypeReference { Inner: PointerTypeReference })
                {
                    string enumeratorType;
                    if (declaration.Type is PointerTypeReference pointerElementType)
                    { enumeratorType = $"{ConstantArrayOfPointersEnumeratorName}<{GetTypeAsString(context, declaration, pointerElementType.Inner)}>"; }
                    else
                    { enumeratorType = $"{ConstantArrayEnumeratorName}<{elementType}>"; }

                    Writer.EnsureSeparation();
                    Writer.WriteLine($"public {enumeratorType} GetEnumerator()");
                    Writer.WriteLineIndented($"=> new {enumeratorType}({element0PointerName}, {elementCount});");
                }
            }
        }

        private void WriteOutConstantArrayEnumerators()
        {
            const string element0Name = "Element0";
            const string countName = "Count";
            const string indexName = "Index";

            Writer.EnsureSeparation();
            Writer.WriteLine($"public unsafe ref struct {ConstantArrayEnumeratorName}<T>");
            Writer.WriteLineIndented($"where T : unmanaged");
            using (Writer.Block())
            {
                Writer.WriteLine($"private readonly T* {element0Name};");
                Writer.WriteLine($"private readonly int {countName};");
                Writer.WriteLine($"private int {indexName};");
                Writer.WriteLine();
                Writer.WriteLine($"public T Current => (uint){indexName} < {countName} ? {element0Name}[{indexName}] : default;");

                Writer.EnsureSeparation();
                Writer.WriteLine($"internal {ConstantArrayEnumeratorName}(T* element0, int count)");
                using (Writer.Block())
                {
                    Writer.WriteLine($"{element0Name} = element0;");
                    Writer.WriteLine($"{countName} = count;");
                    Writer.WriteLine($"{indexName} = -1;");
                }

                Writer.EnsureSeparation();
                Writer.WriteLine("public bool MoveNext()");
                using (Writer.Block())
                {
                    Writer.WriteLine($"int index = {indexName} + 1;");
                    Writer.WriteLine($"if (index < {countName})");
                    using (Writer.Block())
                    {
                        Writer.WriteLine($"{indexName} = index;");
                        Writer.WriteLine("return true;");
                    }
                    Writer.WriteLine();
                    Writer.WriteLine("return false;");
                }
            }

            Writer.EnsureSeparation();
            Writer.WriteLine($"public unsafe ref struct {ConstantArrayOfPointersEnumeratorName}<T>");
            Writer.WriteLineIndented($"where T : unmanaged");
            using (Writer.Block())
            {
                Writer.WriteLine($"private readonly T** {element0Name};");
                Writer.WriteLine($"private readonly int {countName};");
                Writer.WriteLine($"private int {indexName};");
                Writer.WriteLine();
                Writer.WriteLine($"public T* Current => (uint){indexName} < {countName} ? {element0Name}[{indexName}] : default;");

                Writer.EnsureSeparation();
                Writer.WriteLine($"internal {ConstantArrayOfPointersEnumeratorName}(T** element0, int count)");
                using (Writer.Block())
                {
                    Writer.WriteLine($"{element0Name} = element0;");
                    Writer.WriteLine($"{countName} = count;");
                    Writer.WriteLine($"{indexName} = -1;");
                }

                Writer.EnsureSeparation();
                Writer.WriteLine("public bool MoveNext()");
                using (Writer.Block())
                {
                    Writer.WriteLine($"int index = {indexName} + 1;");
                    Writer.WriteLine($"if (index < {countName})");
                    using (Writer.Block())
                    {
                        Writer.WriteLine($"{indexName} = index;");
                        Writer.WriteLine("return true;");
                    }
                    Writer.WriteLine();
                    Writer.WriteLine("return false;");
                }
            }
        }
    }
}
