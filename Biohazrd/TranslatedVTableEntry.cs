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

                if (methodDeclarationCursor is CXXMethodDecl { Type: FunctionProtoType functionType }  methodDeclaration)
                {
                    MethodDeclaration = methodDeclaration;

                    FunctionPointerTypeReference functionTypeReference = new(functionType);

                    //TODO: This depends on the calling convention
                    // Add the retbuf parameter if necessary
                    if (functionType.ReturnType.MustBePassedByReference())
                    {
                        functionTypeReference = functionTypeReference with
                        {
                            ParameterTypes = functionTypeReference.ParameterTypes.Insert(0, functionTypeReference.ReturnType),
                            ReturnType = VoidTypeReference.Instance
                        };
                    }

                    // Add the this pointer parameter
                    TypeReference thisPointerType = VoidTypeReference.PointerInstance;

                    if (methodDeclaration.Parent is RecordDecl recordDeclaration)
                    { thisPointerType = new PointerTypeReference(new TranslatedTypeReference(recordDeclaration)); }
                    else
                    { Diagnostics = Diagnostics.Add(Severity.Warning, $"Could not figure out this pointer type for {methodDeclaration}."); }

                    functionTypeReference = functionTypeReference with
                    {
                        ParameterTypes = functionTypeReference.ParameterTypes.Insert(0, thisPointerType)
                    };

                    Type = functionTypeReference;
                }
                else
                { Diagnostics = Diagnostics.Add(Severity.Warning, $"VTable function point did not resolve to a C++ method declaration."); }
            }
        }



        public override string ToString()
            => $"VTable {Info.Kind} {base.ToString()}";
    }
}
