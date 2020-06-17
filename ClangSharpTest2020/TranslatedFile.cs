// This is disabled right now because it complains about anonyomous types and nested types used immediately for a field's type.
//#define WARN_WHEN_CURSORS_ARE_PROCESSED_MULTIPLE_TIMES
using ClangSharp;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using static ClangSharp.Interop.CXTypeKind;
using ClangType = ClangSharp.Type;

namespace ClangSharpTest2020
{
    public sealed class TranslatedFile : IDisposable
    {
        public TranslatedLibrary Library { get; }

        private readonly List<TranslatedRecord> Records = new List<TranslatedRecord>();
        private readonly List<TranslatedFunction> LooseFunctions = new List<TranslatedFunction>();

        public string FilePath { get; }
        private readonly TranslationUnit TranslationUnit;

        private readonly List<TranslationDiagnostic> _Diagnostics = new List<TranslationDiagnostic>();
        public ReadOnlyCollection<TranslationDiagnostic> Diagnostics { get; }

        private readonly HashSet<Cursor> AllCursors = new HashSet<Cursor>();
        private readonly HashSet<Cursor> UnprocessedCursors;

        private string GlobalFunctionTypeName { get; }

        /// <summary>True if <see cref="Diagnostics"/> contains any diagnostic with <see cref="TranslationDiagnostic.IsError"/> or true.</summary>
        public bool HasErrors { get; private set; }

        internal TranslatedFile(TranslatedLibrary library, CXIndex index, string filePath)
        {
            Library = library;
            FilePath = filePath;
            Diagnostics = _Diagnostics.AsReadOnly();

            // These are the flags used by ClangSharp.PInvokeGenerator, so we're just gonna use them for now.
            CXTranslationUnit_Flags translationFlags =
                CXTranslationUnit_Flags.CXTranslationUnit_IncludeAttributedTypes
            ;

            CXTranslationUnit unitHandle;
            CXErrorCode status = CXTranslationUnit.TryParse(index, FilePath, Library.ClangCommandLineArguments, ReadOnlySpan<CXUnsavedFile>.Empty, translationFlags, out unitHandle);

            if (status != CXErrorCode.CXError_Success)
            {
                Diagnostic(Severity.Fatal, $"Failed to parse source file due to Clang error {status}.");
                return;
            }

            try
            {
                if (unitHandle.NumDiagnostics != 0)
                {
                    for (uint i = 0; i < unitHandle.NumDiagnostics; i++)
                    {
                        using CXDiagnostic diagnostic = unitHandle.GetDiagnostic(i);
                        Diagnostic(diagnostic);
                    }

                    if (HasErrors)
                    {
                        Diagnostic(Severity.Fatal, "Aborting translation due to previous errors.");
                        unitHandle.Dispose();
                        return;
                    }
                }
            }
            catch
            {
                unitHandle.Dispose();
                throw;
            }

            // Create the translation unit
            TranslationUnit = TranslationUnit.GetOrCreate(unitHandle);

            // Enumerate all cursors and mark them as unprocessed (used for sanity checks)
            EnumerateAllCursorsRecursive(TranslationUnit.TranslationUnitDecl);
            UnprocessedCursors = new HashSet<Cursor>(AllCursors);

            // Process the translation unit
            ProcessCursor(ImmutableArray<TranslationContext>.Empty, TranslationUnit.TranslationUnitDecl);

            // Associate loose global functions to a record matching our file name if we have one.
            GlobalFunctionTypeName = Path.GetFileNameWithoutExtension(FilePath);
            if (LooseFunctions.Count > 0)
            {
                TranslatedRecord globalFunctionTarget = Records.FirstOrDefault(r => r.TranslatedName == GlobalFunctionTypeName);

                if (globalFunctionTarget is object)
                {
                    foreach (TranslatedFunction function in LooseFunctions)
                    { globalFunctionTarget.AddAsStaticMethod(function); }

                    LooseFunctions.Clear();
                }
            }

            // Note unprocessed cursors
#if false //TODO: Re-enable this
            foreach (Cursor cursor in UnprocessedCursors)
            { Diagnostic(Severity.Warning, cursor, $"{cursor.CursorKindDetailed()} was not processed."); }
#endif
        }

