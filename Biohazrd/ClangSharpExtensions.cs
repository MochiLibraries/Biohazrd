using Biohazrd.Expressions;
using ClangSharp;
using ClangSharp.Interop;
using ClangSharp.Pathogen;
using System;
using System.Diagnostics;
using System.Text;
using ClangType = ClangSharp.Type;

namespace Biohazrd
{
    public static class ClangSharpExtensions
    {
        internal static bool IsFromMainFile(this Cursor cursor)
            => cursor.Extent.IsFromMainFile();

        internal static bool IsFromMainFile(this CXSourceRange extent)
        {
#if false
            // This property uses libclang's clang_Location_isFromMainFile which in turn uses SourceManager::isWrittenInMainFile
            // This method has some quirks:
            // * It considered cursors which are the result of a macro expansion to have come from outside of the file.
            //  * While technically true, this isn't what we actually want in our case. (Our main motivation is to skip over cursors from included files.)
            // * For some reason the first declaration in a file will only have its end marked as being from the main file, so we check both.
            //  * This happens with some, but not all, cursors created from a macro expansion.
            return extent.Start.IsFromMainFile || extent.End.IsFromMainFile;
#else
            // Unlike clang_Location_isFromMainFile, pathogen_Location_isFromMainFile uses SourceManager::isInMainFile, which does not suffer from the previously mentioned quirks.
            // One downside of it, however, is that it considered builtin macros to be from the main file when CXTranslationUnit_DetailedPreprocessingRecord is enabled.
            // These preprocessor entities look like this:
            //   MacroDefinitionRecord MacroDefinition - __llvm__
            //      From main file: False -- False
            //     From main file2: True -- True
            //       From sys file: True -- True
            //           Expansion: :2:9..19[27..37]
            //       Instantiation: :2:9..19[27..37]
            //            Spelling: :2:9..19[27..37]
            //                File: :2:9..19[27..37]
            //            Presumed: <built-in>:1:9..19
            // --------------------------------------------------------------
            // As such, we check if the cursor comes from a system file first to early-reject it.
            if (extent.Start.IsInSystemHeader || extent.End.IsInSystemHeader)
            { return false; }

            bool isStartInMain = extent.Start.IsFromMainFilePathogen();
            bool isEndInMain = extent.End.IsFromMainFilePathogen();
            Debug.Assert(isStartInMain == isEndInMain, "Both the start and end of a cursor should be in or out of main.");
            return isStartInMain || isEndInMain;
#endif
        }

        public static AccessModifier ToTranslationAccessModifier(this CX_CXXAccessSpecifier accessSpecifier)
            => accessSpecifier switch
            {
                CX_CXXAccessSpecifier.CX_CXXPublic => AccessModifier.Public,
                CX_CXXAccessSpecifier.CX_CXXProtected => AccessModifier.Private, // Protected is not really supported for translation.
                CX_CXXAccessSpecifier.CX_CXXPrivate => AccessModifier.Private,
                _ => AccessModifier.Public // The access specifier is invalid for declarations which aren't members of a record, so they are translated as public.
            };

        internal static bool RecordMustBePassedByReference(this CXCursor cursor, bool isForInstanceMethodReturnValue)
        {
            if (!cursor.IsDeclaration || cursor.DeclKind < CX_DeclKind.CX_DeclKind_FirstRecord || cursor.DeclKind > CX_DeclKind.CX_DeclKind_LastRecord)
            { throw new ArgumentException("The cursor must be a record declaration.", nameof(cursor)); }

            // Note: These rules assume Microsoft x64 ABI, need to evaluate for x86 and non-Windows x64

            // On x64 Windows, user-defined records are _always_ returned by reference for instance methods
            // See https://github.com/InfectedLibraries/Biohazrd/issues/142
            if (isForInstanceMethodReturnValue)
            { return true; }

            // ArgPassingRestrictions only covers cases like having a copy constructor or something similar
            // It (surpririsingly) doesn't handle cases involving the size of the record
            if (cursor.GetRecordArgPassingRestrictions() != PathogenArgPassingKind.CanPassInRegisters)
            { return true; }

            // If the size isn't a power of two or it's bigger than the word size, it must be passed by reference
            long recordSize = cursor.Type.SizeOf;

            if (recordSize < 1)
            { throw new NotSupportedException("This method does not work with cursors missing size information."); }

            switch (recordSize)
            {
                case 1:
                case 2:
                case 4:
                case 8:
                    return false;
                default:
                    return true;
            }
        }

