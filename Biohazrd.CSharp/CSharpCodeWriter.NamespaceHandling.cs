using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Biohazrd.CSharp
{
    partial class CSharpCodeWriter
    {
        private readonly SortedSet<string> UsingNamespaces = new SortedSet<string>(StringComparer.InvariantCulture);

        private readonly Stack<NamespaceScope> NamespaceScopeStack = new();
        private int TopNamespaceIndentLevel = 0;
        public string? CurrentNamespace => NamespaceScopeStack.TryPeek(out NamespaceScope? currentNamespaceScope) ? currentNamespaceScope.FullNamespaceName : null;
        public bool IsInNamespaceOrRoot => TopNamespaceIndentLevel == IndentLevel;

        // Soft-ended namespaces are used to allow merging compatible namespaces written one after another
        private readonly Queue<NamespaceScope> SoftEndedNamespaceScopes = new();
        private bool SuppressSoftEndedNamespaceFlushOnWrite = false;

        private void FlushEndedNamespaces()
        {
            while (SoftEndedNamespaceScopes.TryDequeue(out NamespaceScope? namespaceScope))
            { namespaceScope.ActuallyEndScope(); }
        }

        protected override void BeforeWrite()
        {
            if (!SuppressSoftEndedNamespaceFlushOnWrite)
            { FlushEndedNamespaces(); }

            base.BeforeWrite();
        }

        protected override void BeforeFinish()
        {
            FlushEndedNamespaces();
            base.BeforeFinish();
        }

        private bool IsChildNamespaceOf(string childNamespace, string parentNamespace, [NotNullWhen(true)] out string? commonPrefix)
            // The . suffix ensures that dissimilar namespaces with a common root are not considered common 
            // (IE: This handles a type from InfectedDirectX.Direct3D12 referencing a type from InfectedDirectX.Direct3D)
            => $"{childNamespace}.".StartsWith(commonPrefix = $"{parentNamespace}.");

        private bool IsChildNamespaceOf(string childNamespace, string parentNamespace)
            => IsChildNamespaceOf(childNamespace, parentNamespace, out _);

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
            if (CurrentNamespace is string currentNamespace && IsChildNamespaceOf(currentNamespace, fullNamespaceName))
            { return; }

            UsingNamespaces.Add(fullNamespaceName);
        }

        /// <summary>Writes a scoped namespace block to the file</summary>
        /// <param name="fullNamespaceName">The full name for the namespace, may be null to skip emitting a namespace scope.</param>
        /// <remarks>If the file is currently writing into a namespace, the specified namespace must be a child of the current one.</remarks>
        public NamespaceScope Namespace(string? fullNamespaceName)
        {
            if (fullNamespaceName is null)
            { return NamespaceScope.Null; }

            if (fullNamespaceName.Length == 0)
            { throw new ArgumentException("The namespace name must not be empty.", nameof(fullNamespaceName)); }

            fullNamespaceName = SanitizeNamespace(fullNamespaceName);
            string? currentNamespace = CurrentNamespace;

            // Figure out if this namespace causes any soft-ended namespaces to restore
            ImmutableArray<NamespaceScope> revivedNamespaceScopes = ImmutableArray<NamespaceScope>.Empty;
            while (SoftEndedNamespaceScopes.TryDequeue(out NamespaceScope? softEndedNamespace))
            {
                // If the soft-ended namespace is not a parent of the namespace we're writing, end it and check the next one
                if (!IsChildNamespaceOf(fullNamespaceName, softEndedNamespace.FullNamespaceName))
                {
                    softEndedNamespace.ActuallyEndScope();
                    continue;
                }

                // The reviving namespace becomes the "current" namespace since this namespace will be nested within it
                currentNamespace = softEndedNamespace.FullNamespaceName;

                // If this namespace matches exactly, it will be a direct revive rather than creating a new scope
                int revivedNamespaceScopesCount = SoftEndedNamespaceScopes.Count;
                bool isDirectRevive = false;

                if (fullNamespaceName == softEndedNamespace.FullNamespaceName)
                { isDirectRevive = true; }
                else
                { revivedNamespaceScopesCount++; }

                // If the soft-ended namespace is a parent of our namespace, revive it and all remaining namespaces
                // (We do all remaining because all remaining scopes should be parents of the current soft-ended scope and therefore also parents of the new namespace.)
                ImmutableArray<NamespaceScope>.Builder revivedNamespaceScopesBuilder = ImmutableArray.CreateBuilder<NamespaceScope>(revivedNamespaceScopesCount);

                if (!isDirectRevive)
                { revivedNamespaceScopesBuilder.Add(softEndedNamespace); }

                while (SoftEndedNamespaceScopes.TryDequeue(out NamespaceScope? remainingScope))
                {
                    Debug.Assert(IsChildNamespaceOf(fullNamespaceName, remainingScope.FullNamespaceName));
                    revivedNamespaceScopesBuilder.Add(remainingScope);
                }

                revivedNamespaceScopes = revivedNamespaceScopesBuilder.MoveToImmutable();

                // Handle direct revive
                if (isDirectRevive)
                {
                    softEndedNamespace.Revive(revivedNamespaceScopes);
                    return softEndedNamespace;
                }
            }

            // Make sure we're in a place where a namespace makes conceptual sense
            if (!IsInNamespaceOrRoot)
            {
                Debug.Assert(IndentLevel > TopNamespaceIndentLevel); // If the indent level is below the top namespace indent level, our state is corrupted
                throw new InvalidOperationException("The namespace cannot be changed in the current scope.");
            }

            // If we're currently within a namespace, ensure the specfied namespace is a child of the current one and figure out the partial namespace to write out
            string partialNamespaceName = fullNamespaceName;
            if (currentNamespace is not null)
            {
                // Special case: If the current namespace is exactly the requested namespace, don't enter a new scope
                if (fullNamespaceName == currentNamespace)
                { return NamespaceScope.Null; }

                string? parentNamespacePrefix;
                if (!IsChildNamespaceOf(fullNamespaceName, currentNamespace, out parentNamespacePrefix))
                { throw new ArgumentException($"'{fullNamespaceName}' is not a child of the current namespace '{currentNamespace}'.", nameof(fullNamespaceName)); }

                partialNamespaceName = fullNamespaceName.Substring(parentNamespacePrefix.Length);
            }

            return new NamespaceScope(this, fullNamespaceName, partialNamespaceName, revivedNamespaceScopes);
        }

        public sealed class NamespaceScope : IDisposable
        {
            private readonly CSharpCodeWriter? Writer;
            private readonly int ExpectedIndentLevel;
            internal string FullNamespaceName { get; }
            private ImmutableArray<NamespaceScope> RevivedScopes;

            internal static readonly NamespaceScope Null = new NamespaceScope();

            private NamespaceScope()
            {
                Writer = null;
                ExpectedIndentLevel = Int32.MinValue;
                FullNamespaceName = "<NULL>";
            }

            internal NamespaceScope(CSharpCodeWriter writer, string fullNamespaceName, string partialNamespaceName, ImmutableArray<NamespaceScope> revivedScopes)
            {
                if (writer is null)
                { throw new ArgumentNullException(nameof(writer)); }

                Writer = writer;
                FullNamespaceName = fullNamespaceName;
                RevivedScopes = revivedScopes;
                Debug.Assert(RevivedScopes.All(s => s.RevivedScopes.IsEmpty), "Revived scopes should not have revived scopes!");

                Writer.EnsureSeparation();
                Writer.WriteLine($"namespace {partialNamespaceName}");
                Writer.WriteLine("{");
                Writer.NoSeparationNeededBeforeNextLine();
                Writer.IndentLevel++;
                ExpectedIndentLevel = Writer.IndentLevel;

                // Make the current indent level as the top namespace indent level
                Debug.Assert((Writer.TopNamespaceIndentLevel + 1) == Writer.IndentLevel);
                Writer.TopNamespaceIndentLevel = Writer.IndentLevel;

                Writer.NamespaceScopeStack.Push(this);
            }

            internal void Revive(ImmutableArray<NamespaceScope> otherRevivedScopes)
            {
                if (Writer is null)
                { throw new InvalidOperationException("The null scope cannot be revived."); }

                if (Writer.NamespaceScopeStack.Contains(this) || !RevivedScopes.IsEmpty)
                { throw new InvalidOperationException("The scope is not in a state where it can be revived."); }

                RevivedScopes = otherRevivedScopes;
                Writer.NamespaceScopeStack.Push(this);
            }

            internal void ActuallyEndScope()
            {
                if (Writer is null)
                { throw new InvalidOperationException("The null scope should never be actually ended!"); }

                if (Writer.IndentLevel != ExpectedIndentLevel)
                { throw new InvalidOperationException("Indent level is not where it should be to actually end this scope!"); }

                Debug.Assert(Writer.IndentLevel == Writer.TopNamespaceIndentLevel);
                Writer.IndentLevel--;
                Writer.TopNamespaceIndentLevel--;

                Debug.Assert(!Writer.SuppressSoftEndedNamespaceFlushOnWrite);
                try
                {
                    Writer.SuppressSoftEndedNamespaceFlushOnWrite = true;
                    Writer.WriteLine('}');
                }
                finally
                { Writer.SuppressSoftEndedNamespaceFlushOnWrite = false; }
            }

            void IDisposable.Dispose()
            {
                if (Writer is null)
                { return; }

                if (Writer.NamespaceScopeStack.Peek() != this)
                { throw new InvalidOperationException("The current file namespace is not what it should be to end this scope!"); }

                if ((Writer.IndentLevel - Writer.SoftEndedNamespaceScopes.Count) != ExpectedIndentLevel)
                { throw new InvalidOperationException("Indent level is not where it should be to end this scope!"); }

                NamespaceScope poppedScope = Writer.NamespaceScopeStack.Pop();
                Debug.Assert(poppedScope == this);

                Writer.SoftEndedNamespaceScopes.Enqueue(this);

                // Dispose of all revived scopes too
                foreach (NamespaceScope revivedScope in RevivedScopes)
                { Writer.SoftEndedNamespaceScopes.Enqueue(revivedScope); }

                // Clear our revived scopes list so we can be revived if needed
                RevivedScopes = ImmutableArray<NamespaceScope>.Empty;
            }

            public override string ToString()
                => $"{nameof(NamespaceScope)}<{FullNamespaceName}>";
        }
    }
}
