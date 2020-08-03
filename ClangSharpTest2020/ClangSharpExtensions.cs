using ClangSharp;
using ClangSharp.Interop;
using ClangSharp.Pathogen;
using System;
using System.Diagnostics;
using static ClangSharp.Interop.CXTypeKind;
using ClangType = ClangSharp.Type;

namespace ClangSharpTest2020
{
    internal static class ClangSharpExtensions
    {
        public static bool IsFromMainFile(this Cursor cursor)
            => cursor.Extent.IsFromMainFile();

        public static bool IsFromMainFile(this CXSourceRange extent)
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
                CX_CXXAccessSpecifier.CX_CXXProtected => AccessModifier.Private, //Protected is not really supported for translation.
                CX_CXXAccessSpecifier.CX_CXXPrivate => AccessModifier.Private,
                _ => AccessModifier.Public // The access specifier is invalid for declarations which aren't members of a record, so they are translated as public.
            };

        public static UnderlyingEnumType GetUnderlyingEnumType(this EnumDecl enumDeclaration, TranslatedFile file)
            => enumDeclaration.IntegerType.ToUnderlyingEnumType(enumDeclaration, file);

        public static UnderlyingEnumType ToUnderlyingEnumType(this ClangType type, Cursor context, TranslatedFile file)
        {
            // Reduce the integer type in case it's a typedef
            ClangType reducedIntegerType;
            int levelsOfIndirection;
            file.ReduceType(type, context, TypeTranslationContext.ForEnumUnderlyingType, out reducedIntegerType, out levelsOfIndirection);

            // In the context of enum class, this isn't even possible
            if (levelsOfIndirection > 0)
            { file.Diagnostic(Severity.Error, context, "An enum's underlying type cannot be a pointer."); }

            // Determine the underlying type from the kind
            UnderlyingEnumType? ret = reducedIntegerType.Kind switch
            {
                // Character types in C++ are considered to be integral and can be used
                // Both Char_S and Char_U are translated as unsigned to remain consistent with WriteReducedType.
                CXType_Char_S => UnderlyingEnumType.Byte,
                CXType_Char_U => UnderlyingEnumType.Byte,
                CXType_WChar => UnderlyingEnumType.UShort,
                CXType_Char16 => UnderlyingEnumType.UShort,

                // Unsigned integer types
                CXType_UChar => UnderlyingEnumType.Byte, // unsigned char / uint8_t
                CXType_UShort => UnderlyingEnumType.UShort,
                CXType_UInt => UnderlyingEnumType.UInt,
                CXType_ULong => UnderlyingEnumType.UInt,
                CXType_ULongLong => UnderlyingEnumType.ULong,

                // Signed integer types
                CXType_SChar => UnderlyingEnumType.SByte, // signed char / int8_t
                CXType_Short => UnderlyingEnumType.Short,
                CXType_Int => UnderlyingEnumType.Int,
                CXType_Long => UnderlyingEnumType.Int,
                CXType_LongLong => UnderlyingEnumType.Long,

                // Failed
                _ => null
            };

            if (ret.HasValue)
            { return ret.Value; }

            // Determine the underlying type from the size
            ret = reducedIntegerType.Handle.SizeOf switch
            {
                sizeof(byte) => UnderlyingEnumType.Byte,
                sizeof(short) => UnderlyingEnumType.Short,
                sizeof(int) => UnderlyingEnumType.Int,
                sizeof(long) => UnderlyingEnumType.Long,
                _ => null
            };

            string messagePrefix = $"Could not determine best underlying enum type to use for '{reducedIntegerType}'";

            if (!ReferenceEquals(type, reducedIntegerType))
            { messagePrefix += $" (reduced from '{type}')"; }

            if (ret.HasValue)
            {
                file.Diagnostic(Severity.Note, context, $"{messagePrefix}, using same-size fallback {ret.Value.ToCSharpKeyword()}.");
                return ret.Value;
            }

            // If we got this far, we can't determine a suitable underlying type
            file.Diagnostic(Severity.Error, context, $"{messagePrefix}.");
            return UnderlyingEnumType.Int;
        }

        public static bool RecordMustBePassedByReference(this CXCursor cursor)
        {
            if (!cursor.IsDeclaration || cursor.DeclKind < CX_DeclKind.CX_DeclKind_FirstRecord || cursor.DeclKind > CX_DeclKind.CX_DeclKind_LastRecord)
            { throw new ArgumentException("The cursor must be a record declaration.", nameof(cursor)); }

            // Note: These rules assume Microsoft x64 ABI, need to evaluate for x86 and non-Windows x64

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

        public static bool MustBePassedByReference(this RecordDecl record)
            => record.Handle.RecordMustBePassedByReference();

        public static bool MustBePassedByReference(this ClangType type)
        {
            switch (type)
            {
                case ElaboratedType elaboratedType:
                    return elaboratedType.NamedType.MustBePassedByReference();
                case TypedefType typedefType:
                    return typedefType.CanonicalType.MustBePassedByReference();
                case RecordType recordType:
                    return ((RecordDecl)recordType.Decl).MustBePassedByReference();
                default:
                    return false;
            }
        }
    }
}
