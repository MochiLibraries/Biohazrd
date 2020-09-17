using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Biohazrd.CSharp
{
    internal sealed class FindAllNonNestedTypeDeclarationsVisitor : DeclarationVisitor
    {
        private List<TranslatedDeclaration> _NonNestedTypes = new();
        public ReadOnlyCollection<TranslatedDeclaration> NonNestedTypes { get; }

        public FindAllNonNestedTypeDeclarationsVisitor()
            => NonNestedTypes = _NonNestedTypes.AsReadOnly();

        // Since these visitors don't call the base methods, we won't recurse into nested records/enums
        protected override void VisitRecord(VisitorContext context, TranslatedRecord declaration)
            => _NonNestedTypes.Add(declaration);

        protected override void VisitEnum(VisitorContext context, TranslatedEnum declaration)
            => _NonNestedTypes.Add(declaration);
    }
}
