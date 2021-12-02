using Biohazrd.Expressions;
using System.Diagnostics;
using System.Linq;
using static Biohazrd.CSharp.CSharpCodeWriter;

namespace Biohazrd.CSharp
{
    partial class CSharpLibraryGenerator
    {
        private void StartField(TranslatedField field)
        {
            Writer.Using("System.Runtime.InteropServices");

            Writer.EnsureSeparation();
            Writer.Write($"[FieldOffset({field.Offset})] ");

            // Apply MarshalAs to boolean fields
            // This might not strictly be necessary since our struct has an explicit layout, but we do it anyway for the sake of sanity.
            // (The marshaler definitely still runs on bools in explicit layouts, but it's not immediately clear if it is trying to interpret the memory as a 4-byte or 1-byte bool.)
            if (field is TranslatedNormalField { Type: CSharpBuiltinTypeReference cSharpType } && cSharpType.Type == CSharpBuiltinType.Bool)
            { Writer.Write($"[MarshalAs(UnmanagedType.I1)] "); }

            Writer.Write($"{field.Accessibility.ToCSharpKeyword()} ");
        }

        protected override void VisitField(VisitorContext context, TranslatedField declaration)
            => Fatal(context, declaration, "The field kind is unsupported.", $"@ {declaration.Offset}");

        protected override void VisitUnimplementedField(VisitorContext context, TranslatedUnimplementedField declaration)
            => VisitField(context, declaration);

        protected override void VisitVTableField(VisitorContext context, TranslatedVTableField declaration)
            // VTable concerns are handled in the record emit
            => FatalContext(context, declaration);

        protected override void VisitBaseField(VisitorContext context, TranslatedBaseField declaration)
        {
            StartField(declaration);
            WriteType(context, declaration, declaration.Type);
            Writer.Write(' ');
            Writer.WriteIdentifier(declaration.Name);
            Writer.WriteLine(';');
        }

        protected override void VisitNormalField(VisitorContext context, TranslatedNormalField declaration)
        {
            StartField(declaration);
            WriteType(context, declaration, declaration.Type);
            Writer.Write(' ');
            Writer.WriteIdentifier(declaration.Name);
            Writer.WriteLine(';');
        }

        protected override void VisitStaticField(VisitorContext context, TranslatedStaticField declaration)
        {
            Writer.Using("System.Runtime.InteropServices"); // For NativeLibrary
            Writer.EnsureSeparation();

            Writer.Write($"{declaration.Accessibility.ToCSharpKeyword()} static readonly ");
            WriteTypeAsReference(context, declaration, declaration.Type);
            Writer.Write(' ');
            Writer.WriteIdentifier(declaration.Name);
            Writer.Write(" = (");
            WriteTypeAsReference(context, declaration, declaration.Type);
            //TODO: This leaks handles to the native library.
            Writer.WriteLine($")NativeLibrary.GetExport(NativeLibrary.Load(\"{SanitizeStringLiteral(declaration.DllFileName)}\"), \"{SanitizeStringLiteral(declaration.MangledName)}\");");
        }

        protected override void VisitConstant(VisitorContext context, TranslatedConstant declaration)
        {
            TypeReference? type = declaration.Type ?? declaration.Value.InferType();

            if (type is null)
            {
                Fatal(context, declaration, "Constant type was not specified and cannot be inferred.");
                return;
            }

            Writer.EnsureSeparation();
            Writer.Write($"{declaration.Accessibility.ToCSharpKeyword()} const ");
            WriteType(context, declaration, type);
            Writer.Write($" {SanitizeIdentifier(declaration.Name)} = ");
            Writer.Write(GetConstantAsString(context, declaration, declaration.Value, type));
            Writer.WriteLine(';');
        }

