using ClangSharp;
using ClangSharp.Interop;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Biohazrd
{
    internal static class DiagnosticCollectionExtensions
    {
        public static TList Add<TList>(this TList list, Severity severity, SourceLocation location, string message)
            where TList : IImmutableList<TranslationDiagnostic>
            => (TList)list.Add(new TranslationDiagnostic(location, severity, message));

        public static TList Add<TList>(this TList list, Severity severity, string message)
            where TList : IImmutableList<TranslationDiagnostic>
            => (TList)list.Add(new TranslationDiagnostic(SourceLocation.Null, severity, message));

        public static TList Add<TList>(this TList list, Severity severity, Cursor associatedCursor, string message)
            where TList : IImmutableList<TranslationDiagnostic>
            => (TList)list.Add(new TranslationDiagnostic(new SourceLocation(associatedCursor.Extent.Start), severity, message));

        public static TList Add<TList>(this TList list, Severity severity, CXCursor associatedCursor, string message)
            where TList : IImmutableList<TranslationDiagnostic>
            => (TList)list.Add(new TranslationDiagnostic(new SourceLocation(associatedCursor.Extent.Start), severity, message));

        // --------------------------------------------------------------------------------------------------------------------------------

        public static void Add(this IList<TranslationDiagnostic> list, Severity severity, SourceLocation location, string message)
            => list.Add(new TranslationDiagnostic(location, severity, message));

        public static void Add(this IList<TranslationDiagnostic> list, Severity severity, string message)
            => list.Add(new TranslationDiagnostic(SourceLocation.Null, severity, message));

        public static void Add(this IList<TranslationDiagnostic> list, Severity severity, Cursor associatedCursor, string message)
            => list.Add(new TranslationDiagnostic(new SourceLocation(associatedCursor.Extent.Start), severity, message));

        public static void Add(this IList<TranslationDiagnostic> list, Severity severity, CXCursor associatedCursor, string message)
            => list.Add(new TranslationDiagnostic(new SourceLocation(associatedCursor.Extent.Start), severity, message));
    }
}
