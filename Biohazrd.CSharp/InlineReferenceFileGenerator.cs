using Biohazrd.OutputGeneration;
using ClangSharp;

namespace Biohazrd.CSharp
{
    public sealed class InlineReferenceFileGenerator : DeclarationVisitor
    {
        private readonly CppCodeWriter Writer;
        private int NextInlineReferenceNumber = 0;

        private InlineReferenceFileGenerator(OutputSession session, string filePath)
            => Writer = session.Open<CppCodeWriter>(filePath);

        public static void Generate(OutputSession session, string filePath, TranslatedLibrary library)
        {
            InlineReferenceFileGenerator generator = new(session, filePath);
            generator.Visit(library);
            generator.Writer.Finish();
        }

        protected override void VisitFunction(VisitorContext context, TranslatedFunction declaration)
        {
            if (!ModuleDefinitionGenerator.CanFunctionBeExported(declaration))
            { return; }

            Writer.Include(declaration.File.FilePath);

            switch (declaration.Declaration)
            {
                case CXXConstructorDecl constructorDeclaration:
                    WriteConstructorReference(context, declaration, constructorDeclaration);
                    break;
                case FunctionDecl functionDeclaration:
                    WriteFunctionReference(context, declaration, functionDeclaration, functionDeclaration as CXXMethodDecl);
                    break;
            }
        }

        private void WriteFunctionReference(VisitorContext context, TranslatedFunction function, FunctionDecl functionDeclaration, CXXMethodDecl? methodDeclaration)
        {
            // Note: We can't use auto here because we need to be able to handle overloaded functions.
            Writer.Write($"{functionDeclaration.ReturnType.CanonicalType} (");

            if (methodDeclaration is object && !methodDeclaration.IsStatic)
            {
                WriteOutNamespaceAndType(functionDeclaration);
                Writer.Write("* ");
            }
            else
            { Writer.Write("*"); }

            Writer.Write($"unused{NextInlineReferenceNumber})(");
            NextInlineReferenceNumber++;

            bool first = true;
            foreach (ParmVarDecl parameter in functionDeclaration.Parameters)
            {
                if (first)
                { first = false; }
                else
                { Writer.Write(", "); }

                Writer.Write(parameter.Type.CanonicalType.ToString());
            }

            Writer.Write(")");

            if (methodDeclaration is object && methodDeclaration.IsConst)
            { Writer.Write(" const"); }

            Writer.Write(" = &");
            WriteOutNamespaceAndType(functionDeclaration);
            Writer.WriteLine($"{functionDeclaration};");
        }

        private void WriteConstructorReference(VisitorContext context, TranslatedFunction function, CXXConstructorDecl constructor)
        {
            string? typeName = context.ParentDeclaration?.Name;

            if (typeName is null)
            {
                //TODO: Emit an error
                return;
            }

            Writer.Write($"void __{typeName}__ctor(void* _this");

            int i = 0;
            foreach (ParmVarDecl parameter in constructor.Parameters)
            {
                string parameterString = parameter.Type.CanonicalType.ToString();
                string parameterName = $" _{i}";

                //HACK: This is far from ideal and might be problematic in some cases
                int startOfArray = parameterString.IndexOf("[");
                if (startOfArray == -1)
                { parameterString += parameterName; }
                else
                { parameterString = parameterString.Insert(startOfArray, parameterName); }

                Writer.Write($", {parameterString}");
                i++;
            }

            Writer.Write(") { new (_this) ");
            WriteOutNamespaceAndType(constructor, skipFirstRecord: true);
            Writer.Write($"{constructor.Name}(");

            for (i = 0; i < constructor.Parameters.Count; i++)
            {
                if (i > 0)
                { Writer.Write(", "); }

                Writer.Write($"_{i}");
            }

            Writer.WriteLine("); }");
        }

        private void WriteOutNamespaceAndType(Cursor cursor, bool skipFirstRecord = false)
        {
            if (cursor is TranslationUnitDecl || cursor.CursorParent is null)
            { return; }

            bool skipWrite = false;

            if (skipFirstRecord && cursor is RecordDecl)
            {
                skipWrite = true;
                skipFirstRecord = false;
            }

            WriteOutNamespaceAndType(cursor.CursorParent, skipFirstRecord);

            if (skipWrite)
            { return; }

            switch (cursor)
            {
                case NamespaceDecl namespaceDeclaration:
                    Writer.Write($"{namespaceDeclaration.Name}::");
                    return;
                case RecordDecl recordDeclaration:
                    Writer.Write($"{recordDeclaration.Name}::");
                    return;
            }
        }
    }
}