        private void TranslateGlobalFunctions(CodeWriter writer)
        {
            if (LooseFunctions.Count == 0)
            { return; }

            writer.EnsureSeparation();
            writer.WriteLine($"public static partial class {GlobalFunctionTypeName}");
            using (writer.Block())
            {
                foreach (TranslatedFunction function in LooseFunctions)
                { function.Translate(writer); }
            }
        }

        public void Translate()
        {
            // Translate global functions
            if (LooseFunctions.Count > 0)
            {
                using CodeWriter writer = new CodeWriter();
                TranslateGlobalFunctions(writer);
                writer.WriteOut($"{GlobalFunctionTypeName}.cs");
            }

            // Translate records
            foreach (TranslatedRecord record in Records)
            { record.Translate(); }
        }

        public void Translate(CodeWriter writer)
        {
            // Translate global functions
            TranslateGlobalFunctions(writer);

            // Translate records
            foreach (TranslatedRecord record in Records)
            { record.Translate(writer); }
        }

        private void Diagnostic(in TranslationDiagnostic diagnostic)
        {
            _Diagnostics.Add(diagnostic);

            if (diagnostic.IsError)
            { HasErrors = true; }

            // Send the diagnostic to the library
            Library.Diagnostic(diagnostic);
        }

        internal void Diagnostic(Severity severity, SourceLocation location, string message)
            => Diagnostic(new TranslationDiagnostic(this, location, severity, message));

        internal void Diagnostic(Severity severity, string message)
            => Diagnostic(severity, new SourceLocation(FilePath), message);

        private void Diagnostic(CXDiagnostic clangDiagnostic)
            => Diagnostic(new TranslationDiagnostic(this, clangDiagnostic));

        internal void Diagnostic(Severity severity, Cursor associatedCursor, string message)
            => Diagnostic(severity, new SourceLocation(associatedCursor.Extent.Start), message);

        internal void Diagnostic(Severity severity, CXCursor associatedCursor, string message)
            => Diagnostic(severity, new SourceLocation(associatedCursor.Extent.Start), message);

        private void EnumerateAllCursorsRecursive(Cursor cursor)
        {
            // Only add cursors from the main file to AllCursors
            //PERF: Skip this if the parent node wasn't from the main file
            if (cursor.IsFromMainFile())
            { AllCursors.Add(cursor); }

            // Add all children, recursively
            foreach (Cursor child in cursor.CursorChildren)
            { EnumerateAllCursorsRecursive(child); }
        }

        internal void Consume(Cursor cursor)
        {
            if (cursor.TranslationUnit != TranslationUnit)
            { throw new InvalidOperationException("The file should not attempt to consume cursors from other translation units."); }

            if (!UnprocessedCursors.Remove(cursor))
            {
                if (AllCursors.Contains(cursor))
                {
#if WARN_WHEN_CURSORS_ARE_PROCESSED_MULTIPLE_TIMES
                    // Only warn if the cursor is a declaration, a statement, or an untyped cursor.
                    // This idea here is to only warn for cursors which affect behavior or API.
                    // This avoids issues like when a type reference is shared between multiple cursors, such as `int i, j;`-type variable declarations.
                    if (cursor is Decl || cursor is Stmt || cursor.GetType() == typeof(Cursor))
                    { Diagnostic(Severity.Warning, cursor, $"{cursor.CursorKindDetailed()} cursor was processed more than once."); }
#endif
                }
                else if (cursor.TranslationUnit != TranslationUnit)
                { Diagnostic(Severity.Error, cursor, $"{cursor.CursorKindDetailed()} cursor was processed from an external translation unit."); }
                else
                {
                    // We shouldn't process cursors that come from outside of our file.
                    // Note: This depends on Cursor.IsFromMainFile using pathogen_Location_isFromMainFile because otherwise macro expansions will trigger this.
                    // Note: We can't only rely on the cursor having been in the AllCursors list because there are some oddball cursors that are part of the translation unit but aren't part of the AST.
                    //       One example of such a cursor is the one on the word `union` in an anonymous, fieldless union in a struct.
                    if (!cursor.IsFromMainFile())
                    { Diagnostic(Severity.Warning, cursor, $"{cursor.CursorKindDetailed()} cursor from outside our fle was processed."); }

                    // If we consume a cursor which we didn't consider to be a part of this file, we add it to our list of
                    // all cursors to ensure our double cursor consumption above works for them.
                    AllCursors.Add(cursor);
                }
            }
        }

