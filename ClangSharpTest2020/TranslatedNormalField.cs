using ClangSharp;
using ClangSharp.Interop;
using System;
using ClangType = ClangSharp.Type;

namespace ClangSharpTest2020
{
    public sealed class TranslatedNormalField : TranslatedField
    {
        internal FieldDecl Field { get; }

        protected override string AccessModifier { get; }

        private readonly bool IsBitField;

        internal unsafe TranslatedNormalField(TranslatedRecord record, PathogenRecordField* field)
            : base(record, field)
        {
            if (field->Kind != PathogenRecordFieldKind.Normal)
            { throw new ArgumentException("The specified field must be a normal field.", nameof(field)); }

            Field = (FieldDecl)File.FindCursor(field->FieldDeclaration);
            IsBitField = field->IsBitField != 0;

            AccessModifier = Field.Access switch
            {
                CX_CXXAccessSpecifier.CX_CXXPublic => "public",
                CX_CXXAccessSpecifier.CX_CXXProtected => "private", //TODO: Implement protected access
                _ => "private"
            };
        }

        public override void Translate(CodeWriter writer)
        {
            //TODO: Bitfields
            using var _bitfields = writer.DisableScope(IsBitField, File, Context, "Unimplemented translation: Bitfields.");

            // If the field is a constant array, we need special translation handling
            ClangType reducedType;
            int levelsOfIndirection;
            File.ReduceType(FieldType, Field, TypeTranslationContext.ForField, out reducedType, out levelsOfIndirection);

            bool isPointerToConstantArray = reducedType.Kind == CXTypeKind.CXType_ConstantArray && levelsOfIndirection > 0;
            using var _pointerToConstantArray = writer.DisableScope(isPointerToConstantArray, File, Context, "Unimplemented translation: Pointer to constant array.");

            if (reducedType is ConstantArrayType constantArray && levelsOfIndirection == 0)
            {
                TranslateConstantArrayField(writer, constantArray);
                return;
            }

            // Perform the translation
            base.Translate(writer);
        }

        private void TranslateConstantArrayField(CodeWriter writer, ConstantArrayType constantArrayType)
        {
            // Reduce the element type
            ClangType reducedElementType;
            int levelsOfIndirection;
            File.ReduceType(constantArrayType.ElementType, Field, TypeTranslationContext.ForField, out reducedElementType, out levelsOfIndirection);

            using var _constantArrayOfArrays = writer.DisableScope(reducedElementType.Kind == CXTypeKind.CXType_ConstantArray, File, Context, "Unimplemented translation: Constant array of constant arrays.");

            // Write out the first element field
            writer.Using("System"); // For ReadOnlySpan<T>
            writer.Using("System.Runtime.InteropServices"); // For FieldOffsetAttribute
            writer.Using("System.Runtime.CompilerServices"); // For Unsafe

            writer.Write($"[FieldOffset({Offset})] private ");
            File.WriteReducedType(writer, reducedElementType, levelsOfIndirection, FieldType, Field, TypeTranslationContext.ForField);
            writer.Write(' ');
            string element0Name = $"__{TranslatedName}_Element0";
            writer.WriteIdentifier(element0Name);
            writer.WriteLine(';');

            writer.Write($"{AccessModifier} ReadOnlySpan<");
            File.WriteReducedType(writer, reducedElementType, levelsOfIndirection, FieldType, Field, TypeTranslationContext.ForField);
            writer.Write("> ");
            writer.WriteIdentifier(TranslatedName);
            writer.WriteLine();
            using (writer.Indent())
            {
                writer.Write("=> new ReadOnlySpan<");
                File.WriteReducedType(writer, reducedElementType, levelsOfIndirection, FieldType, Field, TypeTranslationContext.ForField);
                // Note that using fixed would not be valid here since the span leaves the scope where we are fixed.
                // This relies on the fact that TranslatedRecord writes structs out as ref structs. If that were to change, a different strategy is needed here.
                writer.Write(">(Unsafe.AsPointer(ref ");
                writer.WriteIdentifier(element0Name);
                writer.WriteLine($"), {constantArrayType.Size});");
            }
        }
    }
}
