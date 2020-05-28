using ClangSharp.Interop;
using System;

namespace ClangSharpTest2020
{
    //TODO: We should store location when available
    public struct TranslationDiagnostic
    {
        public readonly TranslatedFile TranslatedFile;
        public readonly SourceLocation Location;
        public readonly TranslationDiagnosticSeverity Severity;
        public readonly bool IsFromClang;
        public readonly string Message;

        /// <summary>True if <see cref="Severity"/> is <see cref="TranslationDiagnosticSeverity.Error"/> or <see cref="TranslationDiagnosticSeverity.Fatal"/>.</summary>
        public bool IsError => Severity == TranslationDiagnosticSeverity.Error || Severity == TranslationDiagnosticSeverity.Fatal;

        internal TranslationDiagnostic(TranslatedFile translatedFile, SourceLocation location, TranslationDiagnosticSeverity severity, string message)
        {
            TranslatedFile = translatedFile;
            Location = location;
            Severity = severity;
            IsFromClang = false;
            Message = message;
        }

        private TranslationDiagnostic(TranslatedFile translatedFile, TranslationDiagnosticSeverity severity, string message)
            : this(translatedFile, default, severity, message)
        { }

        private static readonly CXDiagnosticDisplayOptions ClangFormatOptions = CXDiagnostic.DefaultDisplayOptions & ~CXDiagnosticDisplayOptions.CXDiagnostic_DisplaySourceLocation;
        internal TranslationDiagnostic(TranslatedFile translatedFile, CXDiagnostic clangDiagnostic)
        {
            TranslatedFile = translatedFile;
            Location = new SourceLocation(clangDiagnostic.Location);
            Severity = clangDiagnostic.Severity switch
            {
                CXDiagnosticSeverity.CXDiagnostic_Ignored => TranslationDiagnosticSeverity.Ignored,
                CXDiagnosticSeverity.CXDiagnostic_Note => TranslationDiagnosticSeverity.Note,
                CXDiagnosticSeverity.CXDiagnostic_Warning => TranslationDiagnosticSeverity.Warning,
                CXDiagnosticSeverity.CXDiagnostic_Error => TranslationDiagnosticSeverity.Error,
                CXDiagnosticSeverity.CXDiagnostic_Fatal => TranslationDiagnosticSeverity.Fatal,
                _ => throw new ArgumentException($"Unknown Clang diagnostic severity: {clangDiagnostic.Severity}", nameof(clangDiagnostic))
            };
            IsFromClang = true;
            Message = clangDiagnostic.Format(ClangFormatOptions).ToString();
        }
    }
}
