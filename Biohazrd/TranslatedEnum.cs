using ClangSharp;
using ClangSharp.Pathogen;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Biohazrd
{
    public sealed record TranslatedEnum : TranslatedDeclaration
    {
        public TypeReference UnderlyingType { get; init; }
        public ImmutableList<TranslatedEnumConstant> Values { get; init; }

        public bool TranslateAsLooseConstants { get; init; }
        public bool IsFlags { get; init; }

        internal TranslatedEnum(TranslatedFile file, EnumDecl enumDeclaration)
            : base(file, enumDeclaration)
        {
            UnderlyingType = new ClangTypeReference(enumDeclaration.IntegerType);

            // If this enum is anonymous and not an enum class, it will be translated as loose constants by default instead of a normal enum type
            //TODO: This isn't ideal when this anonymous enum is used to type a field. A more sensible default would probably be to name ourselves after the field. (IE: <FieldName>Enum)
            if (IsUnnamed && !enumDeclaration.IsClass)
            { TranslateAsLooseConstants = true; }

            // Enumerate all of the values for this enum
            ImmutableList<TranslatedEnumConstant>.Builder valuesBuilder = ImmutableList.CreateBuilder<TranslatedEnumConstant>();

            foreach (Cursor cursor in enumDeclaration.CursorChildren)
            {
                if (cursor is EnumConstantDecl enumConstant)
                { valuesBuilder.Add(new TranslatedEnumConstant(File, enumConstant)); }
                else
                { Diagnostics = Diagnostics.Add(Severity.Warning, cursor, $"Unexpected {cursor.CursorKindDetailed()} in enum definition."); }
            }

            Values = valuesBuilder.ToImmutable();
        }

        public override IEnumerator<TranslatedDeclaration> GetEnumerator()
            => Values.GetEnumerator();
    }
}
