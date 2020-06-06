using ClangSharp.Interop;
using System;

namespace ClangSharpTest2020
{
    //TODO: We should store location when available
    public struct TranslationDiagnostic
    {
        public readonly TranslatedFile TranslatedFile;
        public readonly SourceLocation Location;
        public readonly Severity Severity;
        public readonly bool IsFromClang;
        public readonly string Message;

        /// <summary>True if <see cref="Severity"/> is <see cref="Severity.Error"/> or <see cref="Severity.Fatal"/>.</summary>
        public bool IsError => Severity == Severity.Error || Severity == Severity.Fatal;

        internal TranslationDiagnostic(TranslatedFile translatedFile, SourceLocation location, Severity severity, string message)
        {
            TranslatedFile = translatedFile;
            Location = location;
            Severity = severity;
            IsFromClang = false;
            Message = message;
        }

        private TranslationDiagnostic(TranslatedFile translatedFile, Severity severity, string message)
            : this(translatedFile, default, severity, message)
        { }

        private static readonly CXDiagnosticDisplayOptions ClangFormatOptions = CXDiagnostic.DefaultDisplayOptions & ~CXDiagnosticDisplayOptions.CXDiagnostic_DisplaySourceLocation;
        internal TranslationDiagnostic(TranslatedFile translatedFile, CXDiagnostic clangDiagnostic)
        {
            TranslatedFile = translatedFile;
            Location = new SourceLocation(clangDiagnostic.Location);
            Severity = clangDiagnostic.Severity switch
            {
                CXDiagnosticSeverity.CXDiagnostic_Ignored => Severity.Ignored,
                CXDiagnosticSeverity.CXDiagnostic_Note => Severity.Note,
                CXDiagnosticSeverity.CXDiagnostic_Warning => Severity.Warning,
                CXDiagnosticSeverity.CXDiagnostic_Error => Severity.Error,
                CXDiagnosticSeverity.CXDiagnostic_Fatal => Severity.Fatal,
                _ => throw new ArgumentException($"Unknown Clang diagnostic severity: {clangDiagnostic.Severity}", nameof(clangDiagnostic))
            };
            IsFromClang = true;
            Message = clangDiagnostic.Format(ClangFormatOptions).ToString();
        }
    }
}
