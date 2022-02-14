using System;

namespace Biohazrd.CSharp;

public enum ByRefKind
{
    Ref,
    In,
    Out
}

public static class ByRefKindExtensions
{
    public static string GetKeyword(this ByRefKind kind)
        => kind switch
        {
            ByRefKind.Ref => "ref",
            ByRefKind.In => "in",
            ByRefKind.Out => "out",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
}
