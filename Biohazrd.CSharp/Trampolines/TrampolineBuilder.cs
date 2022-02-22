using System;
using System.Collections.Generic;

namespace Biohazrd.CSharp.Trampolines;

public struct TrampolineBuilder
{
    public Trampoline Target { get; }
    internal readonly bool TargetIsTemplate;
    public AccessModifier Accessibility { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    internal IReturnAdapter? ReturnAdapter { get; private set; }
    internal Dictionary<Adapter, Adapter>? Adapters { get; private set; }
    internal List<SyntheticAdapter>? SyntheticAdapters { get; private set; }

    /// <summary>Returns true if this builder has adapters.</summary>
    /// <remarks>If this struct is defaulted this will return false.</remarks>
    public bool HasAdapters => ReturnAdapter is not null || Adapters is not null || SyntheticAdapters is not null;

    public TrampolineBuilder(Trampoline target, bool useAsTemplate)
    {
        //TODO: Automatically drop the template designation or?
        //if (useAsTemplate && target.IsNativeFunction)
        //{ throw new ArgumentException("The native function dummy trampoline cannot be used as a template.", nameof(useAsTemplate)); }
        if (target.IsNativeFunction)
        { useAsTemplate = false; }

        Target = target;
        TargetIsTemplate = useAsTemplate;
        Accessibility = target.Accessibility;
        Name = target.Name;
        Description = "Unnamed Trampoline";
        ReturnAdapter = null;
        Adapters = null;
        SyntheticAdapters = null;
    }

    public void AdaptReturnValue(IReturnAdapter adapter)
    {
        if (ReturnAdapter is not null)
        { throw new InvalidOperationException("The return adapter has already been specified for this trampoline."); }

        ReturnAdapter = adapter;
    }

    public void AdaptParameter(Adapter target, Adapter adapter)
    {
        if (!Target.Adapters.Contains(target))
        { throw new InvalidOperationException("The specified parameter is not part of the target trampoline."); }

        if (Adapters is null)
        { Adapters = new Dictionary<Adapter, Adapter>(Target.Adapters.Length, ReferenceEqualityComparer.Instance); }

        if (!Adapters.TryAdd(target, adapter))
        { throw new InvalidOperationException("The specified parameter has already been adapted for this trampoline."); }
    }

    public bool TryAdaptParameter(TranslatedParameter parameter, Adapter adapter)
    {
        Adapter? target = null;
        foreach (Adapter targetAdapter in Target.Adapters)
        {
            if (targetAdapter.CorrespondsTo(parameter))
            {
                target = targetAdapter;
                break;
            }
        }

        if (target is null)
        { return false; }

        AdaptParameter(target, adapter);
        return true;
    }

    //TODO: This overload is not particularly useful because you need the target adapter to make most adapters
    public void AdaptParameter(TranslatedParameter parameter, Adapter adapter)
    {
        if (!TryAdaptParameter(parameter, adapter))
        { throw new InvalidOperationException($"'{parameter}' is not part of the target trampoline."); }
    }

    public bool TryAdaptParameter(SpecialAdapterKind specialParameter, Adapter adapter)
    {
        if (specialParameter == SpecialAdapterKind.None || !Enum.IsDefined(specialParameter))
        { throw new ArgumentOutOfRangeException(nameof(specialParameter)); }

        Adapter? target = null;
        foreach (Adapter targetAdapter in Target.Adapters)
        {
            if (targetAdapter.SpecialKind == specialParameter)
            {
                target = targetAdapter;
                break;
            }
        }

        if (target is null)
        { return false; }

        AdaptParameter(target, adapter);
        return true;
    }

    public void AdaptParameter(SpecialAdapterKind specialParameter, Adapter adapter)
    {
        if (!TryAdaptParameter(specialParameter, adapter))
        { throw new InvalidOperationException($"The target trampoline does not contain a {specialParameter} parameter."); }
    }

    //TODO: Add removing and replacing synthetic adapters
    // Removing should add a Adapter => null kvp to the adapters list
    // Replacing works as normal but should be separtate for API clarity
    public void AddSyntheticAdapter(SyntheticAdapter adapter)
    {
        if (SyntheticAdapters is null)
        { SyntheticAdapters = new List<SyntheticAdapter>(); }

        SyntheticAdapters.Add(adapter);
    }

    // This method only exists as an optimization for CreateTrampolinesTransformation so that it can build the friendly trampoline before the native trampoline is complete.
    internal void AdaptParametersDirect(Dictionary<Adapter, Adapter> adapters)
    {
        if (Adapters is not null)
        { throw new InvalidOperationException("This builder has already received adapters."); }

#if DEBUG
        foreach ((Adapter target, Adapter adapter) in adapters)
        { AdaptParameter(target, adapter); }
#else
        Adapters = adapters;
#endif
    }

    internal void AddSyntheticAdaptersDirect(List<SyntheticAdapter> adapters)
    {
        if (SyntheticAdapters is not null)
        { throw new InvalidOperationException("This builder has already received synthetic adapters."); }

#if DEBUG
        foreach (SyntheticAdapter adapter in adapters)
        { AddSyntheticAdapter(adapter); }
#else
        SyntheticAdapters = adapters;
#endif
    }

    public Trampoline Create()
    {
        if (Target is null)
        { throw new InvalidOperationException("Triend to create a trampoline from a defaulted builder!"); }

        // Changing the name isn't *really* an adaption, but it will create something sane when emitted so let's allow it.
        if (!HasAdapters && Name == Target.Name)
        { throw new InvalidOperationException("Tried to create a trampoline with nothing adapted!"); }

        return new Trampoline(this);
    }
}