        internal void Consume(CXCursor cursorHandle)
            => Consume(FindCursor(cursorHandle));

        internal void ConsumeRecursive(Cursor cursor)
        {
            Consume(cursor);

            foreach (Cursor child in cursor.CursorChildren)
            { ConsumeRecursive(child); }
        }

        internal void ConsumeRecursive(CXCursor cursorHandle)
            => ConsumeRecursive(FindCursor(cursorHandle));

        /// <remarks>Same as consume, but indicates that the cursor has no affect on the translation output.</remarks>
        internal void Ignore(Cursor cursor)
            => Consume(cursor);

        /// <remarks>Same as consume, but indicates that the cursor has no affect on the translation output.</remarks>
        internal void IgnoreRecursive(Cursor cursor)
            => ConsumeRecursive(cursor);

        internal void ProcessCursorChildren(ImmutableArray<TranslationContext> context, Cursor cursor)
        {
            foreach (Cursor child in cursor.CursorChildren)
            { ProcessCursor(context, child); }
        }

        internal void ProcessUnconsumeChildren(ImmutableArray<TranslationContext> context, Cursor cursor)
        {
            foreach (Cursor child in cursor.CursorChildren)
            {
                if (UnprocessedCursors.Contains(child))
                { ProcessCursor(context, child); }
            }
        }

