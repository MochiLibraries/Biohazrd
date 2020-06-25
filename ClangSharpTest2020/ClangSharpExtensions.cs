﻿using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using static ClangSharp.Interop.CXTypeKind;
using ClangType = ClangSharp.Type;
using Type = System.Type;

namespace ClangSharpTest2020
{
    internal static class ClangSharpExtensions
    {
        public static string CursorKindDetailed(this Cursor cursor, string delimiter = "/")
        {
            string kind = cursor.CursorKind.ToString();
            string declKind = cursor.Handle.DeclKind.ToString();

            const string kindPrefix = "CXCursor_";
            if (kind.StartsWith(kindPrefix))
            { kind = kind.Substring(kindPrefix.Length); }

            const string declKindPrefix = "CX_DeclKind_";
            if (declKind.StartsWith(declKindPrefix))
            { declKind = declKind.Substring(declKindPrefix.Length); }

            if (cursor.CursorKind == CXCursorKind.CXCursor_UnexposedDecl)
            { kind = null; }

            if (cursor.Handle.DeclKind == CX_DeclKind.CX_DeclKind_Invalid)
            { declKind = null; }

            string ret = cursor.GetType().Name;

            if (kind is object)
            { ret += $"{delimiter}{kind}"; }

            if (declKind is object)
            { ret += $"{delimiter}{declKind}"; }

            return ret;
        }

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

        public static bool IsFromMainFilePathogen(this CXSourceLocation location)
            => PathogenExtensions.pathogen_Location_isFromMainFile(location) != 0;

        private static MethodInfo TranslationUnit_GetOrCreate_CXCursor;
        private static MethodInfo TranslationUnit_GetOrCreate_CXType;
        [ThreadStatic] private static object[] TranslationUnit_GetOrCreate_Parameters;

        public static Cursor GetOrCreate(this TranslationUnit translationUnit, CXCursor handle)
        {
            if (handle.TranslationUnit != translationUnit.Handle)
            { throw new ArgumentException("The specified cursor is not from the specified translation unit.", nameof(handle)); }

            if (TranslationUnit_GetOrCreate_CXCursor == null)
            {
                Type[] parameterTypes = { typeof(CXCursor) };
                const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DoNotWrapExceptions;
                MethodInfo getOrCreateGeneric = typeof(TranslationUnit).GetMethod("GetOrCreate", genericParameterCount: 1, bindingFlags, binder: null, parameterTypes, modifiers: null);

                if (getOrCreateGeneric is null)
                { throw new NotSupportedException("Could not get the GetOrCreate<TCursor>(CXCursor) method!"); }

                TranslationUnit_GetOrCreate_CXCursor = getOrCreateGeneric.MakeGenericMethod(typeof(Cursor));
            }

            if (TranslationUnit_GetOrCreate_Parameters == null)
            { TranslationUnit_GetOrCreate_Parameters = new object[1]; }

            TranslationUnit_GetOrCreate_Parameters[0] = handle; //PERF: Reuse the box
            return (Cursor)TranslationUnit_GetOrCreate_CXCursor.Invoke(translationUnit, TranslationUnit_GetOrCreate_Parameters);
        }

