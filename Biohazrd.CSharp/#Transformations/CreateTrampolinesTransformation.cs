using Biohazrd.CSharp.Metadata;
using Biohazrd.CSharp.Trampolines;
using Biohazrd.Transformation;
using ClangSharp.Pathogen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Biohazrd.CSharp;

public sealed class CreateTrampolinesTransformation : CSharpTransformationBase
{
    public TargetRuntime TargetRuntime { get; init; } = CSharpGenerationOptions.Default.TargetRuntime; //TODO: This ideally should come from some central context to ensure consistency

    /// <summary>Enables or disables emitting C++ reference returns as C# reference returns.</summary>
    /// <remarks>
    /// If enabled, functions such as <c>int& Hello();</c> will be emitted as <c>ref int Hello();</c>. If disabled (the default), the function will be emitted as <c>int* Hello();</c>
    ///
    /// It is recommended you only enable this only if it makes a lot of sense for your particular library.
    /// C# developers are not typically used to dealing with ref returns and are likely to mistakenly dereference them unintentionally.
    /// Additionally, passing them to other translated methods as pointer parameters is much more cumbersome than the equivalent C++ code.
    ///
    /// Consider the following C++ API:
    /// ```cpp
    /// int& Hello();
    /// Increment(int* x);
    /// ```
    ///
    /// In C++ you might use these functions together like so:
    /// ```cpp
    /// int& x = Hello();
    /// Increment(&x);
    /// ```
    ///
    /// With this property enabled, usage from C# looks like this:
    /// ```csharp
    /// ref int x = ref Hello();
    /// fixed (int* xP = &x)
    /// { Increment(xP); }
    /// ```
    ///
    /// Or if the developer is familiar with <see cref="System.Runtime.CompilerServices.Unsafe"/>:
    /// ```csharp
    /// ref int x = ref Hello();
    /// Increment((int*)Unsafe.AsPointer(ref x));
    /// ```
    ///
    /// Both are quite a bit more clunky than the C++ equivalent. Additionally, C#-style refs cannot be stored in fields (unlike with C++) without odd hacks.
    /// </remarks>
    public bool EmitCppReferenceReturnsAsCSharpReferenceReturns { get; init; } = false;

    protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
    {
        // Don't try to add trampolines to a function which already has them
        if (declaration.Metadata.Has<TrampolineCollection>())
        {
            Debug.Fail("This should be the only transformation which adds trampoline collections.");
            return declaration;
        }

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
        List<SyntheticAdapter>? friendlySyntheticAdapters = null;

        void AddFriendlyAdapter(Adapter target, Adapter adapter)
        {
            friendlyAdapters ??= new Dictionary<Adapter, Adapter>(nativeAdapters.Count);
            friendlyAdapters.Add(target, adapter);
        }

        void AddFriendlySyntheticAdapter(SyntheticAdapter adapter)
        {
            friendlySyntheticAdapters ??= new List<SyntheticAdapter>();
            friendlySyntheticAdapters.Add(adapter);
        }

        // Handle return type when not returning by reference
        if (!declaration.ReturnByReference)
        {
            TypeReference returnType = declaration.ReturnType;

            // Handle returning bool
            if (TargetRuntime < TargetRuntime.Net7 && returnType.IsCSharpType(context.Library, CSharpBuiltinType.Bool))
            {
                // If the function is virtual, use NativeBoolean on the native side and allow the friendly side to just be passthrough
                if (declaration.IsVirtual)
                { nativeReturnAdapter = NonBlittableTypeReturnAdapter.NativeBoolean; }
                else
                {
                    nativeReturnAdapter = new PassthroughReturnAdapter(CSharpBuiltinType.Byte);
                    friendlyReturnAdapter = new CastReturnAdapter(nativeReturnAdapter, CSharpBuiltinType.Bool, CastKind.UnsafeAs);
                }
            }
            // Handle virtual methods returning bool
            else if (declaration.IsVirtual && TargetRuntime < TargetRuntime.Net7 && returnType.IsCSharpType(context.Library, CSharpBuiltinType.Char))
            { nativeReturnAdapter = NonBlittableTypeReturnAdapter.NativeChar; }
            // Handle returning void
            else if (returnType is VoidTypeReference)
            { nativeReturnAdapter = VoidReturnAdapter.Instance; }
            // Handle typical return
            else
            {
                nativeReturnAdapter = new PassthroughReturnAdapter(returnType);

                // Handle C++-style reference
                if (EmitCppReferenceReturnsAsCSharpReferenceReturns && returnType is PointerTypeReference { WasReference: true } referenceType)
                { friendlyReturnAdapter = new ByRefReturnAdapter(nativeReturnAdapter, referenceType.InnerIsConst ? ByRefKind.RefReadOnly : ByRefKind.Ref); }
            }
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
                ReturnByImplicitBufferAdapter returnByReferenceAdapter = new(returnBufferParameter);
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

            // Handle pre-.NET 7 non-blittables
            if (TargetRuntime < TargetRuntime.Net7)
            {
                // Handle bool
                if (parameter.Type.IsCSharpType(context.Library, CSharpBuiltinType.Bool))
                {
                    // If the function is virtual, use NativeBoolean on the native side and allow the friendly side to just be passthrough
                    if (declaration.IsVirtual)
                    { nativeAdapters.Add(new NonBlittableTypeAdapter(parameter, NonBlittableTypeKind.NativeBoolean)); }
                    else
                    {
                        Adapter nativeAdapter = new PassthroughAdapter(parameter, CSharpBuiltinType.Byte);
                        nativeAdapters.Add(nativeAdapter);
                        AddFriendlyAdapter(nativeAdapter, new CastAdapter(nativeAdapter, CSharpBuiltinType.Bool, CastKind.UnsafeAs));
                    }
                    continue;
                }

                // Handle char -- No friendly adapter needed, it can just be passthrough
                if (declaration.IsVirtual && parameter.Type.IsCSharpType(context.Library, CSharpBuiltinType.Char))
                {
                    nativeAdapters.Add(new NonBlittableTypeAdapter(parameter, NonBlittableTypeKind.NativeChar));
                    continue;
                }
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

        // Determine if SetLastError logic is needed
        bool useLegacySetLastError = false;
        if (declaration.Metadata.TryGet(out SetLastErrorFunction setLastErrorMetadata))
        {
            // Prior to .NET 6 we have to use the legacy SetLastError logic
            if (TargetRuntime < TargetRuntime.Net6)
            {
                useLegacySetLastError = true;

                if (setLastErrorMetadata.SkipDefensiveClear)
                { PrimaryTrampolineProblem(Severity.Warning, $"{nameof(setLastErrorMetadata.SkipDefensiveClear)} is not available when targeting {TargetRuntime}."); }
            }
            else
            { AddFriendlySyntheticAdapter(new SetLastSystemErrorAdapter(setLastErrorMetadata.SkipDefensiveClear)); }
        }

        // Create native trampoline
        bool haveFriendlyTrampoline = friendlyReturnAdapter is not null || friendlyAdapters is not null || friendlySyntheticAdapters is not null;
        Trampoline nativeTrampoline = new(declaration, nativeReturnAdapter, nativeAdapters.ToImmutable())
        {
            Name = haveFriendlyTrampoline ? $"{declaration.Name}_PInvoke" : declaration.Name,
            Accessibility = haveFriendlyTrampoline ? AccessModifier.Private : declaration.Accessibility,
            UseLegacySetLastError = useLegacySetLastError,
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

            if (friendlySyntheticAdapters is not null)
            { friendlyBuilder.AddSyntheticAdaptersDirect(friendlySyntheticAdapters); }

            primaryTrampoline = friendlyBuilder.Create();
        }

        // Add metadata and diagnostics to the function
        ImmutableArray<TranslationDiagnostic> diagnositcs = declaration.Diagnostics;
        if (friendlyTrampolineDiagnostics is not null)
        { diagnositcs.AddRange(friendlyTrampolineDiagnostics); }

        return declaration with
        {
            Metadata = declaration.Metadata.Add(new TrampolineCollection(declaration, nativeTrampoline, primaryTrampoline)),
            Diagnostics = diagnositcs
        };
    }
}
