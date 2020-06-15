using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;

namespace ClangSharpTest2020
{
    internal sealed class CSharpBuildHelper
    {
        private readonly List<SyntaxTree> SyntaxTrees = new List<SyntaxTree>();
        private readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.None, SourceCodeKind.Regular);
        private readonly CSharpCompilationOptions CompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true, platform: Platform.X64);

        public void AddFile(string filePath)
        {
            string sourceCode = File.ReadAllText(filePath);
            SourceText sourceText = SourceText.From(sourceCode);
            SyntaxTree syntaxTree = SyntaxFactory.ParseSyntaxTree(sourceText, ParseOptions, filePath);
            SyntaxTrees.Add(syntaxTree);
        }

        public ImmutableArray<Diagnostic> Compile()
        {
            // We're using the .NET 5 preview since we're also using the pre-release compiler meant to be paired with it since we're using the unreleased C# 9 function pointers feature.
            const string referenceAssemblyRoot = @"C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\5.0.0-preview.5.20278.1\ref\net5.0\";
            static MetadataReference GetSystemReference(string assemblyFileName)
                => MetadataReference.CreateFromFile(Path.Combine(referenceAssemblyRoot, assemblyFileName + ".dll"));

            List<MetadataReference> references = new List<MetadataReference>()
            {
                GetSystemReference("System"),
                GetSystemReference("System.Runtime"),
                GetSystemReference("System.Runtime.InteropServices"),
                GetSystemReference("System.Runtime.CompilerServices.Unsafe"),
            };

            CSharpCompilation compilation = CSharpCompilation.Create("Test.dll", SyntaxTrees, references, CompilationOptions);
            return compilation.GetDiagnostics();
        }
    }
}
