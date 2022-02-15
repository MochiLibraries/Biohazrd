using System;

namespace Biohazrd.CSharp;

public enum ByRefKind
{
    Ref,
    In,
    RefReadOnly = In,
    Out
}

public static class ByRefKindExtensions
{
    public static string GetKeywordForParameter(this ByRefKind kind)
        => kind switch
        {
            ByRefKind.Ref => "ref",
            ByRefKind.In => "in",
            ByRefKind.Out => "out",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    public static string GetKeywordForReturn(this ByRefKind kind)
        => kind switch
        {
            ByRefKind.Ref => "ref",
            ByRefKind.RefReadOnly => "ref readonly",
            ByRefKind.Out => throw new ArgumentException("`out` byref is not valid in this context.", nameof(kind)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };

    public static string GetKeywordForLocal(this ByRefKind kind)
        => kind.GetKeywordForReturn();
}
