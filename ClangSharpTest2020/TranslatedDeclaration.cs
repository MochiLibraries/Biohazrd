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

                if (_parent is object)
                { _parent.RemoveDeclaration(this); }

                _parent = value;
                _parent?.AddDeclaration(this);
            }
        }

        public abstract string TranslatedName { get; }

        /// <summary>True if this declaration can be translated as a root declaration in C#.</summary>
        public abstract bool CanBeRoot { get; }

        private protected TranslatedDeclaration(IDeclarationContainer parent)
            => Parent = parent;

        public abstract void Translate(CodeWriter writer);

        public override string ToString()
            => TranslatedName;
    }
}
