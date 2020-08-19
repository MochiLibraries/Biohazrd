#nullable enable
using ClangSharp;
using ClangSharp.Interop;
using ClangType = ClangSharp.Type;

namespace Biohazrd
{
    public abstract class TranslationTransformationFactory
    {
        private TranslatedLibrary Library = null!;
        private TranslatedFile File = null!;

        protected TranslatedDeclaration TryFindTranslation(Decl declaration)
            => Library.TryFindTranslation(declaration);

        protected Cursor FindCursor(CXCursor cursorHandle)
            => Library.FindCursor(cursorHandle);

        protected ClangType FindType(CXType typeHandle)
            => Library.FindType(typeHandle);

        protected void Diagnostic(Severity severity, SourceLocation location, string message)
            => File.Diagnostic(severity, location, message);

        protected void Diagnostic(Severity severity, string message)
            => File.Diagnostic(severity, message);

        protected void Diagnostic(Severity severity, Cursor associatedCursor, string message)
            => File.Diagnostic(severity, associatedCursor, message);

        protected void Diagnostic(Severity severity, CXCursor associatedCursor, string message)
            => File.Diagnostic(severity, associatedCursor, message);

        internal TranslationTransformation? CreateInternal(TranslatedDeclaration declaration)
        {
            Library = declaration.Library;
            File = declaration.File;
            TranslationTransformation? ret = Create(declaration);
            File = null!;
            Library = null!;
            return ret;
        }

        protected abstract TranslationTransformation? Create(TranslatedDeclaration declaration);
    }
}
