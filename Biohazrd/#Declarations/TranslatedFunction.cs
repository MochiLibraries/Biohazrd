using ClangSharp;
using ClangSharp.Interop;
using ClangSharp.Pathogen;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace Biohazrd
{
    public sealed partial record TranslatedFunction : TranslatedDeclaration
    {
        /// <remarks>Will be <c>null</c> when <see cref="IsCallable"/> is <c>false</c>.</remarks>
        public PathogenArrangedFunction? FunctionAbi { get; }

        /// <summary>Whether or not the function is callable</summary>
        /// <remarks>
        /// An example of an uncallable function would be a function returning an undefined type, for example the <c>TestMethod1</c> and <c>TestMethod2</c> below are
        /// uncallable since <c>MyStruct</c> is forward-declared but never defined:
        /// 
        /// <code>
        /// struct MyStruct;
        /// MyStruct TestMethod1();       // IsCallable = false, FunctionAbi = null
        /// void TestMethod2(MyStruct s); // IsCallable = false, FunctionAbi = null
        /// void TestMetod3(MyStruct* s); // IsCallable = true,  FunctionAbi = non-null
        /// </code>
        /// </remarks>
        [MemberNotNullWhen(true, nameof(FunctionAbi))]
        public bool IsCallable => FunctionAbi is not null;

        public CallingConvention CallingConvention { get; init; }

        public TypeReference ReturnType { get; init; }
        /// <remarks>
        /// Since it is impossible to determine whether a return is done by reference when a function is uncallable, this is always <c>false</c> when <see cref="IsCallable"/> is <c>false</c>.
        /// </remarks>
        public bool ReturnByReference => FunctionAbi is null ? false : FunctionAbi.ReturnInfo.Kind == PathogenArgumentKind.Indirect;
        public ImmutableArray<TranslatedParameter> Parameters { get; init; }

        public bool IsInstanceMethod { get; init; }
        public bool IsVirtual { get; init; }
        public bool IsConst { get; init; }
        public bool IsInline { get; init; }

        public SpecialFunctionKind SpecialFunctionKind { get; init; }

        public string DllFileName { get; init; } = "TODO.dll";
        public string MangledName { get; init; }

        internal unsafe TranslatedFunction(TranslationUnitParser parsingContext, TranslatedFile file, FunctionDecl function)
            : base(file, function)
        {
            using (CXString mangling = function.Handle.Mangling)
            { MangledName = mangling.ToString(); }

            ReturnType = new ClangTypeReference(function.ReturnType);
            IsInline = function.IsInlined;
            SpecialFunctionKind = SpecialFunctionKind.None;

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

            // If possible, arrange the function call
            if (!function.IsCallable(out ImmutableArray<string> uncallableDiagnostics))
            {
                string uncallableMessage = "Function is not callable:";

                switch (uncallableDiagnostics.Length)
                {
                    case 0:
                        Debug.Fail($"IsCallable should always return at least one diagnostic upon failure.");
                        uncallableMessage += " An internal error ocurred";
                        break;
                    case 1:
                        uncallableMessage += $" {uncallableDiagnostics[0]}";
                        break;
                    default:
                        foreach (string diagnostic in uncallableDiagnostics)
                        { uncallableMessage += $"\n* {diagnostic}"; }
                        break;
                }

                Diagnostics = Diagnostics.Add(Severity.Error, uncallableMessage);
            }
            else
            {
                Debug.Assert(uncallableDiagnostics.IsDefaultOrEmpty);

                PathogenCodeGenerator? codeGenerator = null;
                try
                {
                    codeGenerator = parsingContext.CodeGeneratorPool.Rent();
                    FunctionAbi = new PathogenArrangedFunction(codeGenerator, function);
                }
                finally
                {
                    if (codeGenerator is not null)
                    { parsingContext.CodeGeneratorPool.Return(codeGenerator); }
                }

                // Assert the arranged function matches our expectations
                int expectedArgumentCount = function.Parameters.Count;

                if (IsInstanceMethod)
                { expectedArgumentCount++; }

                Debug.Assert(FunctionAbi.ArgumentCount == expectedArgumentCount);

                Debug.Assert(IsInstanceMethod == FunctionAbi.Flags.HasFlag(PathogenArrangedFunctionFlags.IsInstanceMethod));
            }

            // Enumerate parameters
            ImmutableArray<TranslatedParameter>.Builder parametersBuilder = ImmutableArray.CreateBuilder<TranslatedParameter>(function.Parameters.Count);
            {
                int parameterIndex = IsInstanceMethod ? 1 : 0;

                foreach (ParmVarDecl parameter in function.Parameters)
                {
                    parametersBuilder.Add(new TranslatedParameter(file, parameter, FunctionAbi?.Arguments[parameterIndex] ?? default));
                    parameterIndex++;
                }
            }

            Parameters = parametersBuilder.MoveToImmutable();

            // Get the function's calling convention
            string errorMessage;
            CXCallingConv clangCallingConvention = function.GetCallingConvention();
            CallingConvention = clangCallingConvention.ToDotNetCallingConvention(out errorMessage);

            if (errorMessage is not null)
            { throw new InvalidOperationException(errorMessage); }

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
                Name = "Destructor";

                Debug.Assert(SpecialFunctionKind == SpecialFunctionKind.None);
                SpecialFunctionKind = SpecialFunctionKind.Destructor;

                // clang_Cursor_getMangling returns the name of the vbase destructor (prefix ?_D), which is not what gets exported by MSVC and is thus useless for our purposes.
                // We instead want the normal destructor (prefix ?1) which is the only value returned by Clang for destructors when targeting the Microsoft ABI so we use the first mangling for them.
                // (In theory constructors are in a similar position where Clang uses a different Itanium-equivalent type to query the mangled name, but the C++ constructor name mangling doesn't change for these.)
                // Some more details are provided here: https://github.com/InfectedLibraries/Biohazrd/issues/12#issuecomment-782604833
                unsafe
                {
                    CXStringSet* manglings = null;
                    try
                    {
                        manglings = function.Handle.CXXManglings;
                        Debug.Assert(manglings->Count > 0);

                        if (manglings->Count > 0)
                        { MangledName = manglings->Strings[0].ToString(); }
                    }
                    finally
                    {
                        if (manglings != null)
                        { clang.disposeStringSet(manglings); }
                    }
                }
            }
        }

        public override string ToString()
        {
            string baseString = base.ToString();

            string methodNameString = SpecialFunctionKind switch
            {
                SpecialFunctionKind.Constructor => "Constructor",
                SpecialFunctionKind.Destructor => "Destructor",
                SpecialFunctionKind.OperatorOverload => "Operator Overload",
                SpecialFunctionKind.ConversionOverload => "Conversion Operator",
                _ => Declaration is CXXMethodDecl ? "Method" : "Function"
            };

            if (IsVirtual)
            { return $"Virtual {methodNameString} {baseString}"; }
            else if (IsInstanceMethod)
            { return $"Instance {methodNameString} {baseString}"; }
            else if (Declaration is CXXMethodDecl)
            { return $"Static {methodNameString} {baseString}"; }
            else
            { return $"{methodNameString} {baseString}"; }
        }
    }
}
