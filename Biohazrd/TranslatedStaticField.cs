using ClangSharp;
using ClangSharp.Interop;

namespace ClangSharpTest2020
{
    /// <summary>A translated static field or global variable.</summary>
    public sealed class TranslatedStaticField : TranslatedDeclaration
    {
        private readonly VarDecl VariableDeclaration;
        public override string DefaultName => VariableDeclaration.Name;
        public override bool CanBeRoot => false;

        internal TranslatedStaticField(IDeclarationContainer parent, VarDecl variableDeclaration)
            : base(parent)
        {
            VariableDeclaration = variableDeclaration;
            Declaration = VariableDeclaration;

            if (!(VariableDeclaration.CursorParent is RecordDecl) || VariableDeclaration.Access == CX_CXXAccessSpecifier.CX_CXXPublic)
            { Accessibility = AccessModifier.Public; }
            else
            { Accessibility = AccessModifier.Private; }

            File.Diagnostic(Severity.Note, VariableDeclaration, "The translation of static fields/globals is slightly lazy. Consider improving.");
        }

        private void TranslateType(CodeWriter writer)
        {
            File.WriteType(writer, VariableDeclaration.Type, VariableDeclaration, TypeTranslationContext.ForField);
            writer.Write("*");
        }

        protected override void TranslateImplementation(CodeWriter writer)
        {
            writer.Using("System.Runtime.InteropServices"); // For NativeLibrary
            writer.EnsureSeparation();

            writer.Write($"{Accessibility.ToCSharpKeyword()} static readonly ");
            TranslateType(writer);
            writer.Write(" ");
            writer.WriteIdentifier(TranslatedName);
            writer.Write(" = (");
            TranslateType(writer);
            //TODO: This leaks handles to the native library.
            writer.WriteLine($")NativeLibrary.GetExport(NativeLibrary.Load(\"TODO.dll\"), \"{CodeWriter.SanitizeStringLiteral(VariableDeclaration.Handle.Mangling.ToString())}\");");
        }
    }
}
