using ClangSharp;
using ClangSharp.Pathogen;

namespace Biohazrd
{
    public sealed record TranslatedVTableEntry : TranslatedDeclaration
    {
        public PathogenVTableEntry Info { get; }

        public bool IsFunctionPointer { get; }
        public DeclarationReference? MethodReference { get; init; }

        internal TranslatedVTableEntry(TranslationUnitParser parsingContext, TranslatedFile file, PathogenVTableEntry info, string name)
            : base(file)
        {
            Name = name;
            Accessibility = AccessModifier.Public;

            Info = info;

            IsFunctionPointer = false;
            MethodReference = null;

            if (Info.Kind.IsFunctionPointerKind())
            {
                IsFunctionPointer = true;
                Cursor methodDeclarationCursor = parsingContext.FindCursor(info.MethodDeclaration);

                if (methodDeclarationCursor is CXXMethodDecl methodDeclaration)
                { MethodReference = new DeclarationReference(methodDeclaration); }
                else
                { Diagnostics = Diagnostics.Add(Severity.Warning, $"VTable function pointer resolved to a {methodDeclarationCursor.GetType().Name} rather than a C++ method declaration."); }
            }
        }

        public override string ToString()
            => $"VTable {Info.Kind} {base.ToString()}";
    }
}
