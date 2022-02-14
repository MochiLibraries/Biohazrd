using Biohazrd.CSharp.Trampolines;
using Biohazrd.Transformation;
using ClangSharp.Pathogen;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Biohazrd.CSharp;

public sealed class CreateTrampolinesTransformation : CSharpTransformationBase
{
    public TargetRuntime TargetRuntime { get; init; } = CSharpGenerationOptions.Default.TargetRuntime; //TODO: This ideally should come from some central context to ensure consistency

    protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
    {
        // Don't try to add trampolines to a function which already has them
        if (declaration.Metadata.Has<TrampolineCollection>())
        { return declaration; }

        // Can't generate trampolines when the function ABI is unknown
        if (declaration.FunctionAbi is null)
        { return declaration; }

        // Create diagnostic accumulator for diagnostics relating to the default friendly trampoline
        ImmutableArray<TranslationDiagnostic>.Builder? friendlyTrampolineDiagnostics = null;
        void PrimaryTrampolineProblem(Severity severity, string message)
        {
            friendlyTrampolineDiagnostics ??= ImmutableArray.CreateBuilder<TranslationDiagnostic>();
            friendlyTrampolineDiagnostics.Add(severity, message);
        }

        // Build dummy native trampoline
        IReturnAdapter? nativeReturnAdapter = null;
        ImmutableArray<Adapter>.Builder nativeAdapters;
        {
            int expectedNativeParameterCount = declaration.Parameters.Length;

            // Add parameter slot for this pointer
            if (declaration.IsInstanceMethod)
            { expectedNativeParameterCount++; }

            // Add parameter slot for return buffer
            if (declaration.ReturnByReference)
            { expectedNativeParameterCount++; }

            nativeAdapters = ImmutableArray.CreateBuilder<Adapter>(expectedNativeParameterCount);
        }

        IReturnAdapter? friendlyReturnAdapter = null;
        Dictionary<Adapter, Adapter>? friendlyAdapters = null;

        void AddFriendlyAdapter(Adapter target, Adapter adapter)
        {
            friendlyAdapters ??= new Dictionary<Adapter, Adapter>(nativeAdapters.Count);
            friendlyAdapters.Add(target, adapter);
        }

        // Handle return type when not returning by reference
        if (!declaration.ReturnByReference)
        {
            TypeReference returnType = declaration.ReturnType;

            // Handle returning bool
            if (TargetRuntime < TargetRuntime.Net7 && returnType == CSharpBuiltinType.Bool)
            {
                //TODO: If function is virtual method rewrite to NativeBoolean instead.
                nativeReturnAdapter = new PassthroughReturnAdapter(CSharpBuiltinType.Byte);
                friendlyReturnAdapter = new ByteToBoolReturnAdapter(nativeReturnAdapter);
            }
            //TODO: Handle char for virtual methods
            // Handle returning void
            else if (returnType is VoidTypeReference)
            { nativeReturnAdapter = VoidReturnAdapter.Instance; }
            // Handle typical return
            else
            { nativeReturnAdapter = new PassthroughReturnAdapter(returnType); }
        }

        // Handle implicit parameters
        {
            void CreateNativeReturnByReferenceAdapter()
            {
                TypeReference returnType = declaration.ReturnType;
                TypeReference returnBufferType = new PointerTypeReference(returnType);

                // Create native return adapter and return buffer parameter
                Debug.Assert(nativeReturnAdapter is null);
                nativeReturnAdapter = new PassthroughReturnAdapter(returnBufferType);
                Adapter returnBufferParameter = new PassthroughAdapter(declaration, SpecialAdapterKind.ReturnBuffer, returnBufferType);
                nativeAdapters.Add(returnBufferParameter);

                // Create friendly adapter for return buffer
                ReturnByReferenceAdapter returnByReferenceAdapter = new(returnBufferParameter);
                friendlyReturnAdapter = returnByReferenceAdapter;
                AddFriendlyAdapter(returnBufferParameter, returnByReferenceAdapter);
            }

            // Add return buffer before this pointer
            // (This also handles normal non-implicit-by-reference return.)
            if (declaration.ReturnByReference && !declaration.FunctionAbi.ReturnInfo.Flags.HasFlag(PathogenArgumentFlags.IsSRetAfterThis))
            { CreateNativeReturnByReferenceAdapter(); }

            // Add this pointer
            if (declaration.IsInstanceMethod)
            {
                TypeReference thisType;
                if (context.ParentDeclaration is TranslatedRecord parentRecord)
                { thisType = new PointerTypeReference(TranslatedTypeReference.Create(parentRecord)); }
                else
                {
                    thisType = VoidTypeReference.PointerInstance;
                    PrimaryTrampolineProblem(Severity.Warning, "Cannot generate primary trampoline: `this` type is unknown.");
                }

                Adapter thisPointer = new PassthroughAdapter(declaration, SpecialAdapterKind.ThisPointer, thisType);
                nativeAdapters.Add(thisPointer);
                AddFriendlyAdapter(thisPointer, new ThisPointerAdapter(thisPointer));
            }

            // Add return buffer after this pointer
            if (declaration.ReturnByReference && declaration.FunctionAbi.ReturnInfo.Flags.HasFlag(PathogenArgumentFlags.IsSRetAfterThis))
            { CreateNativeReturnByReferenceAdapter(); }
        }

        // We should have a native return adapter by this point
        Debug.Assert(nativeReturnAdapter is not null);

        // Handle explicit parameters
        foreach (TranslatedParameter parameter in declaration.Parameters)
        {
            // Handle pre-.NET 7 non-blittables
            if (TargetRuntime < TargetRuntime.Net7)
            {
                // Handle bool
                if (parameter.Type == CSharpBuiltinType.Bool)
                {
                    //TODO: Rewrite to NativeBoolean for virtual methods
                    Adapter nativeAdapter = new PassthroughAdapter(parameter, CSharpBuiltinType.Byte);
                    nativeAdapters.Add(nativeAdapter);
                    AddFriendlyAdapter(nativeAdapter, new BoolToByteAdapter(nativeAdapter));
                    continue;
                }

                // Handle char
                if (declaration.IsVirtual && parameter.Type == CSharpBuiltinType.Char)
                {
                    //TODO: Rewrite to NativeChar for virtual methods
                }
            }

            // Handle implicit pass by reference
            if (parameter.ImplicitlyPassedByReference)
            {
                Adapter nativeAdapter = new PassthroughAdapter(parameter, new PointerTypeReference(parameter.Type));
                nativeAdapters.Add(nativeAdapter);

                // Parameters which are written as being passed by value but are implicitly passed by reference will be adapted to behave the same.
                // Using byref here might seem tempting from a performance standpoint, but doing so would change semantics since the callee assumes it owns the buffer.
                // In theory if the native function receives a const byval we could use a readonly byref, but in partice C++ compilers don't do that even for PODs so we won't either.
                // (const byvals are weird and are not consdiered a good practice in C++ anyway.)
                AddFriendlyAdapter(nativeAdapter, new ToPointerAdapter(nativeAdapter));

                continue;
            }

            // Typical case
            {
                Adapter nativeAdapter = new PassthroughAdapter(parameter);
                nativeAdapters.Add(nativeAdapter);

                // Handle C++-style reference
                if (parameter.Type is PointerTypeReference { WasReference: true } referenceType)
                { AddFriendlyAdapter(nativeAdapter, new ByRefAdapter(nativeAdapter, referenceType.InnerIsConst ? ByRefKind.In : ByRefKind.Ref)); }
            }
        }

        // Create native trampoline
        bool haveFriendlyTrampoline = friendlyReturnAdapter is not null || friendlyAdapters is not null;
        Trampoline nativeTrampoline = new(declaration, nativeReturnAdapter, nativeAdapters.ToImmutable())
        {
            Name = haveFriendlyTrampoline ? $"{declaration.Name}_PInvoke" : declaration.Name,
            Accessibility = haveFriendlyTrampoline ? AccessModifier.Private : declaration.Accessibility
        };
        Trampoline primaryTrampoline;

        if (!haveFriendlyTrampoline)
        { primaryTrampoline = nativeTrampoline; }
        else
        {
            TrampolineBuilder friendlyBuilder = new(nativeTrampoline, useAsTemplate: false)
            {
                Name = declaration.Name,
                Description = "Friendly Overload",
                Accessibility = declaration.Accessibility
            };

            if (friendlyReturnAdapter is not null)
            { friendlyBuilder.AdaptReturnValue(friendlyReturnAdapter); }

            if (friendlyAdapters is not null)
            { friendlyBuilder.AdaptParametersDirect(friendlyAdapters); }

            primaryTrampoline = friendlyBuilder.Create();
        }

        // Add metadata and diagnostics to the function
        ImmutableArray<TranslationDiagnostic> diagnositcs = declaration.Diagnostics;
        if (friendlyTrampolineDiagnostics is not null)
        { diagnositcs.AddRange(friendlyTrampolineDiagnostics); }

        return declaration with
        {
            Metadata = declaration.Metadata.Add(new TrampolineCollection(nativeTrampoline, primaryTrampoline)),
            Diagnostics = diagnositcs
        };
    }
}
