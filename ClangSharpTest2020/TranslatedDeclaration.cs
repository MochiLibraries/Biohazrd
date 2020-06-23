//#define DUMP_DECLARATION_INFO
using ClangSharp;

namespace ClangSharpTest2020
{
    public abstract class TranslatedDeclaration
    {
        internal TranslatedFile File => Parent?.File;

        private IDeclarationContainer _parent;
        internal IDeclarationContainer Parent
        {
            get => _parent;
            set
            {
                if (ReferenceEquals(_parent, value))
                { return; }

                bool needToSyncDeclarationAssociation = _declaration is object && !ReferenceEquals(File, value?.File);

                if (_parent is object)
                {
                    _parent.RemoveDeclaration(this);

                    // Remove our association from the old file
                    if (needToSyncDeclarationAssociation)
                    { File?.RemoveDeclarationAssociation(Declaration, this); }
                }

                _parent = value;
                _parent?.AddDeclaration(this);

                // Add our association to the old file
                if (needToSyncDeclarationAssociation)
                { File?.AddDeclarationAssociation(Declaration, this); }
            }
        }

        private Decl _declaration;
        internal Decl Declaration
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
                { File.RemoveDeclarationAssociation(_declaration, this); }

                // Add new association
                _declaration = value;

                if (_declaration is object)
                { File.AddDeclarationAssociation(_declaration, this); }
            }
        }

        public abstract string TranslatedName { get; }

        public virtual AccessModifier Accessibility { get; set; } = AccessModifier.Internal;

        /// <summary>True if this declaration can be translated as a root declaration in C#.</summary>
        public abstract bool CanBeRoot { get; }

        private protected TranslatedDeclaration(IDeclarationContainer parent)
            => Parent = parent;

        public void Translate(CodeWriter writer)
        {
            Validate();

#if DUMP_DECLARATION_INFO
            // Dump Clang information
            if (Declaration is object)
            {
                writer.EnsureSeparation();
                writer.WriteLineLeftAdjusted($"#region {Declaration.CursorKindDetailed()} Dump");

                using (writer.Prefix("// "))
                { ClangSharpInfoDumper.Dump(writer, Declaration); }

                writer.WriteLineLeftAdjusted("#endregion");
                writer.NoSeparationNeededBeforeNextLine();
            }
#endif

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
        }

        public override string ToString()
            => TranslatedName;
    }
}
