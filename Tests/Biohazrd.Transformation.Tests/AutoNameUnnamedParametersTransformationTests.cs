using Biohazrd.Tests.Common;
using Biohazrd.Transformation.Common;
using System.Linq;
using Xunit;

namespace Biohazrd.Transformation.Tests
{
    public sealed class AutoNameUnnamedParametersTransformationTests : BiohazrdTestBase
    {
        [Fact]
        public void NothingToDo()
        {
            TranslatedLibrary library = CreateLibrary("void Function(int x, int y);");
            TranslatedLibrary transformed = new AutoNameUnnamedParametersTransformation().Transform(library);
            Assert.ReferenceEqual(library, transformed);
        }

        [Fact]
        public void Basic()
        {
            TranslatedLibrary library = CreateLibrary("void Function(int, int);");
            library = new AutoNameUnnamedParametersTransformation().Transform(library);
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Function");
            Assert.Empty(function.Parameters.Where(p => p.IsUnnamed));
            Assert.Equal("arg0", function.Parameters[0].Name);
            Assert.Equal("arg1", function.Parameters[1].Name);
        }

        [Fact]
        public void Partial()
        {
            TranslatedLibrary library = CreateLibrary("void Function(int x, int, int y, int);");
            library = new AutoNameUnnamedParametersTransformation().Transform(library);
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Function");
            Assert.Empty(function.Parameters.Where(p => p.IsUnnamed));
            Assert.Equal("x", function.Parameters[0].Name);
            Assert.Equal("arg1", function.Parameters[1].Name);
            Assert.Equal("y", function.Parameters[2].Name);
            Assert.Equal("arg3", function.Parameters[3].Name);
        }

        [Fact]
        public void Conflict()
        {
            TranslatedLibrary library = CreateLibrary("void Function(int x, int, int arg1);");
            library = new AutoNameUnnamedParametersTransformation().Transform(library);
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Function");
            Assert.Empty(function.Parameters.Where(p => p.IsUnnamed));
            Assert.Equal("x", function.Parameters[0].Name);
            Assert.Equal("_arg1", function.Parameters[1].Name);
            Assert.Equal("arg1", function.Parameters[2].Name);
        }

        [Fact]
        public void CustomPrefix()
        {
            TranslatedLibrary library = CreateLibrary("void Function(int, int, int, int lol2);");
            library = new AutoNameUnnamedParametersTransformation("lol").Transform(library);
            TranslatedFunction function = library.FindDeclaration<TranslatedFunction>("Function");
            Assert.Empty(function.Parameters.Where(p => p.IsUnnamed));
            Assert.Equal("lol0", function.Parameters[0].Name);
            Assert.Equal("lol1", function.Parameters[1].Name);
            Assert.Equal("_lol2", function.Parameters[2].Name);
            Assert.Equal("lol2", function.Parameters[3].Name);
        }
    }
}
