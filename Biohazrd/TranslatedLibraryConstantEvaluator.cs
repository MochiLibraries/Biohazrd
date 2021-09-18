using Biohazrd.Expressions;
using ClangSharp.Interop;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Biohazrd
{
    public sealed class TranslatedLibraryConstantEvaluator : IDisposable
    {
        /* Design note:
         *  It is intentional that this class does not attempt to perform any sort of caching.
         *  This is primarily because odd/broken macros and expressions can influence how others are evaluated.
         *  As such, the only truely safe caching is the single-expression/macro evaluation methods, which aren't recommended for performance reasons regardless.
         *
         *  In theory we could also probably cache results when there's 0 diagnostics or cache results as long as the constant is successfully evlauated,
         *  but in the end it's simpler to just not.
         *
         *  Additionally, it isn't really expected that a macro will be evaluated more than once. A typical Biohazrd generator is expected to probably only have one
         *  transformation which has to process macros, or multiple transformations which process unrelated macros. As such, caching probably wouldn't even be helpful here.
         */

        // This class uses translation unit reparsing, which does not play nice with ClangSharp's internal caching.
        // As such, we store and interact with handles directly.
        private readonly CXIndex ClangIndex;
        private readonly CXTranslationUnit UnitHandle;

        private readonly SourceFile IndexFileBase;
        private readonly ImmutableArray<SourceFileInternal> SourceFiles; // This is primarily to ensure the buffers behind the unsaved files are not garbage collected
        private readonly List<CXUnsavedFile> UnsavedFiles;

        private readonly object ConcurrencyLock = new();

        internal TranslatedLibraryConstantEvaluator(SourceFile indexFileBase, ImmutableArray<SourceFileInternal> sourceFiles, List<CXUnsavedFile> unsavedFiles, ReadOnlySpan<string> commandLineArguments)
        {
            IndexFileBase = indexFileBase;
            SourceFiles = sourceFiles;
            UnsavedFiles = unsavedFiles;

            // Create the initial parsing
            // We can't reuse the parsing from an existing TranslatedLibrary because when we reparse later we end up invalidating all of the memory associated with the original translation unit.
            // Since all translation declarations hold references to cursors in the original translation unit, this would end poorly.
            SourceFileInternal initialIndexFile = new(indexFileBase);
            UnsavedFiles[0] = initialIndexFile.UnsavedFile;

            ClangIndex = CXIndex.Create();
            CXErrorCode translationUnitStatus = CXTranslationUnit.TryParse
            (
                ClangIndex,
                indexFileBase.FilePath,
                commandLineArguments,
                CollectionsMarshal.AsSpan(unsavedFiles),
                // The precompiled peramble is absolutely necessary to ensure evaluation doesn't take forever with large libraries
                // It essentially instructs Clang to automatically cache the parsing of all the headers included by the index.
                CXTranslationUnit_Flags.CXTranslationUnit_PrecompiledPreamble |
                CXTranslationUnit_Flags.CXTranslationUnit_CreatePreambleOnFirstParse |
                // We don't care about warnings from included files, it's assumed they'll be observed in the corresponding TranslatedLibrary.
                CXTranslationUnit_Flags.CXTranslationUnit_IgnoreNonErrorsFromIncludedFiles |
                // We'll never care about function bodies
                CXTranslationUnit_Flags.CXTranslationUnit_SkipFunctionBodies,
                out UnitHandle
            );

            // Ensure the index file is valid during the native call
            GC.KeepAlive(initialIndexFile);

            // In the event parsing fails, we throw an exception
            // This generally never happens since Clang usually emits diagnostics in a healthy manner.
            // libclang uses the status code to report things like internal programming errors or invalid arguments.
            if (translationUnitStatus != CXErrorCode.CXError_Success)
            { throw new InvalidOperationException($"Failed to parse the Biohazrd index file due to a fatal Clang error {translationUnitStatus}."); }

            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
            // Do NOT create a TranslationUnit for the CXTranslationUnit here!
            // ClangSharp does not properly handle a CXTranslationUnit and its associated memory
            // becoming invalid when the reparse occurs!
            //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        }

        public ConstantEvaluationResult Evaluate(TranslatedMacro macro)
        {
            if (macro.WasUndefined)
            { throw new ArgumentException("The specified macro was undefined and cannot be evaluated.", nameof(macro)); }

            if (macro.IsFunctionLike)
            { return Evaluate(macro, Array.Empty<string>()); }

            return Evaluate(macro.Name);
        }

        public ConstantEvaluationResult Evaluate(TranslatedMacro macro, params string[] arguments)
        {
            if (macro.WasUndefined)
            { throw new ArgumentException("The specified macro was undefined and cannot be evaluated.", nameof(macro)); }

            if (!macro.IsFunctionLike)
            {
                if (arguments.Length != 0)
                { throw new ArgumentException($"Arguments specified for non-function macro '{macro}'", nameof(arguments)); }

                return Evaluate(macro.Name);
            }

            // Validate argument count
            int minimumArgumentCount = macro.ParameterNames.Length;

            if (macro.LastParameterIsVardic)
            { minimumArgumentCount--; }

            if (arguments.Length < minimumArgumentCount)
            { throw new ArgumentException($"Not enough arguments specified to evaluate macro '{macro}'", nameof(arguments)); }

            if (!macro.LastParameterIsVardic && arguments.Length > minimumArgumentCount)
            { throw new ArgumentException($"Too many arguments specified to evaluate macro '{macro}'", nameof(arguments)); }

            // Build the expression
            StringBuilder expression = new(macro.Name.Length + 2 + arguments.Length * 3);
            expression.Append(macro.Name);
            expression.Append('(');
            bool first = true;
            foreach (string argument in arguments)
            {
                if (first)
                { first = false; }
                else
                { expression.Append(", "); }

                expression.Append(argument);
            }
            expression.Append(')');

            // Evaluate the expression
            return Evaluate(expression.ToString());
        }

        public ConstantEvaluationResult Evaluate(string expression)
        {
            ImmutableArray<ConstantEvaluationResult> results = EvaluateBatch(new[] { expression });
            Debug.Assert(results.Length == 1);
            return results[0];
        }

        public ImmutableArray<ConstantEvaluationResult> EvaluateBatch(IEnumerable<TranslatedMacro> macros)
        {
            List<string> macroExpressions = macros is ICollection<TranslatedMacro> collection ? new(collection.Count) : new();

            foreach (TranslatedMacro macro in macros)
            {
                if (macro.WasUndefined)
                { throw new ArgumentException("The macro list cannot contain undefined macros, they cannot be evaluated.", nameof(macros)); }

                if (macro.IsFunctionLike && macro.ParameterNames.Length > 0)
                {
                    if (macro.ParameterNames.Length > 0)
                    {
                        throw new ArgumentException
                        (
                            "The macro list cannot contain function-like macros which take parameters. Manually create a macro instantiation expression and call the string overload instead.",
                            nameof(macros)
                        );
                    }

                    macroExpressions.Add($"{macro.Name}()");
                }
                else
                { macroExpressions.Add(macro.Name); }
            }

            return EvaluateBatch(macroExpressions);
        }

        public unsafe ImmutableArray<ConstantEvaluationResult> EvaluateBatch(IReadOnlyList<string> expressions)
        {
            CheckDisposed();
            const string evaluationPrefix = "__BIOHAZRD_EXPRESSION_EVALUATION__";

            // Early out if there are no expressions
            if (expressions.Count == 0)
            { return ImmutableArray<ConstantEvaluationResult>.Empty; }

            //-------------------------------------------------------------------------------------
            // Build the evaluation index file
            //-------------------------------------------------------------------------------------
            StringBuilder evaluationIndexFileContents = new(IndexFileBase.Contents);
            for (int i = 0; i < expressions.Count; i++)
            {
                string evaluationId = $"{evaluationPrefix}{i}";
                evaluationIndexFileContents.AppendLine($"#line 1 \"{evaluationId}\"");
                evaluationIndexFileContents.AppendLine($"auto {evaluationId} = {expressions[i]};");
            }

            //-------------------------------------------------------------------------------------
            // Parse the evaluation index file
            //-------------------------------------------------------------------------------------
            SourceFileInternal evaluationIndexFile = new
            (
                IndexFileBase with
                {
                    Contents = evaluationIndexFileContents.ToString()
                }
            );

            // At this point until completion, this thread owns the translation unit
            lock (ConcurrencyLock)
            {
                UnsavedFiles[0] = evaluationIndexFile.UnsavedFile;

                CXErrorCode unitStatus = UnitHandle.Reparse
                (
                    CollectionsMarshal.AsSpan(UnsavedFiles),
                    UnitHandle.DefaultReparseOptions
                );

                // In the event reparsing fails, we throw an exception
                // This generally never happens since Clang usually emits diagnostics in a healthy manner.
                // libclang uses the status code to report things like internal programming errors or invalid arguments.
                if (unitStatus != CXErrorCode.CXError_Success)
                { throw new InvalidOperationException($"Failed to parse the Biohazrd evaluation index file due to a fatal Clang error {unitStatus}."); }

                //-------------------------------------------------------------------------------------
                // Separate the diagnostics
                //-------------------------------------------------------------------------------------
                List<TranslationDiagnostic> looseDiagnostics = new();
                Dictionary<int, List<TranslationDiagnostic>> compilerDiagnostics = new();
                foreach (CXDiagnostic diagnostic in UnitHandle.DiagnosticSet)
                {
                    // Figure out which evaluation this diagnostic belongs to using the presumed location
                    // (The presumed location is the one which is affected by #line directives.)
                    CXString presumedFile;
                    uint lineNumber;
                    uint columnNumber;
                    diagnostic.Location.GetPresumedLocation(out presumedFile, out lineNumber, out columnNumber);

                    string presumedFileString = presumedFile.ToString();

                    // Handle diagnostics that aren't from an evaluation, generally these are diagnostics from the indexed files.
                    // In theory these could be diagnostics after a macro uses a #line directive, but we don't really expect this to ever actually happen considering
                    // the #line directive doesn't have much real use outside of preprocessor output or very special generated output.
                    if (!presumedFileString.StartsWith(evaluationPrefix))
                    {
                        //TODO: Decide on a better way to handle this.
                        // Currently we just associate any diagnostics with every single evaluation.
                        // (We don't need to worry about warnings from included files here because we instructed Clang to ignore them.)
                        looseDiagnostics.Add(new TranslationDiagnostic(diagnostic));
                        continue;
                    }

                    int evaluationId = Int32.Parse(presumedFileString.AsSpan().Slice(evaluationPrefix.Length));

                    // This really shouldn't even happen unless someone's being intentionally malicious.
                    if (evaluationId < 0 || evaluationId >= expressions.Count)
                    { throw new InvalidOperationException($"The evaluation index is malformed. Unknown evlauation id: {evaluationId}"); }

                    List<TranslationDiagnostic>? evaluationDiagnostics;
                    if (!compilerDiagnostics.TryGetValue(evaluationId, out evaluationDiagnostics))
                    {
                        evaluationDiagnostics = new List<TranslationDiagnostic>();
                        compilerDiagnostics.Add(evaluationId, evaluationDiagnostics);
                    }

                    TranslationDiagnostic evaluationDiagnostic = new
                    (
                        new SourceLocation($"Evaluation of `{expressions[evaluationId]}`", checked((int)lineNumber), checked((int)columnNumber)),
                        diagnostic
                    );
                    evaluationDiagnostics.Add(evaluationDiagnostic);
                }

                //-------------------------------------------------------------------------------------
                // Enumerate the expression cursors
                //-------------------------------------------------------------------------------------
                int maxOnStack = 1024 / sizeof(CXCursor);
                Span<CXCursor> cursors = expressions.Count <= maxOnStack ? stackalloc CXCursor[expressions.Count] : GC.AllocateUninitializedArray<CXCursor>(expressions.Count, pinned: true);
                {
                    // Clear all of the cursors (We have to do this because default(CXCursor) != CXCursor.Null)
                    // (Also we don't want to rely on locals being initialized to 0 for the stackalloc case.)
                    {
                        CXCursor nullCursor = CXCursor.Null;
                        for (int i = 0; i < expressions.Count; i++)
                        { cursors[i] = nullCursor; }
                    }

                    (IntPtr, int) clientData = ((IntPtr)Unsafe.AsPointer(ref cursors[0]), expressions.Count);
                    delegate* unmanaged[Cdecl]<CXCursor, CXCursor, (IntPtr, int)*, CXChildVisitResult> enumeratorPtr = &Enumerator;
                    clang.visitChildren(UnitHandle.Cursor, (IntPtr)enumeratorPtr, &clientData);

                    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
                    static CXChildVisitResult Enumerator(CXCursor cursor, CXCursor parent, (IntPtr Cursors, int ExpressionCount)* clientData)
                    {
                        // Skip cursors not from the main file
                        if (!cursor.Location.IsFromMainFile)
                        { return CXChildVisitResult.CXChildVisit_Continue; }

                        // Skip cursors which are not globals
                        if (cursor.Kind != CXCursorKind.CXCursor_VarDecl)
                        { return CXChildVisitResult.CXChildVisit_Continue; }

                        // Skip variables which are not one of our expression evaluators
                        string name = cursor.Spelling.ToString();
                        if (!name.StartsWith(evaluationPrefix))
                        { return CXChildVisitResult.CXChildVisit_Continue; }

                        // Determine which expression evaluator this is
                        int expressionId;
                        if (!Int32.TryParse(name.AsSpan().Slice(evaluationPrefix.Length), out expressionId))
                        {
                            Debug.Assert(false, "Parsing the ID portion of an expression evaluator variable's name should always succeed.");
                            return CXChildVisitResult.CXChildVisit_Continue;
                        }

                        // Skip if the expression ID is out of bounds
                        if (expressionId < 0 || expressionId >= clientData->ExpressionCount)
                        {
                            Debug.Assert(false, "The expression ID should never be invalid.");
                            return CXChildVisitResult.CXChildVisit_Continue;
                        }

                        // Save the cursor
                        CXCursor* cursors = (CXCursor*)clientData->Cursors;
                        Debug.Assert(cursors[expressionId].IsNull, "An expression ID should never appear more than once.");
                        cursors[expressionId] = cursor;
                        return CXChildVisitResult.CXChildVisit_Continue;
                    }
                }

                //-------------------------------------------------------------------------------------
                // Evaluate the constants and tabulate the results
                //-------------------------------------------------------------------------------------
                ImmutableArray<ConstantEvaluationResult>.Builder results = ImmutableArray.CreateBuilder<ConstantEvaluationResult>(expressions.Count);
                for (int i = 0; i < expressions.Count; i++)
                {
                    string expression = expressions[i];
                    ConstantValue? value = null;
                    ImmutableArray<TranslationDiagnostic> diagnostics = ImmutableArray<TranslationDiagnostic>.Empty;
                    CXCursor cursor = cursors[i];

                    if (compilerDiagnostics.TryGetValue(i, out List<TranslationDiagnostic>? expressionCompilerDiagnostics))
                    { diagnostics = diagnostics.AddRange(expressionCompilerDiagnostics); }

                    if (cursor.IsNull)
                    { diagnostics = diagnostics.Add(new TranslationDiagnostic(Severity.Error, "Expression did not appear in the compiler cursor tree.")); }
                    else
                    {
                        TranslationDiagnostic? evaluationDiagnostic;
                        value = cursor.TryComputeConstantValue(out evaluationDiagnostic);

                        if (evaluationDiagnostic.HasValue)
                        { diagnostics = diagnostics.Add(evaluationDiagnostic.Value); }
                    }

                    results.Add(new ConstantEvaluationResult(expression, value, diagnostics));
                }

                return results.MoveToImmutable();
            }
        }

        private void CheckDisposed()
        {
            if (Disposed)
            { throw new ObjectDisposedException(nameof(TranslatedLibraryConstantEvaluator)); }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool Disposed = false;
        private void Dispose(bool disposing)
        {
            CheckDisposed();

            if (UnitHandle.Handle != default)
            { UnitHandle.Dispose(); }

            if (ClangIndex.Handle != default)
            { ClangIndex.Dispose(); }

            Disposed = true;
        }

        ~TranslatedLibraryConstantEvaluator()
            => Dispose(false);
    }
}
