using System.Diagnostics.CodeAnalysis;

// These xUnit analyzers trigger on xUnit internals
[assembly: SuppressMessage("Assertions", "xUnit2015:Do not use typeof expression to check the exception type", Scope = "namespaceanddescendants", Target = "Xunit")]
[assembly: SuppressMessage("Assertions", "xUnit2007:Do not use typeof expression to check the type", Scope = "namespaceanddescendants", Target = "Xunit")]
