using Biohazrd.Transformation.Infrastructure;
using System;
using System.Threading;

namespace Biohazrd.Transformation
{
    public abstract partial class RawTransformationBase : ITransformation
    {
        /// <summary>Indicates whether this transformation supports concurrency.</summary>
        /// <remarks>
        /// While Biohazrd's transformation infrastructure does not currently support processing declarations within a library concurrently, it is designed such that it will be possible in the future.
        /// 
        /// Typical transformations do not need state beyond the immutable state passed to each method. As such, most should be concurrent-friendly by default.
        ///
        /// However, some transformations (in particular, multi-stage transformations) may have extra state that may be manipulated by multiple Transform* methods at the same time.
        /// If these transformations are not written with concurreny in mind, they will not work once this feature is implemented.
        /// As such, they should override this property and return false to indicate they must be executed serially.
        /// </remarks>
        protected virtual bool SupportsConcurrency => true;

        private volatile TranslatedLibrary? _CurrentLibrary;

        public TranslatedLibrary Transform(TranslatedLibrary library)
        {
            // Ensure this instance is not used from multiple threads
            // This is to protect against invalid use with transformations which need to store state about the library being processed
            if (Interlocked.CompareExchange(ref _CurrentLibrary, library, null) is not null)
            { throw new InvalidOperationException("This instance is alreadyh being used from another thread to process a different library."); }

            library = PreTransformLibrary(library);

            TransformationContext context = new(library);
            using ListTransformHelper newDeclarations = new(library.Declarations);

            // Recursively transform each declaration in the library
            foreach (TranslatedDeclaration declaration in library.Declarations)
            {
                TransformationResult transformedDeclaration = TransformRecursively(context, declaration);
                newDeclarations.Add(transformedDeclaration);
            }

            // If the declarations list mutated, create a new library
            if (newDeclarations.WasChanged)
            {
                library = library with
                {
                    Declarations = newDeclarations.ToImmutable()
                };
            }

            library = PostTransformLibrary(library);

            // Release this instance from processing the library
            _CurrentLibrary = null;

            // Allow very late post-transformation steps as a workaround for https://github.com/InfectedLibraries/Biohazrd/issues/64
            library = __HACK__PostPostTransformLibrary(library);

            // Return the modified library
            return library;
        }

        protected virtual TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
            => library;

        protected virtual TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
            => library;

        // This is a dirty hack to work around https://github.com/InfectedLibraries/Biohazrd/issues/64 for CSharpTypeReductionTransformation.
        protected virtual TranslatedLibrary __HACK__PostPostTransformLibrary(TranslatedLibrary library)
            => library;

        private TransformationResult TransformRecursively(TransformationContext context, TranslatedDeclaration declaration)
        {
            // Transform this declaration
            TransformationResult result = Transform(context, declaration);

            // Recurse into children of applicable declarations
            // Note that we do this using the results of the previous transformation because the children of the old declaration might not necessarily exist on the new one(s)
            // Additionally, even if they did still exist, we'd have trouble correlating the children between the two declarations.
            switch (result.Count)
            {
                case 1:
                    result = TransformChildren(context, result.SingleDeclaration);
                    break;
                case > 1:
                {
                    using ListTransformHelper newResults = new(result.Declarations);

                    foreach (TranslatedDeclaration resultDeclaration in result.Declarations)
                    {
                        TransformationResult subResult = TransformChildren(context, resultDeclaration);
                        newResults.Add(subResult);
                    }

                    if (newResults.WasChanged)
                    { result = new TransformationResult(newResults.ToImmutable()); }
                }
                break;
            }

            return result;
        }

        TransformationResult ITransformation.TransformRecursively(TransformationContext context, TranslatedDeclaration declaration)
            => TransformRecursively(context, declaration);

        protected abstract TransformationResult Transform(TransformationContext context, TranslatedDeclaration declaration);

        private TransformationResult TransformChildren(TransformationContext context, TranslatedDeclaration declaration)
            => declaration switch
            {
                ICustomTranslatedDeclaration customDeclaration => customDeclaration.TransformChildren(this, context.Add(declaration)),
                TranslatedRecord recordDeclaration => TransformRecordChildren(context.Add(declaration), recordDeclaration),
                TranslatedFunction functionDeclaration => TransformFunctionChildren(context.Add(declaration), functionDeclaration),
                TranslatedEnum enumDeclaration => TransformEnumChildren(context.Add(declaration), enumDeclaration),
                TranslatedVTable vTableDeclaration => TransformVTableChildren(context.Add(declaration), vTableDeclaration),
                // In the default case, the declaration has no children:
                TranslatedDeclaration => declaration
            };
    }
}