        internal void ProcessCursor(ImmutableArray<TranslationContext> context, Cursor cursor)
        {
            // Skip cursors outside of the specific file being processed
            if (!cursor.IsFromMainFile())
            { return; }

            //---------------------------------------------------------------------------------------------------------
            // Skip cursors which explicitly do not have translation implemented.
            // This needs to happen first in case some of these checks overlap with cursors which are translated.
            // (For instance, class template specializatiosn are records.)
            //---------------------------------------------------------------------------------------------------------
            if (IsExplicitlyUnsupported(cursor))
            {
                Diagnostic(Severity.Ignored, cursor, $"{cursor.CursorKindDetailed()} aren't supported yet.");
                IgnoreRecursive(cursor);
                return;
            }

            //---------------------------------------------------------------------------------------------------------
            // Cursors which do not have a direct impact on the output
            // (These cursors are usually just containers for other cursors or the information
            //  they provide is already available on the cursors which they affect.)
            //---------------------------------------------------------------------------------------------------------

            // For translation units, just process all the children
            if (cursor is TranslationUnitDecl)
            {
                Debug.Assert(context.Length == 0);
                Ignore(cursor);
                ProcessCursorChildren(context, cursor);
                return;
            }

            // Ignore linkage specification (IE: `exern "C"`)
            if (cursor.Handle.DeclKind == CX_DeclKind.CX_DeclKind_LinkageSpec)
            {
                Ignore(cursor);
                ProcessCursorChildren(context, cursor);
                return;
            }

            // Ignore unimportant (to us) attributes on declarations
            if (cursor is Decl decl)
            {
                foreach (Attr attribute in decl.Attrs)
                {
                    switch (attribute.Kind)
                    {
                        case CX_AttrKind.CX_AttrKind_DLLExport:
                        case CX_AttrKind.CX_AttrKind_DLLImport:
                            Ignore(attribute);
                            break;
                    }
                }
            }

            // Namespace using directives do not impact the output
            if (cursor is UsingDirectiveDecl)
            {
                IgnoreRecursive(cursor);
                return;
            }

            // Namespace aliases do not impact the output
            if (cursor is NamespaceAliasDecl)
            {
                IgnoreRecursive(cursor);
                return;
            }

            // Friend declarations don't really mean anything to C#
            // They're usually implementation details anyway.
            if (cursor is FriendDecl)
            {
                IgnoreRecursive(cursor);
                return;
            }

            //---------------------------------------------------------------------------------------------------------
            // Cursors which only affect the context
            //---------------------------------------------------------------------------------------------------------

            // Namespaces
            if (cursor is NamespaceDecl namespaceDeclaration)
            {
                Consume(cursor);
                ProcessCursorChildren(context.Add(namespaceDeclaration), cursor);
                return;
            }

            //---------------------------------------------------------------------------------------------------------
            // Records and loose functions
            //---------------------------------------------------------------------------------------------------------

            // Handle records (classes, structs, and unions)
            if (cursor is RecordDecl record)
            {
                // Ignore forward-declarations
                if (!record.Handle.IsDefinition)
                {
                    Ignore(cursor);
                    return;
                }

                Records.Add(new TranslatedRecord(context, this, record));
                return;
            }

            // Handle loose functions
            if (cursor is FunctionDecl function)
            {
                LooseFunctions.Add(new TranslatedFunction(context, this, function));
                return;
            }

            //---------------------------------------------------------------------------------------------------------
            // Failure
            //---------------------------------------------------------------------------------------------------------

            // If we got this far, we didn't know how to process the cursor
            // At one point we processed the children of the cursor anyway, but this can lead to confusing behavior when the skipped cursor provided meaningful context.
            Diagnostic(Severity.Warning, cursor, $"Not sure how to process cursor of type {cursor.CursorKindDetailed()}.");
        }

        private static bool IsExplicitlyUnsupported(Cursor cursor)
        {
            // Ignore template specializations
            if (cursor is ClassTemplateSpecializationDecl)
            { return true; }

            // Ignore templates
            if (cursor is TemplateDecl)
            { return true; }

            // Ignore typedefs
            // Typedefs will probably almost always have to be a special case.
            // Sometimes they aren't very meaningful to the translation, and sometimes they have a large impact on how the API is used.
            if (cursor is TypedefDecl)
            { return true; }

            // Can't translate global variables yet
            //TODO: Constants deserve special treatment here.
            if (cursor is VarDecl)
            { return true; }

            // Enums aren't supported yet
            if (cursor is EnumDecl)
            { return true; }

            // If we got this far, the cursor might be supported
            return false;
        }

        public Cursor FindCursor(CXCursor cursorHandle)
        {
            if (cursorHandle.IsNull)
            {
                Diagnostic(Severity.Warning, $"Someone tried to get the Cursor for a null handle.");
                return null;
            }

            return TranslationUnit.GetOrCreate(cursorHandle);
        }

        public ClangType FindType(CXType typeHandle)
        {
            if (typeHandle.kind == CXTypeKind.CXType_Invalid)
            {
                Diagnostic(Severity.Warning, $"Someone tried to get the Type for an invalid type handle.");
                return null;
            }

            return TranslationUnit.GetOrCreate(typeHandle);
        }

