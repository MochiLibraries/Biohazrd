using ClangSharp;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Biohazrd
{
    public abstract record TranslatedDeclaration : IEquatable<TranslatedDeclaration>, IEnumerable<TranslatedDeclaration>
    {
        public TranslatedFile File { get; }
        public DeclarationId Id { get; }

        public TranslatedDeclaration Original { get; }
        public bool IsOriginal => ReferenceEquals(Original, this);

        public bool IsUnnamed { get; private init; }
        private readonly string _name = null!;
        [AllowNull]
        public string Name
        {
            get => _name;
            init
            {
                // If there's an attempt to set this value to null, use a default name
                // (We do this here since C++ has lots of situations where you don't actually need to name things.)
                if (String.IsNullOrEmpty(value))
                {
                    IsUnnamed = true;
                    _name = $"<>Unnamed{GetType().Name}";
                }
                else
                {
                    Debug.Assert(value.Length > 0);
                    IsUnnamed = false;
                    _name = value;
                }
            }
        }

        public AccessModifier Accessibility { get; init; } = AccessModifier.Internal;

        private string? _namespace = null;
        /// <summary>The dot-separated namespace which contains this declaration.</summary>
        /// <remarks>
        /// This will never contain parent types in the case of declarations within a type. (This includes nested types.)
        ///
        /// You can think of it as the nearest namespace to the declaration. This is done so that if you un-nest a declaration it has a namespace that makes sense.
        /// </remarks>
        public string? Namespace
        {
            get => _namespace;
            init
            {
                // No namespace should use null.
                if (value is string { Length: 0 })
                { throw new InvalidOperationException("Namespace cannot be an empty string. (To specify no namespace, use null.)"); }

                _namespace = value;
            }
        }

        public Decl? Declaration { get; init; }
        public ImmutableArray<Decl> SecondaryDeclarations { get; init; } = ImmutableArray<Decl>.Empty;

        public ImmutableArray<TranslationDiagnostic> Diagnostics { get; init; } = ImmutableArray<TranslationDiagnostic>.Empty;

        public DeclarationMetadata Metadata { get; init; }

        protected TranslatedDeclaration(TranslatedFile file, Decl? declaration = null)
        {
            File = file;
            Id = DeclarationId.NewId();
            Original = this;
            Declaration = declaration;

            // Name handles setting null by auto-generating a placeholder name and updaing IsUnnamed, so it's OK to set it to null.
            Name = Declaration is NamedDecl namedDeclaration ? namedDeclaration.Name : null;

            if (Declaration is not null)
            {
                Accessibility = Declaration.Access.ToTranslationAccessModifier();

                string? namespaceName = null;
                IDeclContext context = Declaration.DeclContext;

                while (context is not null)
                {
                    if (context is NamespaceDecl namespaceContext)
                    {
                        if (namespaceName is null)
                        { namespaceName = namespaceContext.Name; }
                        else
                        { namespaceName = $"{namespaceContext.Name}.{namespaceName}"; }
                    }

                    context = context.Parent;
                }

                Namespace = namespaceName;
            }
        }

        internal bool IsTranslationOf(Decl declaration)
        {
            if (declaration == Declaration)
            { return true; }

            foreach (Decl secondaryDeclaration in SecondaryDeclarations)
            {
                if (declaration == secondaryDeclaration)
                { return true; }
            }

            return false;
        }

        public virtual IEnumerator<TranslatedDeclaration> GetEnumerator()
            => EmptyEnumerator<TranslatedDeclaration>.Instance;

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();

        private string DefaultToString()
            => (IsOriginal || Original.Name == Name) ? Name : $"{Name} ({Original.Name})";

        public override string ToString()
            => DefaultToString();

        // We explicitly implement PrintMembers to opt-out of the compiler's default implementation.
        // We need to do this to avoid infinite recursion in ToString in derived types.
        protected virtual bool PrintMembers(StringBuilder builder)
        {
            Debug.Fail("Records which inherit from TranslatedDeclaration are expected to override ToString.");
            builder.Append(DefaultToString());
            return true;
        }

        // We explicitly implement Equals and GetHashCode to opt-out of records' value-equality.
        // We have to do this because Original creates a cycle in the reference graph
        // (Additionally, we don't actually want/care about value-equality.)
        public virtual bool Equals(TranslatedDeclaration? other)
            => base.Equals(other);

        public override int GetHashCode()
            => base.GetHashCode();
    }
}
