using ClangSharp;
using ClangSharp.Pathogen;
using System;
using System.Collections.Generic;

namespace ClangSharpTest2020
{
    public abstract class TranslatedDeclaration
    {
        public TranslatedLibrary Library { get; }
        public TranslatedFile File => Parent?.File; //TODO: Ideally this is internal-only, it's used by PhysXFlagsEnumTransformation to access __HACK__LooseDeclarationsContainer

        private IDeclarationContainer _parent;
        public IDeclarationContainer Parent
        {
            get => _parent;
            set
            {
                if (ReferenceEquals(_parent, value))
                { return; }

                if (value is object && !ReferenceEquals(value.Library, this.Library))
                { throw new InvalidOperationException("Translations cannot be moved between translated libraries."); }

                if (_parent is object)
                {
                    _parent.RemoveDeclaration(this);

                    // If we're being removed, remove our declcaration associations from the library
                    if (value is null)
                    {
                        if (Declaration is object)
                        { Library.RemoveDeclarationAssociation(Declaration, this); }

                        if (SecondaryDeclarations is object)
                        {
                            foreach (Decl secondaryDeclaration in SecondaryDeclarations)
                            { Library.RemoveDeclarationAssociation(secondaryDeclaration, this); }
                        }
                    }
                }

                IDeclarationContainer oldParent = _parent;
                _parent = value;
                _parent?.AddDeclaration(this);

                // If we were removed and now we're being re-added, add our declaration associations to the library
                if (oldParent is null && _parent is object)
                {
                    if (Declaration is object)
                    { Library.AddDeclarationAssociation(Declaration, this); }

                    if (SecondaryDeclarations is object)
                    {
                        foreach (Decl secondaryDeclaration in SecondaryDeclarations)
                        { Library.AddDeclarationAssociation(secondaryDeclaration, this); }
                    }
                }
            }
        }

        public void ReplaceWith(TranslatedDeclaration other)
        {
            other.Parent = Parent;
            //TODO: Reorder members of Parent so that other is in the same spot as us.
            Parent = null;
        }

        private Decl _declaration;
        public Decl Declaration
        {
            get => _declaration;
            private protected set
            {
                if (ReferenceEquals(_declaration, value))
                { return; }

                if (File is null)
                { return; }

                // Remove old association
                if (_declaration is object)
                { Library.RemoveDeclarationAssociation(_declaration, this); }

                // Add new association
                _declaration = value;

                if (_declaration is object)
                { Library.AddDeclarationAssociation(_declaration, this); }
            }
        }

        private List<Decl> SecondaryDeclarations = null;
        public void AddSecondaryDeclaration(Decl declaration)
        {
            if (declaration is null)
            { throw new ArgumentNullException(nameof(declaration)); }

            if (SecondaryDeclarations is null)
            { SecondaryDeclarations = new List<Decl>(); }

            SecondaryDeclarations.Add(declaration);

            if (File is object)
            { Library.AddDeclarationAssociation(declaration, this); }
        }

        public abstract string DefaultName { get; }

        private string _TranslatedName = null;
        public string TranslatedName
        {
            get => _TranslatedName ?? DefaultName;
            set => _TranslatedName = value;
        }

        public virtual AccessModifier Accessibility { get; set; } = AccessModifier.Internal;

        /// <summary>True if this declaration can be translated as a root declaration in C#.</summary>
        public abstract bool CanBeRoot { get; }

        /// <summary>True if this declaration has no actual translation.</summary>
        public virtual bool IsDummy => false;

        private protected TranslatedDeclaration(IDeclarationContainer parent)
        {
            Library = parent.Library; // This must be assigned before Parent
            Parent = parent;
        }

        public void Translate(CodeWriter writer)
        {
            // Dump Clang information
            if (GlobalConfiguration.DumpClangDetails && Declaration is object)
            {
                writer.EnsureSeparation();
                writer.WriteLineLeftAdjusted($"#region {Declaration.CursorKindDetailed()} Dump");

                using (writer.Prefix("// "))
                { ClangSharpInfoDumper.Dump(writer, Declaration); }

                writer.WriteLineLeftAdjusted("#endregion");
                writer.NoSeparationNeededBeforeNextLine();
            }

            TranslateImplementation(writer);
        }

        protected abstract void TranslateImplementation(CodeWriter writer);

        public virtual void Validate()
        {
            // If we're at the root, ensure we're using an access level that's valid
            // If invalid, we force the element to be internal
            if (Parent is TranslatedFile && !Accessibility.IsAllowedInNamespaceScope())
            {
                File.Diagnostic
                (
                    Severity.Warning,
                    Declaration,
                    $"{this} was set to be translated as {Accessibility.ToCSharpKeyword()}, but it will be translated into a file/namespace scope. Accessibility changed to internal."
                );

                Accessibility = AccessModifier.Internal;
            }

            // The only thing we translate right now is structs and static classes, neither of which support protected.
            // (C# technically allows subtypes of static classes to be protected, but there's no real reason to do so.)
            if (Accessibility == AccessModifier.Protected || Accessibility == AccessModifier.ProtectedAndInternal || Accessibility == AccessModifier.ProtectedOrInternal)
            {
                File.Diagnostic
                (
                    Severity.Warning,
                    Declaration,
                    $"{this} was set to be translated as {Accessibility.ToCSharpKeyword()}, but protected isn't supported in any translation context."
                );

                Accessibility = AccessModifier.Internal;
            }

            // If the declaration has no name, we specify a default one
            if (String.IsNullOrEmpty(TranslatedName))
            {
                string category = GetType().Name;
                const string stripPrefix = "Translated";

                if (category.StartsWith(stripPrefix))
                { category = category.Substring(stripPrefix.Length); }

                string automaticName = Parent.GetNameForUnnamed(category);
                TranslatedName = automaticName;

                if (Declaration is null)
                { File.Diagnostic(Severity.Warning, $"Nameless {GetType().Name} automatically renamed to {automaticName}."); }
                else
                { File.Diagnostic(Severity.Warning, Declaration, $"Nameless {Declaration.CursorKindDetailed()} at {Declaration.Location} automatically renamed to {automaticName}."); }
            }

            // Validate all children
            if (this is IDeclarationContainer container)
            {
                foreach (TranslatedDeclaration declaration in container)
                { declaration.Validate(); }
            }
        }

        public override string ToString()
            => _TranslatedName is null ? DefaultName : $"{TranslatedName} ({DefaultName})";
    }
}