        internal void WriteType(CodeWriter writer, ClangType type, CXCursor associatedCursor, TypeTranslationContext context)
        {
            int levelsOfIndirection = 0;
            ClangType unreducedType = type;

            // Walk the type up until we find the type we actually want to print
            // This also figures out how many levels of indirection the type has
            bool keepGoing = true;
            do
            {
                switch (type)
                {
                    // Elaborated types are namespace-qualified types like physx::PxU32 instead of just PxU32.
                    case ElaboratedType elaboratedType:
                        type = elaboratedType.NamedType;
                        break;
                    // For now, we discard typedefs and translate the type they alias
                    case TypedefType typedefType:
                        type = typedefType.CanonicalType;
                        break;
                    case PointerType pointerType:
                        type = pointerType.PointeeType;
                        levelsOfIndirection++;
                        break;
                    case ReferenceType referenceType:
                        // Our test codebase doesn't have any types like this, and I'm not actually sure what it is.
                        // Complain if we find one so we can hopefully resolve the issue.
                        if (referenceType.Kind == CXTypeKind.CXType_RValueReference)
                        { Diagnostic(Severity.Warning, associatedCursor, "Found RValue reference type. This type may not be translated correctly (due to lack of real-world samples.)"); }

                        // References are translated as pointers
                        type = referenceType.PointeeType;
                        levelsOfIndirection++;
                        break;
                    case ArrayType arrayType:
                        // Specific array type handling
                        switch (arrayType)
                        {
                            case ConstantArrayType constantArrayType:
                                if (context == TypeTranslationContext.ForReturn)
                                { Diagnostic(Severity.Error, associatedCursor, "Cannot translate constant-sized array return type."); }
                                else if (context == TypeTranslationContext.ForField)
                                { Diagnostic(Severity.Warning, associatedCursor, "Constant-sized arrays are not fully supported. Only first element was translated."); }
                                else if (context == TypeTranslationContext.ForParameter)
                                { Diagnostic(Severity.Warning, associatedCursor, "The size of the array for this parameter won't be translated."); }
                                break;
                            case DependentSizedArrayType dependentSizedArrayType:
                                // Dependent-sized arrays are arrays sized by a template parameter
                                Diagnostic(Severity.Error, associatedCursor, "Dependent-sized arrays are not supported.");
                                break;
                            case IncompleteArrayType incompleteArrayType:
                                if (context != TypeTranslationContext.ForParameter)
                                { Diagnostic(Severity.Error, associatedCursor, "Incomplete array types are only supported as parameters."); }
                                break;
                            default:
                                Diagnostic(Severity.Error, associatedCursor, $"Don't know how to translate array type {type.GetType().Name} ({type.Kind})");
                                break;
                        }

                        // If we're in the context other than a field, translate the array as a pointer
                        if (context != TypeTranslationContext.ForField)
                        { levelsOfIndirection++; }

                        type = arrayType.ElementType;
                        break;
                    default:
                        // If we got this far, we either encountered a type we can't deal with or we hit a type we can translate.
                        keepGoing = false;
                        break;
                }
            } while (keepGoing);

            // Determine the type name
            // Things we probably can/should support: (Some things here need verification of when they actually occur in a C++ codebase.)
            // CXType_Char_U
            // CXType_Char16
            // CXType_Char32 -- Unclear what to translate it as Char.ConvertToUtf32 just uses int.
            //TODO: Support function pointers (CXType_FunctionProto)
            (string typeName, int cSharpTypeSize) = type.Kind switch
            {
                CXType_Void => ("void", 0),
                CXType_Bool => ("bool", sizeof(bool)),

                // Character types
                // We always translate `char` (without an explicit sign) as `byte` because in C this type ususally indicates a string and
                // .NET's Encoding utilities all work with bytes.
                // (Additionally, good developers will explicitly sign numeric 8-bit fields since char's signedness is undefined)
                CXType_Char_S => ("byte", sizeof(byte)), // char (with -fsigned-char)
                CXType_Char_U => ("byte", sizeof(byte)), // char (with -fno-signed-char)
                CXType_WChar => ("char", sizeof(char)), // wchar_t
                CXType_Char16 => ("char", sizeof(char)), // char16_t

                // Unsigned integer types
                CXType_UChar => ("byte", sizeof(byte)), // unsigned char / uint8_t
                CXType_UShort => ("ushort", sizeof(ushort)),
                CXType_UInt => ("uint", sizeof(uint)),
                CXType_ULong => ("uint", sizeof(uint)),
                CXType_ULongLong => ("ulong", sizeof(ulong)),

                // Signed integer types
                CXType_SChar => ("sbyte", sizeof(sbyte)), // signed char / int8_t
                CXType_Short => ("short", sizeof(short)),
                CXType_Int => ("int", sizeof(int)),
                CXType_Long => ("int", sizeof(int)),
                CXType_LongLong => ("long", sizeof(long)),

                // Floating point types
                CXType_Float => ("float", sizeof(float)),
                CXType_Double => ("double", sizeof(double)),

                // Records and enums
                //TODO: Deal with namespaces and such
                CXType_Record => (CodeWriter.SanitizeIdentifier(((RecordType)type).Decl.Name), 0),
                CXType_Enum => (CodeWriter.SanitizeIdentifier(((EnumType)type).Decl.Name), 4),

                // If we got this far, we don't know how to translate this type
                _ => (null, 0)
            };

            // If the size of the C# type is known, we try to sanity-check it
            // If the check fails, we erase the type name and let the substitute logic run below
            // (This check failing likely indicates a programming issue, so we assert too.)
            if (cSharpTypeSize != 0 && type.Handle.SizeOf != cSharpTypeSize)
            {
                Diagnostic(Severity.Error, $"sizeof({type}) is {type.Handle.SizeOf}, but the translated sizeof({typeName}) is {cSharpTypeSize}.");
                Debug.Assert(false, "This size check shouldn't fail.");
                typeName = null;
            }

            // If the type isn't supported, we try to translate it as a primitive
            if (typeName is null)
            {
                string reducedTypeNote = ReferenceEquals(type, unreducedType) ? "" : $" (reduced from `{unreducedType}`)";
                string warningPrefix = $"Not sure how to translate `{type}`{reducedTypeNote}";

                // Pointers to unknown types are changed to void pointers
                if (levelsOfIndirection > 0)
                {
                    typeName = "void";
                    Diagnostic(Severity.Warning, associatedCursor, $"{warningPrefix}, translated as void pointer.");
                }
                // Otherwise we try to find a matching primitive
                else
                {
                    typeName = type.Handle.SizeOf switch
                    {
                        sizeof(byte) => "byte",
                        sizeof(short) => "short",
                        sizeof(int) => "int",
                        sizeof(long) => "long",
                        // Note: There's no reason to try and handle IntPtr here.
                        // Even ignoring the fact that it'll be handled by the int or long branch,
                        // we aren't dealing with pointers at this point so we don't want to translate anything as such.
                        _ => null
                    };

                    if (typeName is object)
                    { Diagnostic(Severity.Warning, associatedCursor, $"{warningPrefix}, translated as same-size C# primitive type `{typeName}`."); }
                    else
                    {
                        //TODO: This is reasonable for fields since we use explicit layouts, but it totally breaks calling conventions.
                        // We need a way to retroactively mark the member as [Obsolete] to preven it from being used.
                        typeName = "byte";
                        Diagnostic
                        (
                            context == TypeTranslationContext.ForField ? Severity.Warning : Severity.Error,
                            associatedCursor,
                            $"{warningPrefix}`, translated as a `byte` since it isn't the size of any C# primitive."
                        );
                    }
                }
            }

            // Write out the type
            // Note that we do not want to use SanatizeSymbol here, because typeName might be a built-in type keyword.
            writer.Write(typeName);

            for (int i = 0; i < levelsOfIndirection; i++)
            { writer.Write('*'); }
        }

        internal void WriteType(CodeWriter writer, ClangType type, Cursor associatedCursor, TypeTranslationContext context)
            => WriteType(writer, type, associatedCursor.Handle, context);

        public void Dispose()
            => TranslationUnit?.Dispose();
    }
}
