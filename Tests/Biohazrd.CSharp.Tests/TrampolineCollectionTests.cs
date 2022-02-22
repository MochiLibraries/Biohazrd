using Biohazrd.CSharp.Trampolines;
using Biohazrd.Tests.Common;
using System;
using System.Collections.Generic;
using Xunit;

namespace Biohazrd.CSharp.Tests;

public sealed class TrampolineCollectionTests : BiohazrdTestBase
{
    private TrampolineCollection CreateCollection()
        => CreateCollection(out _, out _, out _);

    private TrampolineCollection CreateCollection(out TrampolineCollection otherCollection)
        => CreateCollection(out _, out otherCollection, out _);

    private TrampolineCollection CreateCollection(out TranslatedFunction function)
        => CreateCollection(out function, out _, out _);

    private TrampolineCollection CreateCollection(out TranslatedFunction function, out TrampolineCollection otherCollection, out TranslatedFunction otherFunction)
    {
        TranslatedLibrary library = CreateLibrary
        (@"
int MyFunction(const int& a = 100, const int& b = 200); // MyFunction will always have a default primary trampoline
int OtherFunction(int a = 100, int b = 200); // OtherFunction will never have a default primary trampoline
"
        );

        library = new CSharpTypeReductionTransformation().Transform(library);
        library = new CreateTrampolinesTransformation().Transform(library);

        // Get and sanity check the main function
        TrampolineCollection collection;
        {
            function = library.FindDeclaration<TranslatedFunction>("MyFunction");
            collection = function.Metadata.Get<TrampolineCollection>();

            Assert.NotNull(collection.NativeFunction);
            Assert.True(collection.NativeFunction.IsNativeFunction);
            Assert.Equal(function.Id, collection.NativeFunction.TargetFunctionId);

            Assert.NotNull(collection.PrimaryTrampoline);
            Assert.False(collection.PrimaryTrampoline.IsNativeFunction);
            Assert.ReferenceEqual(collection.NativeFunction, collection.PrimaryTrampoline.Target);
            Assert.Equal(collection.NativeFunction.TargetFunctionId, collection.PrimaryTrampoline.TargetFunctionId);

            Assert.Empty(collection.SecondaryTrampolines);
        }

        // Get and sanity check the other function
        {
            otherFunction = library.FindDeclaration<TranslatedFunction>("OtherFunction");
            otherCollection = otherFunction.Metadata.Get<TrampolineCollection>();

            Assert.NotNull(otherCollection.NativeFunction);
            Assert.True(otherCollection.NativeFunction.IsNativeFunction);
            Assert.Equal(otherFunction.Id, otherCollection.NativeFunction.TargetFunctionId);

            // The other function does not require a primary trampoline so the native dummy trampoline should be the primary
            Assert.NotNull(otherCollection.PrimaryTrampoline);
            Assert.ReferenceEqual(otherCollection.NativeFunction, otherCollection.PrimaryTrampoline);
            Assert.Equal(otherCollection.NativeFunction.TargetFunctionId, otherCollection.PrimaryTrampoline.TargetFunctionId);

            Assert.Empty(otherCollection.SecondaryTrampolines);
        }

        return collection;
    }

    [Fact]
    public void NativeFunctionCanBeReplaced()
    {
        TrampolineCollection collection = CreateCollection();
        collection = collection with
        {
            NativeFunction = collection.NativeFunction with { Name = "NewName" }
        };
        Assert.Equal("NewName", collection.NativeFunction.Name);
        Assert.True(collection.NativeFunction.IsNativeFunction);
    }

    [Fact]
    public void NativeFunctionCannotBeSetToNonNative()
    {
        TrampolineCollection collection = CreateCollection();
        Assert.Throws<ArgumentException>
        (
            () => collection = collection with { NativeFunction = collection.PrimaryTrampoline }
        );
    }

