using ClangSharp;
using ClangSharp.Pathogen;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Biohazrd
{
    internal sealed class CodeGeneratorPool : IDisposable
    {
        private readonly TranslationUnit TranslationUnit;

        // Generator => Currently free
        private readonly ConcurrentDictionary<PathogenCodeGenerator, bool> AllGenerators = new();
        private readonly ConcurrentBag<PathogenCodeGenerator> FreeGenerators = new();

        public CodeGeneratorPool(TranslationUnit translationUnit)
            => TranslationUnit = translationUnit;

        public PathogenCodeGenerator Rent()
        {
            if (FreeGenerators.TryTake(out PathogenCodeGenerator? result))
            {
                AllGenerators[result] = false; // Mark generator as rented
                return result;
            }

            result = new PathogenCodeGenerator(TranslationUnit.Handle);
            bool success = AllGenerators.TryAdd(result, false); // Generator starts rented
            Debug.Assert(success);
            return result;
        }

        public void Return(PathogenCodeGenerator codeGenerator)
        {
            Debug.Assert(AllGenerators.TryGetValue(codeGenerator, out bool isFree), "Generator must be a member of this pool.");
            Debug.Assert(!isFree, "The generator must have been rented.");

            // Mark generator as free and return it to the free pool
            AllGenerators[codeGenerator] = true;
            Thread.MemoryBarrier();
            FreeGenerators.Add(codeGenerator);
        }

        public void Dispose()
        {
            foreach (PathogenCodeGenerator codeGenerator in AllGenerators.Keys)
            { codeGenerator.Dispose(); }

            AllGenerators.Clear();
            FreeGenerators.Clear();
        }
    }
}
