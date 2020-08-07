using ClangSharp;
using System;
using System.Diagnostics;

namespace ClangSharpTest2020
{
    public sealed class TranslatedUndefinedRecord : TranslatedDeclaration
    {
        private RecordDecl Record { get; }

        public override string DefaultName { get; }
        public override bool CanBeRoot => true;

        internal TranslatedUndefinedRecord(IDeclarationContainer container, RecordDecl record)
            : base(container)
        {
            if (record.Definition is object)
            { throw new ArgumentException("The specified record is defined.", nameof(record)); }

            Record = record;
            Declaration = Record;
            Accessibility = AccessModifier.Public;

            DefaultName = Record.Name;
            Debug.Assert(!String.IsNullOrEmpty(DefaultName), "Undefined records are expected to always have a name.");
        }

        protected override void TranslateImplementation(CodeWriter writer)
        {
            // Eventually it'd be nice to mark this with a special attribute and have an analyzer that prevents dereferencing this type.
            writer.EnsureSeparation();
            writer.WriteLine("/// <remarks>This type was forward-declared but never defined. Do not dereference.</remarks>");
            writer.WriteLine($"{Accessibility.ToCSharpKeyword()} ref partial struct {CodeWriter.SanitizeIdentifier(TranslatedName)}");
            using (writer.Block())
            {
            }
        }
    }
}