    [Fact]
    public void NativeFunctionCannotBeUnrelated()
    {
        TrampolineCollection collection = CreateCollection(out TrampolineCollection otherCollection);
        Assert.Throws<ArgumentException>
        (
            () => collection = collection with { NativeFunction = otherCollection.NativeFunction }
        );
    }

    [Fact]
    public void NativeFunctionCannotBeNull()
    {
        TrampolineCollection collection = CreateCollection(out TrampolineCollection otherCollection);
        Assert.ThrowsAny<Exception>
        (
            () => collection = collection with { NativeFunction = null! }
        );
    }

    [Fact]
    public void PrimaryTrampolineCanBeReplaced()
    {
        TrampolineCollection collection = CreateCollection();
        collection = collection with
        {
            PrimaryTrampoline = collection.PrimaryTrampoline with { Name = "NewName" }
        };
        Assert.Equal("NewName", collection.PrimaryTrampoline.Name);
    }

    [Fact]
    public void PrimaryTrampolineCannotBeUnrelated()
    {
        TrampolineCollection collection = CreateCollection(out TrampolineCollection otherCollection);
        Assert.Throws<ArgumentException>
        (
            () => collection = collection with { PrimaryTrampoline = otherCollection.PrimaryTrampoline }
        );
    }

    [Fact]
    public void PrimaryTrampolineCanBeNative()
    {
        TrampolineCollection collection = CreateCollection();
        collection = collection with
        {
            PrimaryTrampoline = collection.NativeFunction
        };
        Assert.True(collection.PrimaryTrampoline.IsNativeFunction);
    }

    [Fact]
    public void PrimaryTrampolineCannotBeNull()
    {
        TrampolineCollection collection = CreateCollection(out TrampolineCollection otherCollection);
        Assert.ThrowsAny<Exception>
        (
            () => collection = collection with { PrimaryTrampoline = null! }
        );
    }

    [Fact]
    public void PrimaryTrampolineDoesNotVerifyGraph()
    {
        TrampolineCollection collection = CreateCollection(out TrampolineCollection otherCollection);
        TrampolineBuilder builder = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName"
        };

