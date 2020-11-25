using Biohazrd.Transformation;
using System.Diagnostics;

namespace Biohazrd.CSharp
{
    public sealed class WrapNonBlittableTypesWhereNecessaryTransformation : CSharpTypeTransformationBase
    {
        private NativeBooleanDeclaration? NativeBoolean = null;
        private TranslatedTypeReference? NativeBooleanReference = null;
        private bool NativeBooleanIsNew = false;
        private bool NativeBooleanWasUsed = false;
        private NativeCharDeclaration? NativeChar = null;
        private TranslatedTypeReference? NativeCharRefertence = null;
        private bool NativeCharIsNew = false;
        private bool NativeCharwasUsed = false;

        protected override TranslatedLibrary PreTransformLibrary(TranslatedLibrary library)
        {
            Debug.Assert(NativeBoolean is null, "The native declarations should be available at this point.");
            Debug.Assert(NativeChar is null, "The native declarations should be available at this point.");
            Debug.Assert(NativeBooleanReference is null, "The native declaration references should be available at this point.");
            Debug.Assert(NativeCharRefertence is null, "The native declaration references should be available at this point.");
            NativeBoolean = null;
            NativeChar = null;
            NativeBooleanIsNew = false;
            NativeCharIsNew = false;
            NativeBooleanWasUsed = false;
            NativeCharwasUsed = false;

            // Find any existing declarations
            foreach (TranslatedDeclaration declaration in library.EnumerateRecursively())
            {
                if (NativeBoolean is null && declaration is NativeBooleanDeclaration nativeBoolean)
                { NativeBoolean = nativeBoolean; }

                if (NativeChar is null && declaration is NativeCharDeclaration nativeChar)
                { NativeChar = nativeChar; }

                if (NativeBoolean is not null && NativeChar is not null)
                { break; }
            }

            // Create new declarations if needed
            if (NativeBoolean is null)
            {
                NativeBoolean = new NativeBooleanDeclaration();
                NativeBooleanIsNew = true;
            }

            if (NativeChar is null)
            {
                NativeChar = new NativeCharDeclaration();
                NativeCharIsNew = true;
            }

            NativeBooleanReference = TranslatedTypeReference.Create(NativeBoolean);
            NativeCharRefertence = TranslatedTypeReference.Create(NativeChar);

            return library;
        }

        protected override TypeTransformationResult TransformCSharpBuiltinTypeReference(TypeTransformationContext context, CSharpBuiltinTypeReference type)
        {
            // Marshaler doesn't touch things in pointers
            if (context.Parent is PointerTypeReference)
            { return type; }

            // Methods can use MarshalAs or implicitly use these types on the vtable
            if (context.ParentDeclaration is TranslatedFunction or TranslatedParameter)
            { return type; }

            // Fields can use MarshalAs or are translated using pointers
            if (context.ParentDeclaration is TranslatedField or TranslatedStaticField)
            { return type; }

            if (type.Type == CSharpBuiltinType.Bool)
            {
                NativeBooleanWasUsed = true;
                Debug.Assert(NativeBooleanReference is not null);
                return NativeBooleanReference;
            }
            else if (type.Type == CSharpBuiltinType.Char)
            {
                NativeCharwasUsed = true;
                Debug.Assert(NativeCharRefertence is not null);
                return NativeCharRefertence;
            }
            else
            { return type; }
        }

        protected override TranslatedLibrary PostTransformLibrary(TranslatedLibrary library)
        {
            Debug.Assert(NativeBoolean is not null, "We should have native declarations at this point.");
            Debug.Assert(NativeChar is not null, "We should have native declarations at this point.");

            if (NativeBooleanIsNew && NativeBooleanWasUsed)
            {
                library = library with
                {
                    Declarations = library.Declarations.Add(NativeBoolean)
                };
            }

            if (NativeCharIsNew && NativeCharwasUsed)
            {
                library = library with
                {
                    Declarations = library.Declarations.Add(NativeChar)
                };
            }

            NativeBoolean = null;
            NativeChar = null;
            return library;
        }
    }
}
