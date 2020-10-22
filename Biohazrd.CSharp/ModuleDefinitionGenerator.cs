using Biohazrd.OutputGeneration;
using ClangSharp;
using ClangSharp.Interop;
using System.IO;

namespace Biohazrd.CSharp
{
    public static class ModuleDefinitionGenerator
    {
        internal static bool CanFunctionBeExported(TranslatedFunction declaration)
        {
            // Since this method concerns the C++ semantics, we want to consider the C++ declarations directly
            FunctionDecl? function = declaration.Declaration as FunctionDecl;
            CXXMethodDecl? method = function as CXXMethodDecl;
            CXXConstructorDecl? constructor = method as CXXConstructorDecl;
            CXXDestructorDecl? destructor = method as CXXDestructorDecl;

            // Biohazrd functions that aren't backed by Clang functions cannot be exported
            if (function is null)
            { return false; }

            // Virtual functions do not need to be exported because they are accessed via the VTable
            if (method is { IsVirtual: true })
            { return false; }

            // Skip destructors for now
            if (destructor is not null)
            { return false; }

            // Skip private and protected members for now.
            // (Private will probably never work, protected requires special handling.)
            if (function.Access == CX_CXXAccessSpecifier.CX_CXXPrivate || function.Access == CX_CXXAccessSpecifier.CX_CXXProtected)
            { return false; }

            // Static non-method functions cannot be exported
            if (method is null && function.StorageClass == CX_StorageClass.CX_SC_Static)
            { return false; }

            // Skip constructors on abstract types
            if (constructor is not null && constructor.CursorParent is CXXRecordDecl { IsAbstract: true })
            { return false; }

            // If we got this far, the function can be exported
            return true;
        }

        public static void Generate(OutputSession session, string filePath, TranslatedLibrary library)
        {
            StreamWriter writer = session.Open<StreamWriter>(filePath);
            session.WriteHeader(writer, "; ");
            writer.WriteLine("EXPORTS");

            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (declaration is TranslatedFunction function && CanFunctionBeExported(function))
                { writer.WriteLine($"    {function.MangledName}"); }
            }
        }
    }
}