        // This isn't a valid collection, but the enforcement of the graph completedness is handled by the verification stage
        collection = collection with { PrimaryTrampoline = builder.Create() };
        Assert.Equal("AlternateName", collection.PrimaryTrampoline.Name);
        Assert.NotNull(collection.PrimaryTrampoline.Target);
        Assert.False(collection.Contains(collection.PrimaryTrampoline.Target));
    }

    [Fact]
    public void SecondaryTrampolinesCanBeReplaced()
    {
        TrampolineCollection collection = CreateCollection();
        TrampolineBuilder builder = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName"
        };
        Trampoline secondary = builder.Create();

        collection = collection with { SecondaryTrampolines = collection.SecondaryTrampolines.Add(secondary) };
        Assert.Contains(secondary, collection.SecondaryTrampolines);
    }

    [Fact]
    public void SecondaryTrampolinesCanBeReplacedUsingHelper()
    {
        TrampolineCollection collection = CreateCollection();
        TrampolineBuilder builder = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName"
        };
        Trampoline secondary = builder.Create();

        collection = collection.WithTrampoline(secondary);
        Assert.Contains(secondary, collection.SecondaryTrampolines);
    }

    [Fact]
    public void SecondaryTrampolinesCannotBeUnrelated()
    {
        TrampolineCollection collection = CreateCollection(out TrampolineCollection otherCollection);
        TrampolineBuilder builder = new(otherCollection.NativeFunction, useAsTemplate: false) { Name = "AlternateName" };
        Trampoline trampoline = builder.Create();
        Assert.False(trampoline.IsNativeFunction);

        Exception ex = Assert.Throws<ArgumentException>
        (
            () => collection = collection with { SecondaryTrampolines = collection.SecondaryTrampolines.Add(trampoline) }
        );
        Assert.Contains($"'{trampoline}' does not belong", ex.Message);
    }

    [Fact]
    public void SecondaryTrampolinesCannotBeUnrelatedUsingHelper()
    {
        TrampolineCollection collection = CreateCollection(out TrampolineCollection otherCollection);
        TrampolineBuilder builder = new(otherCollection.NativeFunction, useAsTemplate: false) { Name = "AlternateName" };
        Trampoline trampoline = builder.Create();
        Assert.False(trampoline.IsNativeFunction);

        Exception ex = Assert.Throws<ArgumentException>
        (
            () => collection = collection.WithTrampoline(trampoline)
        );
        Assert.Contains($"does not belong", ex.Message);
    }

    [Fact]
    public void SecondaryTrampolinesCannotBeNative()
    {
        TrampolineCollection collection = CreateCollection();
        Exception ex = Assert.Throws<ArgumentException>
        (
            () => collection = collection with { SecondaryTrampolines = collection.SecondaryTrampolines.Add(collection.NativeFunction) }
        );
        Assert.Contains("Native functions cannot be trampolines.", ex.Message);
    }

    [Fact]
    public void SecondaryTrampolinesCannotBeNativeUsingHelper()
    {
        TrampolineCollection collection = CreateCollection();
        Exception ex = Assert.Throws<ArgumentException>
        (
            () => collection = collection.WithTrampoline(collection.NativeFunction)
        );
        Assert.StartsWith("Native trampolines", ex.Message);
    }

    [Fact]
    public void SecondaryTrampolinesCannotBeNull()
    {
        TrampolineCollection collection = CreateCollection();
        Assert.ThrowsAny<Exception>
        (
            () => collection = collection with { SecondaryTrampolines = collection.SecondaryTrampolines.Add(null!) }
        );
    }

    [Fact]
    public void SecondaryTrampolinesCannotBeNullUsingHelper()
    {
        TrampolineCollection collection = CreateCollection();
        Assert.ThrowsAny<Exception>
        (
            () => collection = collection.WithTrampoline(null!)
        );
    }

    [Fact]
    public void SecondaryTrampolinesDoesNotVerifyGraph()
    {
        TrampolineCollection collection = CreateCollection(out TrampolineCollection otherCollection);
        TrampolineBuilder builder = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName"
        };

        // This isn't a valid collection, but the enforcement of the graph completedness is handled by the verification stage
        collection = collection with { PrimaryTrampoline = collection.NativeFunction };
        collection = collection with { SecondaryTrampolines = collection.SecondaryTrampolines.Add(builder.Create()) };
        Trampoline trampoline = Assert.Single(collection.SecondaryTrampolines, t => t.Name == "AlternateName");
        Assert.NotNull(trampoline.Target);
        Assert.False(collection.Contains(trampoline.Target));
    }

    [Fact]
    public void SecondaryTrampolinesDoesNotVerifyGraphUsingHelper()
    {
        TrampolineCollection collection = CreateCollection(out TrampolineCollection otherCollection);
        TrampolineBuilder builder = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName"
        };

        // This isn't a valid collection, but the enforcement of the graph completedness is handled by the verification stage
        collection = collection with { PrimaryTrampoline = collection.NativeFunction };
        collection = collection.WithTrampoline(builder.Create());
        Trampoline trampoline = Assert.Single(collection.SecondaryTrampolines, t => t.Name == "AlternateName");
        Assert.NotNull(trampoline.Target);
        Assert.False(collection.Contains(trampoline.Target));
    }

    [Fact]
    public void Contains()
    {
        TrampolineCollection collection = CreateCollection(out TrampolineCollection otherCollection);
        TrampolineBuilder builder = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName"
        };
        collection = collection.WithTrampoline(builder.Create());

        Trampoline a = collection.NativeFunction;
        Trampoline b = collection.PrimaryTrampoline;
        Trampoline c = Assert.Single(collection.SecondaryTrampolines);
        Assert.NotReferenceEqual(a, b);
        Assert.NotReferenceEqual(a, c);
        Assert.NotReferenceEqual(b, c);
        Assert.True(collection.Contains(a));
        Assert.True(collection.Contains(b));
        Assert.True(collection.Contains(c));
        Assert.False(otherCollection.Contains(a));
        Assert.False(otherCollection.Contains(b));
        Assert.False(otherCollection.Contains(c));
    }

    [Fact]
    public void Enumerator()
    {
        TrampolineCollection collection = CreateCollection();
        TrampolineBuilder builder1 = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName"
        };
        collection = collection.WithTrampoline(builder1.Create());
        TrampolineBuilder builder2 = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName2"
        };
        collection = collection.WithTrampoline(builder2.Create());

        List<Trampoline> trampolines = new();
        foreach (Trampoline trampoline in collection)
        { trampolines.Add(trampoline); }

        Assert.Equal(4, trampolines.Count);
        Assert.ReferenceEqual(collection.NativeFunction, trampolines[0]);
        Assert.ReferenceEqual(collection.PrimaryTrampoline, trampolines[1]);
        Assert.ReferenceEqual(collection.SecondaryTrampolines[0], trampolines[2]);
        Assert.ReferenceEqual(collection.SecondaryTrampolines[1], trampolines[3]);
    }

    [Fact]
    public void Enumerator_NoPrimary()
    {
        CreateCollection(out TrampolineCollection collection);
        TrampolineBuilder builder1 = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName"
        };
        collection = collection.WithTrampoline(builder1.Create());
        TrampolineBuilder builder2 = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName2"
        };
        collection = collection.WithTrampoline(builder2.Create());

        List<Trampoline> trampolines = new();
        foreach (Trampoline trampoline in collection)
        { trampolines.Add(trampoline); }

        Assert.Equal(3, trampolines.Count);
        Assert.ReferenceEqual(collection.NativeFunction, trampolines[0]);
        Assert.ReferenceEqual(collection.SecondaryTrampolines[0], trampolines[1]);
        Assert.ReferenceEqual(collection.SecondaryTrampolines[1], trampolines[2]);
    }

    [Fact]
    public void IEnumerator()
    {
        TrampolineCollection collection = CreateCollection();
        TrampolineBuilder builder1 = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName"
        };
        collection = collection.WithTrampoline(builder1.Create());
        TrampolineBuilder builder2 = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName2"
        };
        collection = collection.WithTrampoline(builder2.Create());

        List<Trampoline> trampolines = new((IEnumerable<Trampoline>)collection);
        Assert.Equal(4, trampolines.Count);
        Assert.ReferenceEqual(collection.NativeFunction, trampolines[0]);
        Assert.ReferenceEqual(collection.PrimaryTrampoline, trampolines[1]);
        Assert.ReferenceEqual(collection.SecondaryTrampolines[0], trampolines[2]);
        Assert.ReferenceEqual(collection.SecondaryTrampolines[1], trampolines[3]);
    }

    [Fact]
    public void IEnumerator_NoPrimary()
    {
        CreateCollection(out TrampolineCollection collection);
        TrampolineBuilder builder1 = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName"
        };
        collection = collection.WithTrampoline(builder1.Create());
        TrampolineBuilder builder2 = new(collection.PrimaryTrampoline, useAsTemplate: false)
        {
            Name = "AlternateName2"
        };
        collection = collection.WithTrampoline(builder2.Create());

        List<Trampoline> trampolines = new((IEnumerable<Trampoline>)collection);
        Assert.Equal(3, trampolines.Count);
        Assert.ReferenceEqual(collection.NativeFunction, trampolines[0]);
        Assert.ReferenceEqual(collection.SecondaryTrampolines[0], trampolines[1]);
        Assert.ReferenceEqual(collection.SecondaryTrampolines[1], trampolines[2]);
    }
}
