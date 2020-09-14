using ClangSharp;
using ClangSharp.Interop;
using ClangSharp.Pathogen;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Biohazrd
{
    public sealed record TranslatedFunction : TranslatedDeclaration
    {
        public CallingConvention CallingConvention { get; }

        public TypeReference ReturnType { get; init; }
        public bool ReturnByReference { get; init; }
        public ImmutableArray<TranslatedParameter> Parameters { get; init; }

        public bool IsInstanceMethod { get; }
        public bool IsVirtual { get; }
        public bool IsConst { get; }
        public bool IsOperatorOverload { get; }

        public bool HideFromIntellisense { get; init; } //TODO: C# leak

        public string DllFileName { get; init; } = "TODO.dll";
        public string MangledName { get; }

        internal TranslatedFunction(TranslatedFile file, FunctionDecl function)
            : base(file, function)
        {
            MangledName = function.Handle.Mangling.ToString();
            ReturnType = new ClangTypeReference(function.ReturnType);
            ReturnByReference = function.ReturnType.MustBePassedByReference();

            // Enumerate parameters
            ImmutableArray<TranslatedParameter>.Builder parametersBuilder = ImmutableArray.CreateBuilder<TranslatedParameter>(function.Parameters.Count);

            foreach (ParmVarDecl parameter in function.Parameters)
            { parametersBuilder.Add(new TranslatedParameter(file, parameter)); }

            Parameters = parametersBuilder.MoveToImmutable();

            // Get the function's calling convention
            string errorMessage;
            CXCallingConv clangCallingConvention = function.GetCallingConvention();
            CallingConvention = clangCallingConvention.ToDotNetCallingConvention(out errorMessage);

            if (errorMessage is not null)
            { throw new InvalidOperationException(errorMessage); }

            // Set method-specific properties
            if (function is CXXMethodDecl method)
            {
                Accessibility = method.Access.ToTranslationAccessModifier();
                IsInstanceMethod = !method.IsStatic;
                IsVirtual = method.IsVirtual;
                IsConst = method.IsConst;
            }
            // Non-method defaults
            else
            {
                Accessibility = AccessModifier.Public; // Don't use function.Access here, it's always private on non-method functions for some reason
                IsInstanceMethod = false;
                IsVirtual = false;
                IsConst = false;
            }

            // Handle operator overloads
            ref PathogenOperatorOverloadInfo operatorOverloadInfo = ref function.GetOperatorOverloadInfo();

            if (operatorOverloadInfo.Kind != PathogenOperatorOverloadKind.None)
            {
                Name = $"operator_{operatorOverloadInfo.Name}";
                IsOperatorOverload = true;
            }
            else
            { IsOperatorOverload = false; }

            // Handle conversion operator overloads
            if (function is CXXConversionDecl)
            {
                Name = "____ConversionOperator";
                IsOperatorOverload = true;
            }

            // Rename constructors/destructors
            if (function is CXXConstructorDecl)
            { Name = "Constructor"; }
            else if (function is CXXDestructorDecl)
            { Name = "Destructor"; }
        }

        public override IEnumerator<TranslatedDeclaration> GetEnumerator()
        {
            foreach (TranslatedParameter parameter in Parameters)
            { yield return parameter; }
        }
    }
}
