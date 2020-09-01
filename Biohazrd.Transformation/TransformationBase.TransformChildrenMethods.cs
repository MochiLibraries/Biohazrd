using Biohazrd.Transformation.Infrastructure;
using System;

namespace Biohazrd.Transformation
{
    partial class TransformationBase
    {
        private TransformationResult TransformRecordChildren(TransformationContext context, TranslatedRecord declaration)
        {
            using ListTransformHelper newMembers = new(declaration.Members);
            SingleTransformHelper<TranslatedBaseField> newNonVirtualBaseField = new(declaration.NonVirtualBaseField);
            SingleTransformHelper<TranslatedVTableField> newVTableField = new(declaration.VTableField);
            SingleTransformHelper<TranslatedVTable> newVTable = new(declaration.VTable);
            using ListTransformHelper newUnsupportedMembers = new(declaration.UnsupportedMembers);

            // Transform members
            foreach (TranslatedDeclaration member in declaration.Members)
            { newMembers.Add(Transform(context, member)); }

            // Transform non-virtual base field
            if (declaration.NonVirtualBaseField is not null)
            {
                Assumes.IsSealed<TranslatedBaseField>();
                newNonVirtualBaseField.SetValue(TransformBaseField(context, declaration.NonVirtualBaseField));
            }

            // Transform vtable field
            if (declaration.VTableField is not null)
            {
                Assumes.IsSealed<TranslatedVTableField>();
                newVTableField.SetValue(TransformVTableField(context, declaration.VTableField));
            }

            // Transform vtable
            if (declaration.VTable is not null)
            {
                Assumes.IsSealed<TranslatedVTable>();
                newVTable.SetValue(TransformVTable(context, declaration.VTable));
            }

            // Transform unsupported members
            foreach (TranslatedDeclaration unsupportedMember in declaration.UnsupportedMembers)
            { newUnsupportedMembers.Add(Transform(context, unsupportedMember)); }

            // If the record changed, mutate it
            if (newMembers.WasChanged || newNonVirtualBaseField.WasChanged || newVTableField.WasChanged || newVTable.WasChanged || newUnsupportedMembers.WasChanged)
            {
                // If any of the specifically-typed fields were replaced with a value that isn't valid for that field, move the new declaration to the Members list
                TransformationResult extraMembers = new();

                if (newNonVirtualBaseField.HasExtraValues)
                { extraMembers.AddRange(newNonVirtualBaseField.ExtraValues); }

                if (newVTableField.HasExtraValues)
                { extraMembers.AddRange(newVTableField.ExtraValues); }

                if (newVTable.HasExtraValues)
                { extraMembers.AddRange(newVTable.ExtraValues); }

                if (extraMembers.Count > 0)
                { newMembers.Add(extraMembers); }

                // Create the new record
                return declaration with
                {
                    Members = newMembers.ToImmutable(),
                    NonVirtualBaseField = newNonVirtualBaseField.NewValue,
                    VTableField = newVTableField.NewValue,
                    VTable = newVTable.NewValue,
                    UnsupportedMembers = newUnsupportedMembers.ToImmutable()
                };
            }
            else
            { return declaration; }
        }

        private TransformationResult TransformFunctionChildren(TransformationContext context, TranslatedFunction declaration)
        {
            // Transform parameters
            ArrayTransformHelper<TranslatedParameter> newParameters = new(declaration.Parameters);
            foreach (TranslatedParameter parameter in declaration.Parameters)
            {
                Assumes.IsSealed<TranslatedParameter>();
                newParameters.Add(TransformParameter(context, parameter));

                // In theory we could handle this situation by returning the other declarations as siblings of the function, but for now we consider it invalid.
                if (newParameters.HasOtherDeclarations)
                { throw new InvalidOperationException("Tried to transform a function parameter into something other than a function parameter in the context of an function."); }
            }

            // If the function changed, mutate it
            if (newParameters.WasChanged)
            {
                return declaration with
                {
                    Parameters = newParameters.MoveToImmutable()
                };
            }
            else
            { return declaration; }
        }

        private TransformationResult TransformEnumChildren(TransformationContext context, TranslatedEnum declaration)
        {
            // Transform values
            using ListTransformHelper<TranslatedEnumConstant> newValues = new(declaration.Values);
            foreach (TranslatedEnumConstant value in declaration.Values)
            {
                Assumes.IsSealed<TranslatedEnumConstant>();
                newValues.Add(TransformEnumConstant(context, value));

                // In theory we could handle this situation by returning the other declarations as siblings of the enum, but for now we consider it invalid.
                if (newValues.HasOtherDeclarations)
                { throw new InvalidOperationException("Tried to transform an enum constant into something other than an enum constant in the context of an enum."); }
            }

            // If the enum changed, mutate it
            if (newValues.WasChanged)
            {
                return declaration with
                {
                    Values = newValues.ToImmutable()
                };
            }
            else
            { return declaration; }
        }
    }
}
