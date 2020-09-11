using ClangSharp.Interop;
using System;

namespace Biohazrd
{
    public struct TranslationDiagnostic
    {
        public readonly SourceLocation Location;
        public readonly Severity Severity;
        public readonly bool IsFromClang;
        public readonly string Message;

        /// <summary>True if <see cref="Severity"/> is <see cref="Severity.Error"/> or <see cref="Severity.Fatal"/>.</summary>
        public bool IsError => Severity == Severity.Error || Severity == Severity.Fatal;

        internal TranslationDiagnostic(SourceLocation location, Severity severity, string message)
        {
            Location = location;
            Severity = severity;
            IsFromClang = false;
            Message = message;
        }

        public TranslationDiagnostic(Severity severity, string message)
            : this(SourceLocation.Null, severity, message)
        { }

        private static readonly CXDiagnosticDisplayOptions ClangFormatOptions = CXDiagnostic.DefaultDisplayOptions & ~CXDiagnosticDisplayOptions.CXDiagnostic_DisplaySourceLocation;
        internal TranslationDiagnostic(CXDiagnostic clangDiagnostic)
        {
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