        internal static bool MustBePassedByReference(this RecordDecl record, bool isForInstanceMethodReturnValue)
            => record.Handle.RecordMustBePassedByReference(isForInstanceMethodReturnValue);

        public static bool MustBePassedByReference(this ClangType type, bool isForInstanceMethodReturnValue)
        {
            switch (type)
            {
                case ElaboratedType elaboratedType:
                    return elaboratedType.NamedType.MustBePassedByReference(isForInstanceMethodReturnValue);
                case TypedefType typedefType:
                    return typedefType.CanonicalType.MustBePassedByReference(isForInstanceMethodReturnValue);
                case RecordType recordType:
                    return ((RecordDecl)recordType.Decl).MustBePassedByReference(isForInstanceMethodReturnValue);
                default:
                    return false;
            }
        }

        internal static Cursor FindCursor(this TranslationUnit translationUnit, CXCursor handle)
        {
            if (handle.IsNull)
            { throw new ArgumentException("The specified cursor handle is null.", nameof(handle)); }

#if DEBUG
            if (handle.TranslationUnit != translationUnit.Handle)
            { throw new ArgumentException("The specified cursor handle comes from an unrelated ClangSharp translation unit.", nameof(handle)); }
#endif

            Cursor ret = translationUnit.GetOrCreate(handle);
            Debug.Assert(ret is not null);
            return ret;
        }

        internal static ClangType FindType(this TranslationUnit translationUnit, CXType handle)
        {
            if (handle.kind == CXTypeKind.CXType_Invalid)
            { throw new ArgumentException("The specified type handle is invalid.", nameof(handle)); }

            ClangType ret = translationUnit.GetOrCreate(handle);
            Debug.Assert(ret is not null);
            return ret;
        }

        public unsafe static ConstantValue? TryComputeConstantValue(this CXCursor cursor, out TranslationDiagnostic? diagnostic)
        {
            bool success = false;
            byte* error;
            PathogenConstantValueInfo info;

            try
            {
                success = PathogenExtensions.pathogen_ComputeConstantValue(cursor, &info, &error);
                if (!success)
                {
                    if (error is null)
                    {
                        diagnostic = null;
                        return null;
                    }

                    StringBuilder messageBuilder = new();
                    messageBuilder.Append($"Failed to compute constant: ");

                    for (; *error != 0; error++)
                    { messageBuilder.Append((char)*error); }

                    diagnostic = new TranslationDiagnostic(Severity.Warning, messageBuilder.ToString());
                    return null;
                }

                ConstantValue value = info.ToConstantExpression();

                // Since this type isn't very useful, we just turn it into a diagnostic
                if (value is UnsupportedConstantExpression unsupportedValue)
                {
                    diagnostic = new TranslationDiagnostic(Severity.Warning, $"Unsupported constant: {unsupportedValue.Message}");
                    return null;
                }

                diagnostic = null;
                return value;
            }
            finally
            {
                if (success)
                { PathogenExtensions.pathogen_DeletePathogenConstantValueInfo(&info); }
            }
        }

        public static ConstantValue? TryComputeConstantValue(this VarDecl declaration, out TranslationDiagnostic? diagnostic)
            => TryComputeConstantValue(declaration.Handle, out diagnostic);

        public static ConstantValue? TryComputeConstantValue(this Expr expression, out TranslationDiagnostic? diagnostic)
            => TryComputeConstantValue(expression.Handle, out diagnostic);
    }
}
