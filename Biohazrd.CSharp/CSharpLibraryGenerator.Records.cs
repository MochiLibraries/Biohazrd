using ClangSharp;
using System.Linq;
using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp
{
    partial class CSharpLibraryGenerator
    {
        protected override void VisitRecord(VisitorContext context, TranslatedRecord declaration)
        {
            Writer.Using("System.Runtime.InteropServices");

            Writer.EnsureSeparation();
            Writer.Write($"[StructLayout(LayoutKind.Explicit, Size = {declaration.Size}");

            // Write out CharSet.Unicode if necessary. An explicit charset ensures char fields in this struct are considered blittable by the runtime
            // https://github.com/dotnet/runtime/blob/29e9b5b7fd95231d9cd9d3ae351404e63cbb6d5a/src/coreclr/src/vm/fieldmarshaler.cpp#L233-L235
            foreach (TranslatedNormalField field in declaration.Members.OfType<TranslatedNormalField>())
            {
                if (field.Type.IsCSharpType(CSharpBuiltinType.Char))
                {
                    Writer.Write(", CharSet = CharSet.Unicode");
                    break;
                }
            }

            Writer.WriteLine(")]");
            Writer.WriteLine($"{declaration.Accessibility.ToCSharpKeyword()} unsafe partial struct {SanitizeIdentifier(declaration.Name)}");
            using (Writer.Block())
            {
                VisitorContext childContext = context.Add(declaration);

                // Emit base field
                if (declaration.NonVirtualBaseField is not null)
                { VisitBaseField(childContext, declaration.NonVirtualBaseField); }

                // Emit normal members
                foreach (TranslatedDeclaration member in declaration.Members)
                { Visit(childContext, member); }

                // Emit VTable+Field
                if (declaration.VTable is not null && declaration.VTableField is not null)
                { EmitVTable(childContext, declaration.VTableField, declaration.VTable); }

                // List any unsupported members
                if (declaration.UnsupportedMembers.Count > 0)
                {
                    Diagnostics.Add(Severity.Warning, $"Record {declaration.Name} has {declaration.UnsupportedMembers.Count} unsupported members which will not be translated.");
                    Writer.EnsureSeparation();
                    Writer.WriteLine("// The following members could not be translated:");

                    foreach (TranslatedDeclaration unsupportedMember in declaration.UnsupportedMembers)
                    { Writer.WriteLine($"// {unsupportedMember.GetType().Name} {unsupportedMember.Name}"); }
                }
            }
        }

        private void EmitVTable(VisitorContext context, TranslatedVTableField field, TranslatedVTable vTable)
        {
            // Emit the VTable field
            Writer.EnsureSeparation();
            StartField(field);
            Writer.WriteLine($"{SanitizeIdentifier(vTable.Name)}* {SanitizeIdentifier(field.Name)};");

            // Emit the VTable type
            Writer.EnsureSeparation();
            Writer.Using("System.Runtime.InteropServices");
            Writer.WriteLine("[StructLayout(LayoutKind.Sequential)]");
            Writer.WriteLine($"{vTable.Accessibility.ToCSharpKeyword()} unsafe struct {SanitizeIdentifier(vTable.Name)}");
            using (Writer.Block())
            {
                bool foundFirstFunctionPointer = false;
                foreach (TranslatedVTableEntry entry in vTable.Entries)
                {
                    // If this entry is a function pointer kind, we can start writing
                    // (VTables have non-vtable stuff like RTTI before the vtable pointer, we don't want to translate those.)
                    if (entry.IsFunctionPointer)
                    { foundFirstFunctionPointer = true; }

                    // If we haven't found a function pointer yet, we don't want to start writing
                    if (!foundFirstFunctionPointer)
                    { continue; }

                    // For function pointers, write out the signature of the method as a documentation comment
                    //TODO: This could/should reference the translated method if there is one.
                    if (entry.IsFunctionPointer)
                    { Writer.WriteLine($"/// <summary>Virtual method pointer for `{entry.Info.MethodDeclaration}`</summary>"); }

                    Writer.Write($"{entry.Accessibility.ToCSharpKeyword()} ");

                    if (entry.IsFunctionPointer && entry.MethodReference?.TryResolve(context.Library) is TranslatedFunction associatedFunction)
                    {
                        EmitFunctionContext emitContext = new(context, associatedFunction);
                        EmitFunctionPointerForVTable(context, emitContext, associatedFunction);
                    }
                    else
                    { WriteType(context.Add(entry), entry, VoidTypeReference.PointerInstance); }

                    Writer.WriteLine($" {SanitizeIdentifier(entry.Name)};");
                }
            }
        }

        protected override void VisitVTable(VisitorContext context, TranslatedVTable declaration)
            => FatalContext(context, declaration, $"w/ {declaration.Entries.Length} entries");

        protected override void VisitVTableEntry(VisitorContext context, TranslatedVTableEntry declaration)
            => FatalContext(context, declaration, $"({declaration.Info.Kind} to {declaration.Info.MethodDeclaration})");

        protected override void VisitSynthesizedLooseDeclarationsType(VisitorContext context, SynthesizedLooseDeclarationsTypeDeclaration declaration)
        {
            Writer.EnsureSeparation();
            Writer.WriteLine($"{declaration.Accessibility.ToCSharpKeyword()} unsafe static partial class {SanitizeIdentifier(declaration.Name)}");
            using (Writer.Block())
            {
                context = context.Add(declaration);

                foreach (TranslatedDeclaration member in declaration.Members)
                { Visit(context, member); }
            }
        }
    }
}
