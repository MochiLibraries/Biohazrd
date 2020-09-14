using ClangSharp;
using ClangSharp.Pathogen;

namespace Biohazrd
{
    public sealed record TranslatedVTableEntry : TranslatedDeclaration
    {
        public PathogenVTableEntry Info { get; }

        public bool IsFunctionPointer { get; }
        public CXXMethodDecl? MethodDeclaration { get; }

        public TypeReference Type { get; init; }

        internal TranslatedVTableEntry(TranslationUnitParser parsingContext, TranslatedFile file, PathogenVTableEntry info, string name)
            : base(file)
        {
            Name = name;
            Accessibility = AccessModifier.Public;

            Info = info;
            Type = VoidTypeReference.PointerInstance;

            IsFunctionPointer = false;
            MethodDeclaration = null;

            if (Info.Kind.IsFunctionPointerKind())
            {
                IsFunctionPointer = true;
                Cursor methodDeclarationCursor = parsingContext.FindCursor(info.MethodDeclaration);

                if (methodDeclarationCursor is CXXMethodDecl methodDeclaration)
                {
                    MethodDeclaration = methodDeclaration;
                    Type = new ClangTypeReference(methodDeclaration.Type);
                }
                else
                { Diagnostics = Diagnostics.Add(Severity.Warning, $"VTable function point did not resolve to a C++ method declaration."); }
            }
        }



        public override string ToString()
            => $"VTable {Info.Kind} {base.ToString()}";
    }
}
