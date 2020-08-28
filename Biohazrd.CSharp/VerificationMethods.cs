#if false
using System;

namespace Biohazrd.CSharp
{
    class TranslatedLibrary
    {
        public void Validate()
        {
            foreach (string filePath in UnusedFilePaths)
            { Diagnostic(Severity.Note, new SourceLocation(filePath), "Input file did not appear in Clang's cursor tree."); }

            foreach (TranslatedFile file in Files)
            { file.Validate(); }
        }
    }

    class TranslatedDeclaration
    {
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
    }

    class TranslatedNormalField
    {
        public override void Validate()
        {
            base.Validate();

            // Fields in C++ can have the same name as their enclosing type, but this isn't allowed in C# (it results in CS0542)
            // When we encounter such fields, we rename them to avoid the error.
            if (Parent is TranslatedDeclaration parentDeclaration && TranslatedName == parentDeclaration.TranslatedName)
            {
                File.Diagnostic(Severity.Note, Field, $"Renaming '{this}' to avoid conflict with parent with the same name.");
                TranslatedName += "_";
            }
        }
    }

    class TranslatedBaseField
    {
        public TranslatedBaseField()
        {
            // We do not expect more than one base field
            if (Record.Members.Any(m => m is TranslatedBaseField && m != this))
            {
                DefaultName = Record.GetNameForUnnamed(field->Kind.ToString());
                File.Diagnostic(Severity.Warning, Context, $"Record layout contains more than one non-virtual base field, renamed redundant base to {TranslatedName}.");
            }
        }
    }

    class TranslatedVTableField
    {
        public TranslatedVTableField()
        {
            // We do not support more than one VTable field
            if (Record.Members.Any(m => m is TranslatedVTableField && m != this))
            {
                DefaultName = Record.GetNameForUnnamed(field->Kind.ToString());
                File.Diagnostic(Severity.Warning, Context, $"Record layout contains more than one non-virtual base field, renamed redundant base to {DefaultName}.");
            }
        }

        public TranslatedVTableField(int x)// base alias overload
        {
            // This constructor should only be used when the record does not have its own VTable field
            if (Record.Members.Any(m => m is TranslatedVTable && m != this))
            { throw new InvalidOperationException("Base vTable aliases should not be added to records which already have a vTable pointer."); }

            //TODO: Ensure base has a vtable pointer @ 0
        }
    }
}
#endif
