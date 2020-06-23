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

        public abstract void Translate(CodeWriter writer);

        public override string ToString()
            => TranslatedName;
    }
}
