using Biohazrd.OutputGeneration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Biohazrd.CSharp
{
    partial class CSharpCodeWriter
    {
        private readonly SortedSet<string> UsingNamespaces = new SortedSet<string>(StringComparer.InvariantCulture);

        private readonly Stack<string> NamespaceStack = new();
        private int TopNamespaceIndentLevel = 0;
        public string? CurrentNamespace => NamespaceStack.TryPeek(out string? currentNamespace) ? currentNamespace : null;
        public bool IsInNamespaceOrRoot => TopNamespaceIndentLevel == IndentLevel;

        /// <summary>Adds a using statement for the specified namespace to the top of this file.</summary>
        /// <remarks>
        /// Does nothing if the specified namespace is null or is already in scope.
        ///
        /// Using statements are automatically sorted and de-duplicated.
        /// </remarks>
        public void Using(string? fullNamespaceName)
        {
            if (fullNamespaceName is null)
            { return; }

            if (fullNamespaceName.Length == 0)
            { throw new ArgumentException("The namespace name must not be empty.", nameof(fullNamespaceName)); }

            fullNamespaceName = SanitizeNamespace(fullNamespaceName);

            // If we're currently within the requested namespace, don't emit a using
            if (CurrentNamespace is string currentNamespace)
            {
                // The . suffix ensures that dissimilar namespaces with a common root are not considered common 
                // (IE: This handles a type from InfectedDirectX.Direct3D12 referencing a type from InfectedDirectX.Direct3D)
                if ($"{currentNamespace}.".StartsWith($"{fullNamespaceName}."))
                { return; }
            }

            UsingNamespaces.Add(fullNamespaceName);
        }

        /// <summary>Writes a scoped namespace block to the file</summary>
        /// <param name="fullNamespaceName">The full name for the namespace, may be null to skip emitting a namespace scope.</param>
        /// <remarks>If the file is currently writing into a namespace, the specified namespace must be a child of the current one.</remarks>
        public NamespaceScope Namespace(string? fullNamespaceName)
        {
            if (fullNamespaceName is null)
            { return default; }

            if (fullNamespaceName.Length == 0)
            { throw new ArgumentException("The namespace name must not be empty.", nameof(fullNamespaceName)); }

            fullNamespaceName = SanitizeNamespace(fullNamespaceName);
            string partialNamespaceName = fullNamespaceName;

            // Make sure we're in a place where a namespace makes conceptual sense
            if (!IsInNamespaceOrRoot)
            {
                Debug.Assert(IndentLevel > TopNamespaceIndentLevel); // If the indent level is below the top namespace indent level, our state is corrupted
                throw new InvalidOperationException("The namespace cannot be changed in the current scope.");
            }

            // If we're currently within a namespace, ensure the specfied namespace is a child of the current one and figure out the partial namespace to write out
            if (CurrentNamespace is string currentNamespace)
            {
                // Special case: If the current namespace is exactly the requested namespace, don't enter a new scope
                if (fullNamespaceName == currentNamespace)
                { return default; }

                // The . suffix ensures that dissimilar namespaces with a common root are not considered common 
                // (IE: This handles a type from InfectedDirectX.Direct3D12 referencing a type from InfectedDirectX.Direct3D)
                string parentNamespacePrefix = $"{currentNamespace}.";
                if (!$"{fullNamespaceName}.".StartsWith(parentNamespacePrefix))
                { throw new ArgumentException($"'{fullNamespaceName}' is not a child of the current namespace '{currentNamespace}'.", nameof(fullNamespaceName)); }

                partialNamespaceName = fullNamespaceName.Substring(parentNamespacePrefix.Length);
            }

            return new NamespaceScope(this, fullNamespaceName, partialNamespaceName);
        }

        public readonly struct NamespaceScope : IDisposable
        {
            private readonly CSharpCodeWriter Writer;
            private readonly IndentScope Block;
            private readonly string NamespaceName;

            internal NamespaceScope(CSharpCodeWriter writer, string fullNamespaceName, string partialNamespaceName)
            {
                if (writer is null)
                { throw new ArgumentNullException(nameof(writer)); }

                Writer = writer;
                NamespaceName = fullNamespaceName;
                Writer.NamespaceStack.Push(NamespaceName);

                Writer.EnsureSeparation();
                Writer.WriteLine($"namespace {partialNamespaceName}");
                Block = Writer.Block();

                // Make the current indent level as the top namespace indent level
                Debug.Assert((Writer.TopNamespaceIndentLevel + 1) == Writer.IndentLevel);
                Writer.TopNamespaceIndentLevel = Writer.IndentLevel;
            }

            void IDisposable.Dispose()
            {
                if (Writer is null)
                { return; }

                if (Writer.NamespaceStack.Peek() != NamespaceName)
                { throw new InvalidOperationException("The current file namespace is not what it should be to end this scope!"); }

                // Helper to avoid boxing IndentScope
                static void CallDispose<TDisposable>(TDisposable disposable)
                    where TDisposable : struct, IDisposable
                    => disposable.Dispose();

                CallDispose(Block);

                // Ensure the indent level is consistent (IndentScope.Dispose should've thrown an exception if it wasn't.)
                Debug.Assert(Writer.TopNamespaceIndentLevel == (Writer.IndentLevel + 1));
                Writer.TopNamespaceIndentLevel = Writer.IndentLevel;

                string poppedNamespace = Writer.NamespaceStack.Pop();
                Debug.Assert(poppedNamespace == NamespaceName);
            }
        }
    }
}
