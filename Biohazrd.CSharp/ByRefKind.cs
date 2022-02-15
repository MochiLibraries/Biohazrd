using System;

namespace Biohazrd.CSharp;

public enum ByRefKind
{
    Ref,
    // It might seem odd for In and RefReadOnly to be separate, but it greatly simplifies emitting ByRefTypeReference
    RefReadOnly,
    In,
    Out
}

public static class ByRefKindExtensions
{
    public static string GetKeyword(this ByRefKind kind)
        => kind switch
        {
            ByRefKind.Ref => "ref",
            ByRefKind.RefReadOnly => "ref readonly",
            ByRefKind.In => "in",
            ByRefKind.Out => "out",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
}
