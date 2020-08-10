//TODO: Global variables
using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.IO;

namespace ClangSharpTest2020
{
    public sealed class GenerateModuleDefinitionTransformation : IDisposable
    {
        private readonly StreamWriter DefWriter;
        private readonly StreamWriter InlineReferenceFile;
        private int NextInlineReferenceNumber = 0;

        public GenerateModuleDefinitionTransformation(string filePath, List<string> includeFiles)
        {
            DefWriter = new StreamWriter(filePath);
            DefWriter.WriteLine("; This file was automatically generated");
            DefWriter.WriteLine("LIBRARY PhysXPathogen_64");
            DefWriter.WriteLine("EXPORTS");

            InlineReferenceFile = new StreamWriter(Path.ChangeExtension(filePath, "cpp"));
            InlineReferenceFile.WriteLine("// This file was automatically generated");

            foreach (string file in includeFiles)
            { InlineReferenceFile.WriteLine($"#include \"{file}\""); }

            InlineReferenceFile.WriteLine();
        }

        private static void WriteOutNamespaceAndType(StreamWriter writer, Cursor cursor, bool skipFirstRecord = false)
        {
            if (cursor is TranslationUnitDecl || cursor.CursorParent is null)
            { return; }

            bool skipWrite = false;

            if (skipFirstRecord && cursor is RecordDecl)
            {
                skipWrite = true;
                skipFirstRecord = false;
            }

            WriteOutNamespaceAndType(writer, cursor.CursorParent, skipFirstRecord);

            if (skipWrite)
            { return; }

            switch (cursor)
            {
                case NamespaceDecl namespaceDeclaration:
                    writer.Write($"{namespaceDeclaration.Name}::");
                    return;
                case RecordDecl recordDeclaration:
                    writer.Write($"{recordDeclaration.Name}::");
                    return;
            }
        }

        private void WriteFunctionReference(TranslatedFunction function, FunctionDecl functionDeclaration, CXXMethodDecl methodDeclaration)
        {
            // It'd be nice if we could type these variables with `auto`, but we need target typing for overloads.
            InlineReferenceFile.Write($"{functionDeclaration.ReturnType.CanonicalType} (");

            if (methodDeclaration is object && !methodDeclaration.IsStatic)
            {
                WriteOutNamespaceAndType(InlineReferenceFile, functionDeclaration);
                InlineReferenceFile.Write("* ");
            }
            else
            { InlineReferenceFile.Write("*"); }

            InlineReferenceFile.Write($"unused{NextInlineReferenceNumber})(");

            bool first = true;
            foreach (ParmVarDecl parameter in functionDeclaration.Parameters)
            {
                if (first)
                { first = false; }
                else
                { InlineReferenceFile.Write(", "); }

                InlineReferenceFile.Write(parameter.Type.CanonicalType.ToString());
            }

            InlineReferenceFile.Write(")");

            if (methodDeclaration is object && methodDeclaration.IsConst)
            { InlineReferenceFile.Write(" const"); }

            InlineReferenceFile.Write(" = &");
            WriteOutNamespaceAndType(InlineReferenceFile, functionDeclaration);
            InlineReferenceFile.WriteLine($"{functionDeclaration};");
        }

        private void WriteConstructorReference(TranslatedFunction function, CXXConstructorDecl constructor)
        {
            InlineReferenceFile.Write($"void __{function.Record.TranslatedName}__ctor(void* _this");

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

                InlineReferenceFile.Write($", {parameterString}");
                i++;
            }

            InlineReferenceFile.Write(") { new (_this) ");
            WriteOutNamespaceAndType(InlineReferenceFile, constructor, skipFirstRecord: true);
            InlineReferenceFile.Write($"{constructor.Name}(");

            for (i = 0; i < constructor.Parameters.Count; i++)
            {
                if (i > 0)
                { InlineReferenceFile.Write(", "); }

                InlineReferenceFile.Write($"_{i}");
            }

            InlineReferenceFile.WriteLine("); }");
        }

        public TranslationTransformation Factory(TranslatedDeclaration declaration)
        {
            if (declaration is TranslatedFunction function)
            {
                FunctionDecl functionDeclaration = function.Function;
                CXXMethodDecl methodDeclaration = functionDeclaration as CXXMethodDecl;
                CXXConstructorDecl constructorDeclaration = methodDeclaration as CXXConstructorDecl;
                CXXDestructorDecl destructorDeclaration = methodDeclaration as CXXDestructorDecl;

                // Skip destructors for now.
                if (destructorDeclaration is object)
                { return null; }

                // Skip private and protected members for now.
                // (Private will probably never work, protected requires special handling.)
                if (functionDeclaration.Access == CX_CXXAccessSpecifier.CX_CXXPrivate || functionDeclaration.Access == CX_CXXAccessSpecifier.CX_CXXProtected)
                { return null; }

                // Skip virtual functions
                if (function.IsVirtual)
                { return null; }

                //HACK: This function (PxRepXInstantiationArg::operator=) is never defined in PhysX
                if (function.Record?.DefaultName == "PxRepXInstantiationArgs" && function.IsOperatorOverload)
                { return null; }

                // Static non-method functions cannot be exported
                if (methodDeclaration is null && functionDeclaration.StorageClass == CX_StorageClass.CX_SC_Static)
                { return null; }

                // Skip constructors on abstract tyes
                if (constructorDeclaration is object && function.Record.Record is CXXRecordDecl cppClass && cppClass.IsAbstract)
                { return null; }

                DefWriter.WriteLine($"    {function.Function.Handle.Mangling}");

                // If the function is inline, ensure it is referenced so it'll end up in the compiler output
                if (functionDeclaration.IsInlined)
                {
                    //InlineReferenceFile.WriteLine("/*");
                    //ClangSharpInfoDumper.Dump(InlineReferenceFile, functionDeclaration);
                    //InlineReferenceFile.WriteLine("*/");

                    if (constructorDeclaration is object)
                    { WriteConstructorReference(function, constructorDeclaration); }
                    else
                    { WriteFunctionReference(function, functionDeclaration, methodDeclaration); }

                    NextInlineReferenceNumber++;
                }
            }

            return null;
        }

        public void Dispose()
        {
            DefWriter?.Dispose();
            InlineReferenceFile?.Dispose();
        }
    }
}
