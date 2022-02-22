using Biohazrd.CSharp.Trampolines;
using Biohazrd.Expressions;
using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using System.Linq;
using Xunit;

namespace Biohazrd.CSharp.Tests;

public sealed class CSharpTranslationVerifierTrampolineTests : BiohazrdTestBase
{
    private const string FunctionName = "MyFunction";
    private TranslatedLibrary MakeDefaultTestLibrary(bool createTrampolines = true)
    {
        TranslatedLibrary library = CreateLibrary
        (@"
class MyClass
{
public:
    int MyFunction(int a = 100, int b = 200);
};
"
        );

        library = new CSharpTypeReductionTransformation().Transform(library);

        if (createTrampolines)
        { library = new CreateTrampolinesTransformation().Transform(library); }

        return library;
    }

    [Fact]
    public void WarnOnMissingTrampolineCollection()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary(createTrampolines: false);
        Assert.Empty(library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics);
        library = new CSharpTranslationVerifier().Transform(library);
        Assert.Contains
        (
            library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Function does not have trampolines")
        );
    }

    [Fact]
    public void WarnOnDefaultedTrampolineCollection()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary(createTrampolines: false);
        Assert.Empty(library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics);
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) => d with { Metadata = d.Metadata.Add<TrampolineCollection>(default) }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);
        Assert.Contains
        (
            library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Function has trampolines but they were defaulted.")
        );
    }

    [Fact]
    public void NoDiagnosticsForDefaultTrampoline()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new CSharpTranslationVerifier().Transform(library);
        Assert.Empty(library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics);
    }

    [Fact]
    public void WarnOnNameChange()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) => d with { Name = "RennamedFunction" }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        Assert.Contains
        (
            library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>("RennamedFunction").Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Function's name was changed")
        );
    }

    [Fact]
    public void WarnOnReturnTypeChange()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) => d with { ReturnType = CSharpBuiltinType.UInt }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        Assert.Contains
        (
            library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Function's return type was changed")
        );
    }

    [Fact]
    public void WarnOnParametersRemoved()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) => d with { Parameters = d.Parameters.RemoveAt(0) }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        Assert.Contains
        (
            library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("The number of parameters changed")
        );
    }

    [Fact]
    public void WarnOnParametersAdded()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) => d with { Parameters = d.Parameters.Add(d.Parameters[0]) }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        Assert.Contains
        (
            library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("The number of parameters changed")
        );
    }

    [Fact]
    public void WarnOnParameterTypeChanged()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) => d with { Parameters = d.Parameters.SetItem(0, d.Parameters[0] with { Type = CSharpBuiltinType.UInt }) }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        Assert.Empty(library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics); // The diagnostic should be on the parameter
        Assert.Contains
        (
            library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Parameters[0].Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Parameter's type was changed")
        );
    }

    [Fact]
    public void WarnOnParameterNameChanged()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) => d with { Parameters = d.Parameters.SetItem(0, d.Parameters[0] with { Name = "newName" }) }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        Assert.Empty(library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics); // The diagnostic should be on the parameter
        Assert.Contains
        (
            library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Parameters[0].Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Parameter's name was changed")
        );
    }

    [Fact]
    public void WarnOnParameterDefaultValueChanged()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) => d with { Parameters = d.Parameters.SetItem(0, d.Parameters[0] with { DefaultValue = IntegerConstant.FromInt32(0xC0FFEE) }) }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        Assert.Empty(library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics); // The diagnostic should be on the parameter
        Assert.Contains
        (
            library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Parameters[0].Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Parameter's default value was changed")
        );
    }

    [Fact]
    public void NoDiagnosticsForCustomTrampoline()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) =>
            {
                TrampolineCollection trampolines = d.Metadata.Get<TrampolineCollection>();
                Trampoline primary = trampolines.PrimaryTrampoline;
                TrampolineBuilder builder = new(primary, useAsTemplate: false)
                {
                    Name = $"{primary.Name}_UInt",
                    Description = "Test Trampoline"
                };
                builder.AdaptReturnValue(new CastReturnAdapter(trampolines.NativeFunction.ReturnAdapter, CSharpBuiltinType.UInt, CastKind.Explicit));
                return d.WithSecondaryTrampoline(builder.Create());
            }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        Assert.Empty(library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics);
    }

    [Fact]
    public void NoDiagnosticsForCustomTemplateTrampoline()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) =>
            {
                TrampolineCollection trampolines = d.Metadata.Get<TrampolineCollection>();
                Trampoline primary = trampolines.PrimaryTrampoline;
                TrampolineBuilder builder = new(primary, useAsTemplate: true)
                {
                    Name = $"{primary.Name}_UInt",
                    Description = "Test Trampoline"
                };
                builder.AdaptReturnValue(new CastReturnAdapter(primary.ReturnAdapter, CSharpBuiltinType.UInt, CastKind.Explicit));
                return d.WithSecondaryTrampoline(builder.Create());
            }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        Assert.Empty(library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName).Diagnostics);
    }

    [Fact]
    public void BrokenTrampolineGraphPrimaryLeaf()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) =>
            {
                TrampolineCollection trampolines = d.Metadata.Get<TrampolineCollection>();
                Trampoline primary = trampolines.PrimaryTrampoline;
                TrampolineBuilder builder = new(primary, useAsTemplate: false)
                {
                    Name = $"{primary.Name}_UInt",
                    Description = "Test Trampoline"
                };
                builder.AdaptReturnValue(new CastReturnAdapter(trampolines.NativeFunction.ReturnAdapter, CSharpBuiltinType.UInt, CastKind.Explicit));
                return d with
                {
                    Metadata = d.Metadata.Set(trampolines with
                    {
                        // The new trampoline is referencing the trampoline which it is replacing
                        PrimaryTrampoline = builder.Create()
                    })
                };
            }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        TranslatedFunction function = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName);
        TrampolineCollection trampolineCollection = function.Metadata.Get<TrampolineCollection>();
        Assert.ReferenceEqual(trampolineCollection.NativeFunction, trampolineCollection.PrimaryTrampoline); // The broken trampoline should have been removed
        Assert.Empty(trampolineCollection.SecondaryTrampolines);
        Assert.Contains
        (
            function.Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Trampoline 'Test Trampoline' referenced missing trampoline")
        );
    }

    [Fact]
    public void BrokenTrampolineGraphSecondaryLeaf()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) =>
            {
                TrampolineCollection trampolines = d.Metadata.Get<TrampolineCollection>();
                Trampoline primary = trampolines.PrimaryTrampoline;
                TrampolineBuilder builder = new(primary, useAsTemplate: false)
                {
                    Name = $"{primary.Name}_UInt",
                    Description = "Test Trampoline"
                };
                builder.AdaptReturnValue(new CastReturnAdapter(trampolines.NativeFunction.ReturnAdapter, CSharpBuiltinType.UInt, CastKind.Explicit));
                return d with
                {
                    Metadata = d.Metadata.Set(trampolines with
                    {
                        // Remove the primary trampoline
                        PrimaryTrampoline = trampolines.NativeFunction,

                        // Add the new trampoline we just created
                        SecondaryTrampolines = trampolines.SecondaryTrampolines.Add(builder.Create())
                    })
                };
            }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        TranslatedFunction function = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName);
        TrampolineCollection trampolineCollection = function.Metadata.Get<TrampolineCollection>();
        Assert.Empty(trampolineCollection.SecondaryTrampolines); // The broken trampoline should have been removed
        Assert.ReferenceEqual(trampolineCollection.NativeFunction, trampolineCollection.PrimaryTrampoline);
        Assert.Contains
        (
            function.Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Trampoline 'Test Trampoline' referenced missing trampoline")
        );
    }

    [Fact]
    public void BrokenTrampolineGraphIndirect()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) =>
            {
                TrampolineCollection trampolines = d.Metadata.Get<TrampolineCollection>();
                Trampoline primary = trampolines.PrimaryTrampoline;

                TrampolineBuilder builder1 = new(primary, useAsTemplate: false)
                {
                    Name = $"{primary.Name}_UInt",
                    Description = "Test Trampoline"
                };
                builder1.AdaptReturnValue(new CastReturnAdapter(trampolines.NativeFunction.ReturnAdapter, CSharpBuiltinType.UInt, CastKind.Explicit));
                Trampoline trampoline1 = builder1.Create();

                TrampolineBuilder builder2 = new(trampoline1, useAsTemplate: false)
                {
                    Name = $"{primary.Name}_ULong",
                    Description = "Test Trampoline 2"
                };
                builder2.AdaptReturnValue(new CastReturnAdapter(trampolines.NativeFunction.ReturnAdapter, CSharpBuiltinType.ULong, CastKind.Explicit));
                Trampoline trampoline2 = builder2.Create();

                return d with
                {
                    Metadata = d.Metadata.Set(trampolines with
                    {
                        // Remove the primary trampoline
                        PrimaryTrampoline = trampolines.NativeFunction,

                        // Add the new trampolines we just created
                        SecondaryTrampolines = trampolines.SecondaryTrampolines.Add(trampoline1).Add(trampoline2)
                    })
                };
            }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        TranslatedFunction function = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName);
        TrampolineCollection trampolineCollection = function.Metadata.Get<TrampolineCollection>();
        Assert.Empty(trampolineCollection.SecondaryTrampolines); // The broken trampolines should have been removed
        Assert.ReferenceEqual(trampolineCollection.NativeFunction, trampolineCollection.PrimaryTrampoline);
        Assert.Contains
        (
            function.Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Trampoline 'Test Trampoline' referenced missing trampoline")
        );
        Assert.Contains
        (
            function.Diagnostics,
            // Trampoline 2 referenced Trampoline 1 which was removed, so it should've been removed too
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Trampoline 'Test Trampoline 2' referenced missing trampoline")
        );
    }

    [Fact]
    public void BrokenTrampolineGraphMiddleMissing()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) =>
            {
                TrampolineCollection trampolines = d.Metadata.Get<TrampolineCollection>();
                Trampoline primary = trampolines.PrimaryTrampoline;

                TrampolineBuilder builder1 = new(primary, useAsTemplate: false)
                {
                    Name = $"{primary.Name}_UInt",
                    Description = "Test Trampoline"
                };
                builder1.AdaptReturnValue(new CastReturnAdapter(trampolines.NativeFunction.ReturnAdapter, CSharpBuiltinType.UInt, CastKind.Explicit));
                Trampoline trampoline1 = builder1.Create();

                TrampolineBuilder builder2 = new(trampoline1, useAsTemplate: false)
                {
                    Name = $"{primary.Name}_ULong",
                    Description = "Test Trampoline 2"
                };
                builder2.AdaptReturnValue(new CastReturnAdapter(trampolines.NativeFunction.ReturnAdapter, CSharpBuiltinType.ULong, CastKind.Explicit));
                Trampoline trampoline2 = builder2.Create();

                return d with
                {
                    Metadata = d.Metadata.Set(trampolines with
                    {
                        // Remove the primary trampoline
                        PrimaryTrampoline = trampolines.NativeFunction,

                        // Add the new trampoline we just created (just the second though, leave out the first.)
                        SecondaryTrampolines = trampolines.SecondaryTrampolines.Add(trampoline2)
                    })
                };
            }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        TranslatedFunction function = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName);
        TrampolineCollection trampolineCollection = function.Metadata.Get<TrampolineCollection>();
        Assert.Empty(trampolineCollection.SecondaryTrampolines); // The broken trampolines should have been removed
        Assert.ReferenceEqual(trampolineCollection.NativeFunction, trampolineCollection.PrimaryTrampoline);
        Assert.Contains
        (
            function.Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Trampoline 'Test Trampoline 2' referenced missing trampoline")
        );
    }

    [Fact]
    public void RemovingTemplateDoesNotBreakGraph()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) =>
            {
                TrampolineCollection trampolines = d.Metadata.Get<TrampolineCollection>();
                Trampoline primary = trampolines.PrimaryTrampoline;
                TrampolineBuilder builder = new(primary, useAsTemplate: true)
                {
                    Name = $"{primary.Name}_UInt",
                    Description = "Test Trampoline"
                };
                builder.AdaptReturnValue(new CastReturnAdapter(trampolines.NativeFunction.ReturnAdapter, CSharpBuiltinType.UInt, CastKind.Explicit));
                return d with
                {
                    Metadata = d.Metadata.Set(trampolines with
                    {
                        // The new trampoline is referencing the trampoline which it is replacing
                        PrimaryTrampoline = builder.Create()
                    })
                };
            }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        TranslatedFunction function = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName);
        TrampolineCollection trampolineCollection = function.Metadata.Get<TrampolineCollection>();
        Assert.Equal("Test Trampoline", trampolineCollection.PrimaryTrampoline.Description);
        Assert.Empty(trampolineCollection.SecondaryTrampolines);
        Assert.Empty(function.Diagnostics);
    }

    [Fact]
    public void SecondaryRedundantToPrimaryIsRemoved()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) =>
            {
                TrampolineCollection trampolines = d.Metadata.Get<TrampolineCollection>();
                return d with
                {
                    Metadata = d.Metadata.Set(trampolines.WithTrampoline(trampolines.PrimaryTrampoline))
                };
            }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        TranslatedFunction function = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName);
        TrampolineCollection trampolineCollection = function.Metadata.Get<TrampolineCollection>();
        Assert.Empty(trampolineCollection.SecondaryTrampolines);
        Assert.Contains
        (
            function.Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith($"Trampoline '{trampolineCollection.PrimaryTrampoline.Description}' was added as both a secondary and the primary trampoline.")
        );
    }

    [Fact]
    public void MultipleSecondariesRedundantToPrimaryAreRemoved()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) =>
            {
                TrampolineCollection trampolines = d.Metadata.Get<TrampolineCollection>();
                return d with
                {
                    Metadata = d.Metadata.Set(trampolines.WithTrampoline(trampolines.PrimaryTrampoline).WithTrampoline(trampolines.PrimaryTrampoline))
                };
            }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        TranslatedFunction function = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName);
        TrampolineCollection trampolineCollection = function.Metadata.Get<TrampolineCollection>();
        Assert.Empty(trampolineCollection.SecondaryTrampolines);
        int count = function.Diagnostics.Count(d => d.Severity is Severity.Warning && d.Message.StartsWith($"Trampoline '{trampolineCollection.PrimaryTrampoline.Description}' was added as both a secondary and the primary trampoline."));
        Assert.Equal(2, count);
    }

    [Fact]
    public void SecondaryRedundantToSecondaryIsRemoved()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) =>
            {
                TrampolineCollection trampolines = d.Metadata.Get<TrampolineCollection>();
                Trampoline primary = trampolines.PrimaryTrampoline;

                TrampolineBuilder builder1 = new(primary, useAsTemplate: true)
                {
                    Name = $"{primary.Name}_UInt",
                    Description = "Test Trampoline"
                };
                builder1.AdaptReturnValue(new CastReturnAdapter(trampolines.NativeFunction.ReturnAdapter, CSharpBuiltinType.UInt, CastKind.Explicit));
                Trampoline trampoline1 = builder1.Create();

                TrampolineBuilder builder2 = new(primary, useAsTemplate: true)
                {
                    Name = $"{primary.Name}_ULong",
                    Description = "Test Trampoline 2"
                };
                builder2.AdaptReturnValue(new CastReturnAdapter(trampolines.NativeFunction.ReturnAdapter, CSharpBuiltinType.ULong, CastKind.Explicit));
                Trampoline trampoline2 = builder2.Create();

                return d with
                {
                    Metadata = d.Metadata.Set(trampolines with
                    {
                        // Remove the primary trampoline
                        PrimaryTrampoline = trampolines.NativeFunction,

                        // Add the new trampoline we just created (adding an extra trampoline1)
                        SecondaryTrampolines = trampolines.SecondaryTrampolines.Add(trampoline1).Add(trampoline2).Add(trampoline1)
                    })
                };
            }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        TranslatedFunction function = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName);
        TrampolineCollection trampolineCollection = function.Metadata.Get<TrampolineCollection>();
        Assert.Single(trampolineCollection.SecondaryTrampolines, t => t.Description == "Test Trampoline");
        Assert.Single(trampolineCollection.SecondaryTrampolines, t => t.Description == "Test Trampoline 2");
        Assert.Contains
        (
            function.Diagnostics,
            d => d.Severity is Severity.Warning && d.Message.StartsWith("Trampoline 'Test Trampoline' was added to the secondary trampoline list more than once.")
        );
    }

    [Fact]
    public void MultipleSecondariesRedundantToSecondaryAreRemoved()
    {
        TranslatedLibrary library = MakeDefaultTestLibrary();
        library = new SimpleTransformation()
        {
            TransformFunction = (c, d) =>
            {
                TrampolineCollection trampolines = d.Metadata.Get<TrampolineCollection>();
                Trampoline primary = trampolines.PrimaryTrampoline;

                TrampolineBuilder builder1 = new(primary, useAsTemplate: true)
                {
                    Name = $"{primary.Name}_UInt",
                    Description = "Test Trampoline"
                };
                builder1.AdaptReturnValue(new CastReturnAdapter(trampolines.NativeFunction.ReturnAdapter, CSharpBuiltinType.UInt, CastKind.Explicit));
                Trampoline trampoline1 = builder1.Create();

                TrampolineBuilder builder2 = new(primary, useAsTemplate: true)
                {
                    Name = $"{primary.Name}_ULong",
                    Description = "Test Trampoline 2"
                };
                builder2.AdaptReturnValue(new CastReturnAdapter(trampolines.NativeFunction.ReturnAdapter, CSharpBuiltinType.ULong, CastKind.Explicit));
                Trampoline trampoline2 = builder2.Create();

                return d with
                {
                    Metadata = d.Metadata.Set(trampolines with
                    {
                        // Remove the primary trampoline
                        PrimaryTrampoline = trampolines.NativeFunction,

                        // Add the new trampoline we just created (adding an extra trampoline1)
                        SecondaryTrampolines = trampolines.SecondaryTrampolines.Add(trampoline1).Add(trampoline1).Add(trampoline2).Add(trampoline1)
                    })
                };
            }
        }.Transform(library);
        library = new CSharpTranslationVerifier().Transform(library);

        TranslatedFunction function = library.FindDeclaration<TranslatedRecord>().FindDeclaration<TranslatedFunction>(FunctionName);
        TrampolineCollection trampolineCollection = function.Metadata.Get<TrampolineCollection>();
        Assert.Single(trampolineCollection.SecondaryTrampolines, t => t.Description == "Test Trampoline");
        Assert.Single(trampolineCollection.SecondaryTrampolines, t => t.Description == "Test Trampoline 2");
        int count = function.Diagnostics.Count(d => d.Severity is Severity.Warning && d.Message.StartsWith("Trampoline 'Test Trampoline' was added to the secondary trampoline list more than once."));
        Assert.Equal(2, count);
    }
}
