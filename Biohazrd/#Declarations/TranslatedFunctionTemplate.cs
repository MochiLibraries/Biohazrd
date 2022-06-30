using ClangSharp;
using ClangSharp.Pathogen;
using System.Diagnostics;

namespace Biohazrd;

public sealed record TranslatedFunctionTemplate : TranslatedTemplate
{
    public bool IsInstanceMethod { get; init; }
    public bool IsConst { get; init; }
    public bool IsInline { get; init; }

    public SpecialFunctionKind SpecialFunctionKind { get; init; }

    public TranslatedFunctionTemplate(TranslatedFile file, FunctionTemplateDecl functionTemplate)
        : base(file, functionTemplate)
    {
        //TODO: A lot of the logic here is duplicated from TranslatedFunction, we should consider adding a helper for all this.
        FunctionDecl function = functionTemplate.AsFunction;

        IsInline = function.IsInlined;
        SpecialFunctionKind = SpecialFunctionKind.None;

        if (function is CXXMethodDecl method)
        {
            Accessibility = method.Access.ToTranslationAccessModifier();
            IsInstanceMethod = !method.IsStatic;
            Debug.Assert(!method.IsVirtual, "Template functions are not expected to be able to be virtual.");
            IsConst = method.IsConst;
        }
        else
        {
            Accessibility = AccessModifier.Public;
            IsInstanceMethod = false;
            IsConst = false;
        }

        // Handle operator overloads
        ref PathogenOperatorOverloadInfo operatorOverloadInfo = ref function.GetOperatorOverloadInfo();

        if (operatorOverloadInfo.Kind != PathogenOperatorOverloadKind.None)
        {
            Name = $"operator_{operatorOverloadInfo.Name}";

            Debug.Assert(SpecialFunctionKind == SpecialFunctionKind.None);
            SpecialFunctionKind = SpecialFunctionKind.OperatorOverload;
        }

        // Handle conversion operator overloads
        if (function is CXXConversionDecl)
        {
            Name = $"____ConversionOperator_{function.ReturnType}";

            Debug.Assert(SpecialFunctionKind == SpecialFunctionKind.None);
            SpecialFunctionKind = SpecialFunctionKind.ConversionOverload;
        }

        // Handle constructors/destructors
        if (function is CXXConstructorDecl)
        {
            Name = "Constructor";

            Debug.Assert(SpecialFunctionKind == SpecialFunctionKind.None);
            SpecialFunctionKind = SpecialFunctionKind.Constructor;
        }
        else if (function is CXXDestructorDecl)
        {
            Debug.Fail("Destructors are not expected to be able to be template functions.");
            Name = "Destructor";

            Debug.Assert(SpecialFunctionKind == SpecialFunctionKind.None);
            SpecialFunctionKind = SpecialFunctionKind.Destructor;
        }
    }

    public override string ToString()
        => $"Function{base.ToString()}";
}