        protected override void VisitBitField(VisitorContext context, TranslatedBitField declaration)
        {
            // Determine the type for the backing field
            static CSharpBuiltinType? GetBackingType(TranslatedLibrary library, TypeReference type, out TypeReference effectiveType, ref bool fieldIsEnum, ref bool fieldIsEnumWithSignedValues)
            {
                effectiveType = type;

                switch (type)
                {
                    case CSharpBuiltinTypeReference cSharpType:
                        return cSharpType.Type;
                    case TranslatedTypeReference typeReference:
                        switch (typeReference.TryResolve(library))
                        {
                            case TranslatedEnum { UnderlyingType: CSharpBuiltinTypeReference cSharpType } translatedEnum:
                                CSharpBuiltinType result = cSharpType.Type;
                                fieldIsEnum = true;

                                // Check if any of the enum's values are negative
                                if (cSharpType.Type.IsSigned)
                                { fieldIsEnumWithSignedValues = translatedEnum.Values.Any(v => v.Value > cSharpType.Type.MaxValue); }

                                return result;
                            case TranslatedTypedef translatedTypedef:
                                return GetBackingType(library, translatedTypedef.UnderlyingType, out effectiveType, ref fieldIsEnum, ref fieldIsEnumWithSignedValues);
                            default:
                                return null;
                        }
                    default:
                        return null;
                }
            }

            TypeReference effectiveType;
            bool fieldIsEnum = false;
            bool fieldIsEnumWithSignedValues = false;
            CSharpBuiltinType? backingType = GetBackingType(context.Library, declaration.Type, out effectiveType, ref fieldIsEnum, ref fieldIsEnumWithSignedValues);

            // Boolean bitfields require some special treatment
            bool isBoolBitfield = backingType == CSharpBuiltinType.Bool;
            if (isBoolBitfield)
            { backingType = CSharpBuiltinType.Byte; }

            if (backingType is null || !backingType.IsIntegral)
            {
                Fatal(context, declaration, $"Bit field has an invalid type.");
                return;
            }

            // To simplify some of the bit handling, we treat the backing field as unsigned
            CSharpBuiltinType unsignedBackingType;
            if (!backingType.IsSigned)
            { unsignedBackingType = backingType; }
            else
            {
                if (backingType == CSharpBuiltinType.SByte)
                { unsignedBackingType = CSharpBuiltinType.Byte; }
                else if (backingType == CSharpBuiltinType.Short)
                { unsignedBackingType = CSharpBuiltinType.UShort; }
                else if (backingType == CSharpBuiltinType.Int)
                { unsignedBackingType = CSharpBuiltinType.UInt; }
                else if (backingType == CSharpBuiltinType.Long)
                { unsignedBackingType = CSharpBuiltinType.ULong; }
                else
                {
                    Fatal(context, declaration, $"Unknown/unsupported signed C# integral type '{backingType}'.");
                    return;
                }

                // If the field is an enum, we pretend the field is unsigned (unless the enum explicitly has signed values.)
                // See https://github.com/InfectedLibraries/Biohazrd/issues/71 for details
                if (fieldIsEnum && !fieldIsEnumWithSignedValues)
                { backingType = unsignedBackingType; }
            }

            // Get all the relevant types as strings
            string backingTypeString = GetTypeAsString(context, declaration, backingType);
            string unsignedBackingTypeString = GetTypeAsString(context, declaration, unsignedBackingType);
            string declarationTypeString = GetTypeAsString(context, declaration, effectiveType);

            // Clang (and by extension, Biohazrd) puts the offset of the field at the nearest byte boundry
            // We could just use the smallest underlying type can fit the bit field, but this doesn't work when the width of the field at the end of the bit field group
            // would best be serviced by a 24-bit number. Consider the following layout:
            // struct BitField3
            // {
            //     unsigned int X : 9;
            //     unsigned int Y : 23;
            //     unsigned char Z;
            // };
            // Y is at byte offset 1, bit offset 1, and has a width of 23 bits.
            // If we used a uint for it at this location, the uint would bleed into Z
            // This could cause issues when writing to Y and Z from different threads, and could cause access violations for both reads and writes in cases where
            // Z isn't there and the struct is allocated at the very end of a page and followed by an unallocated page or a guard page.
            // As such, we unfortunately have to divine a "healthier" offset from the information Clang gives us.
            long offset = declaration.Offset;
            int bitOffset = declaration.BitOffset;
            long extraOffset = offset % backingType.SizeOf;
            offset -= extraOffset;
            bitOffset += checked((int)(extraOffset * 8));
            ulong bitMask = (1ul << declaration.BitWidth) - 1ul;
            ulong inverseShiftedMask = ~(bitMask << bitOffset);
            inverseShiftedMask &= backingType.FullBitMask;

            Writer.EnsureSeparation();

            if (Options.__DumpClangInfo)
            {
                Writer.WriteLine($"// ===== Extra bitfield info for {declaration.Name} =====");
                Writer.WriteLine($"//         Offset = {declaration.Offset} -> {offset}");
                Writer.WriteLine($"//      BitOffset = {declaration.BitOffset} -> {bitOffset}");
                Writer.WriteLine($"//       BitWidth = {declaration.BitWidth}");
                Writer.WriteLine($"// Unshifted mask = 0x{bitMask:X8}");
                Writer.WriteLine($"//  Shifted ~mask = 0x{inverseShiftedMask:X8}");
            }

            // Write out the backing field
            Writer.Using("System.Runtime.InteropServices");
            string backingFieldName = SanitizeIdentifier($"__{declaration.Name}__backingField");

            Writer.WriteLine($"[FieldOffset({offset})] private {unsignedBackingTypeString} {backingFieldName};");

            Writer.WriteLine($"{declaration.Accessibility.ToCSharpKeyword()} {declarationTypeString} {SanitizeIdentifier(declaration.Name)}");
            using (Writer.Block())
            {
                string getter;

                // For signed types, we need to handle the sign extension
                if (backingType.IsSigned)
                {
                    int backingSizeOfBits = backingType.SizeOf * 8;
                    int leftShiftAmount = backingSizeOfBits - (bitOffset + declaration.BitWidth);
                    int rightShiftAmount = backingSizeOfBits - declaration.BitWidth;
                    getter = $"(({backingTypeString})({backingFieldName} << {leftShiftAmount})) >> {rightShiftAmount}";
                }
                else
                {
                    getter = $"({backingFieldName} >> {bitOffset}) & 0x{bitMask:X}U";
                }

                // If the backing type doesn't match the declaration type or it isn't 4 or 8 bytes (IE: it isn't int, uint, long, or ulong) we need to cast the result
                if (isBoolBitfield)
                { getter = $"({getter}) != 0"; }
                else if (backingType != effectiveType || (backingType.SizeOf != 4 && backingType.SizeOf != 8))
                { getter = $"({declarationTypeString})({getter})"; }

                Writer.WriteLine($"get => {getter};");

                Writer.WriteLine("set");
                using (Writer.Block())
                {
                    Debug.Assert(unsignedBackingType.SizeOf <= 8, "Types larger than 64 bits are not handled below.");
                    CSharpBuiltinType intermediateBackingType = unsignedBackingType.SizeOf <= 4 ? CSharpBuiltinType.UInt : CSharpBuiltinType.ULong;
                    string intermediateBackingTypeString = GetTypeAsString(context, declaration, intermediateBackingType);

                    // uint shiftedValue = (((uint)value) & 0x00FF) << 16;
                    Writer.Write($"{intermediateBackingTypeString} shiftedValue = (");

                    if (isBoolBitfield)
                    { Writer.Write($"(value ? 1U : 0U)"); }
                    else if (unsignedBackingType != effectiveType)
                    { Writer.Write($"(({intermediateBackingTypeString})value)"); }
                    else
                    { Writer.Write("value"); }

                    Writer.WriteLine($" & 0x{bitMask:X}U) << {bitOffset};");

                    // uint otherBits = backingField & inverseShiftedMask;
                    Writer.WriteLine($"{intermediateBackingTypeString} otherBits = {backingFieldName} & 0x{inverseShiftedMask:X}U;");

                    // backingField = otherBits | shiftedValue;
                    Writer.Write($"{backingFieldName} = ");

                    if (intermediateBackingType != unsignedBackingType)
                    { Writer.Write($"({unsignedBackingTypeString})(otherBits | shiftedValue)"); }
                    else
                    { Writer.Write("otherBits | shiftedValue"); }

                    Writer.WriteLine(";");
                }
            }
        }
    }
}
