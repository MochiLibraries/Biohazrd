using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp
{
    partial class CSharpLibraryGenerator
    {
        private void StartField(TranslatedField field)
        {
            Writer.Using("System.Runtime.InteropServices");

            Writer.EnsureSeparation();
            Writer.Write($"[FieldOffset({field.Offset})] {field.Accessibility.ToCSharpKeyword()} ");
        }

        protected override void VisitField(VisitorContext context, TranslatedField declaration)
            => Fatal(context, declaration, "The field kind is unsupported.", $"@ {declaration.Offset}");

        protected override void VisitUnimplementedField(VisitorContext context, TranslatedUnimplementedField declaration)
            => VisitField(context, declaration);

        protected override void VisitVTableField(VisitorContext context, TranslatedVTableField declaration)
            // VTable concerns are handled in the record emit
            => FatalContext(context, declaration);

        protected override void VisitBaseField(VisitorContext context, TranslatedBaseField declaration)
        {
            StartField(declaration);
            WriteType(context, declaration, declaration.Type);
            Writer.Write(' ');
            Writer.WriteIdentifier(declaration.Name);
            Writer.WriteLine(';');
        }

        protected override void VisitNormalField(VisitorContext context, TranslatedNormalField declaration)
        {
            //TODO: Bitfields
            if (declaration.IsBitField)
            {
                Fatal(context, declaration, "Bitfields are not supported.", $"@ {declaration.Offset}");
                return;
            }

            StartField(declaration);
            WriteType(context, declaration, declaration.Type);
            Writer.Write(' ');
            Writer.WriteIdentifier(declaration.Name);
            Writer.WriteLine(';');
        }

        protected override void VisitStaticField(VisitorContext context, TranslatedStaticField declaration)
        {
            Writer.Using("System.Runtime.InteropServices"); // For NativeLibrary
            Writer.EnsureSeparation();

            Writer.Write($"{declaration.Accessibility.ToCSharpKeyword()} static readonly ");
            WriteTypeAsReference(context, declaration, declaration.Type);
            Writer.Write(' ');
            Writer.WriteIdentifier(declaration.Name);
            Writer.Write(" = (");
            WriteTypeAsReference(context, declaration, declaration.Type);
            //TODO: This leaks handles to the native library.
            Writer.Write($")NativeLibrary.GetExport(NativeLibrary.Load(\"{SanitizeStringLiteral(declaration.DllFileName)}\"), \"{SanitizeStringLiteral(declaration.MangledName)}\");");
        }
    }
}