        public static ClangType GetOrCreate(this TranslationUnit translationUnit, CXType handle)
        {
            // This has issues with built-in types. Unclear how important this check even is, so it's disabled for now.
            // In theory we could just check this when there is a declaration, but built-in types seem to have invalid declarations rather than just null ones.
            //if (handle.Declaration.TranslationUnit != translationUnit.Handle)
            //{ throw new ArgumentException("The specified type is not from the specified translation unit.", nameof(handle)); }

            if (TranslationUnit_GetOrCreate_CXType == null)
            {
                Type[] parameterTypes = { typeof(CXType) };
                const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DoNotWrapExceptions;
                MethodInfo getOrCreateGeneric = typeof(TranslationUnit).GetMethod("GetOrCreate", genericParameterCount: 1, bindingFlags, binder: null, parameterTypes, modifiers: null);

                if (getOrCreateGeneric is null)
                { throw new NotSupportedException("Could not get the GetOrCreate<TType>(CXType) method!"); }

                TranslationUnit_GetOrCreate_CXType = getOrCreateGeneric.MakeGenericMethod(typeof(ClangType));
            }

            if (TranslationUnit_GetOrCreate_Parameters == null)
            { TranslationUnit_GetOrCreate_Parameters = new object[1]; }

            TranslationUnit_GetOrCreate_Parameters[0] = handle; //PERF: Reuse the box
            return (ClangType)TranslationUnit_GetOrCreate_CXType.Invoke(translationUnit, TranslationUnit_GetOrCreate_Parameters);
        }

        public static CXCallingConv GetCallingConvention(this FunctionDecl function)
        {
            // When the convention is explicitly specified (with or without the __attribute__ syntax), function.Type will be AttributedType
            // Calling conventions that don't affect the current platform (IE: stdcall on x64) are ignored by Clang (they become CXCallingConv_C)
            if (function.Type is AttributedType attributedType)
            { return attributedType.Handle.FunctionTypeCallingConv; }
            else if (function.Type is FunctionType functionType)
            { return functionType.CallConv; }
            else
            { throw new NotSupportedException($"The function has an unexpected value for `{nameof(function.Type)}`."); }
        }

        public static CallingConvention GetCSharpCallingConvention(this CXCallingConv clangCallingConvention, out string errorMessage)
        {
            errorMessage = null;

            // https://github.com/llvm/llvm-project/blob/91801a7c34d08931498304d93fd718aeeff2cbc7/clang/include/clang/Basic/Specifiers.h#L269-L289
            // https://clang.llvm.org/docs/AttributeReference.html#calling-conventions
            // We generally expect this to always be cdecl on x64. (Clang supports some special calling conventions on x64, but C# doesn't support them.)
            switch (clangCallingConvention)
            {
                case CXCallingConv.CXCallingConv_C:
                    return CallingConvention.Cdecl;
                case CXCallingConv.CXCallingConv_X86StdCall:
                    return CallingConvention.StdCall;
                case CXCallingConv.CXCallingConv_X86FastCall:
                    return CallingConvention.FastCall;
                case CXCallingConv.CXCallingConv_X86ThisCall:
                    return CallingConvention.ThisCall;
                case CXCallingConv.CXCallingConv_Win64:
                    return CallingConvention.Winapi;
                case CXCallingConv.CXCallingConv_Invalid:
                    errorMessage = "Could not determine function's calling convention.";
                    return default;
                default:
                    errorMessage = $"Function uses unsupported calling convention '{clangCallingConvention}'.";
                    return default;
            }
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
        {
            // Reduce the integer type in case it's a typedef
            ClangType reducedIntegerType;
            int levelsOfIndirection;
            file.ReduceType(enumDeclaration.IntegerType, enumDeclaration, TypeTranslationContext.ForEnumUnderlyingType, out reducedIntegerType, out levelsOfIndirection);

            if (levelsOfIndirection > 0)
            { file.Diagnostic(Severity.Error, enumDeclaration, "It is not expected to be possible for an enum's underlying type to be a pointer."); }

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

            if (!ReferenceEquals(enumDeclaration.IntegerType, reducedIntegerType))
            { messagePrefix += $" (reduced from '{enumDeclaration.IntegerType}')"; }

            if (ret.HasValue)
            {
                file.Diagnostic(Severity.Note, enumDeclaration, $"{messagePrefix}, using same-size fallback {ret.Value.ToCSharpKeyword()}.");
                return ret.Value;
            }

            // If we got this far, we can't determine a suitable underlying type
            file.Diagnostic(Severity.Error, enumDeclaration, $"{messagePrefix}.");
            return UnderlyingEnumType.Int;
        }
    }
}
