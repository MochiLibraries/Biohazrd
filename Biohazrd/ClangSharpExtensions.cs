using Biohazrd.Expressions;
using ClangSharp;
using ClangSharp.Interop;
using ClangSharp.Pathogen;
using System;
using System.Diagnostics;
using System.Text;
using ClangType = ClangSharp.Type;

namespace Biohazrd
{
    public static class ClangSharpExtensions
    {
        public static AccessModifier ToTranslationAccessModifier(this CX_CXXAccessSpecifier accessSpecifier)
            => accessSpecifier switch
            {
                CX_CXXAccessSpecifier.CX_CXXPublic => AccessModifier.Public,
                CX_CXXAccessSpecifier.CX_CXXProtected => AccessModifier.Private, // Protected is not really supported for translation.
                CX_CXXAccessSpecifier.CX_CXXPrivate => AccessModifier.Private,
                _ => AccessModifier.Public // The access specifier is invalid for declarations which aren't members of a record, so they are translated as public.
            };

        internal static Cursor FindCursor(this TranslationUnit translationUnit, CXCursor handle)
        {
            if (handle.IsNull)
            { throw new ArgumentException("The specified cursor handle is null.", nameof(handle)); }

#if DEBUG
            if (handle.TranslationUnit != translationUnit.Handle)
            { throw new ArgumentException("The specified cursor handle comes from an unrelated ClangSharp translation unit.", nameof(handle)); }
#endif

            Cursor ret = translationUnit.GetOrCreate(handle);
            Debug.Assert(ret is not null);
            return ret;
        }

        internal static ClangType FindType(this TranslationUnit translationUnit, CXType handle)
        {
            if (handle.kind == CXTypeKind.CXType_Invalid)
            { throw new ArgumentException("The specified type handle is invalid.", nameof(handle)); }

            ClangType ret = translationUnit.GetOrCreate(handle);
            Debug.Assert(ret is not null);
            return ret;
        }

        public unsafe static ConstantValue? TryComputeConstantValue(this CXCursor cursor, out TranslationDiagnostic? diagnostic)
        {
            bool success = false;
            byte* error;
            PathogenConstantValueInfo info;

            try
            {
                success = PathogenExtensions.pathogen_ComputeConstantValue(cursor, &info, &error);
                if (!success)
                {
                    if (error is null)
                    {
                        diagnostic = null;
                        return null;
                    }

                    StringBuilder messageBuilder = new();
                    messageBuilder.Append($"Failed to compute constant: ");

                    for (; *error != 0; error++)
                    { messageBuilder.Append((char)*error); }

                    diagnostic = new TranslationDiagnostic(Severity.Warning, messageBuilder.ToString());
                    return null;
                }

                ConstantValue value = info.ToConstantExpression();

                // Since this type isn't very useful, we just turn it into a diagnostic
                if (value is UnsupportedConstantExpression unsupportedValue)
                {
                    diagnostic = new TranslationDiagnostic(Severity.Warning, $"Unsupported constant: {unsupportedValue.Message}");
                    return null;
                }

                diagnostic = null;
                return value;
            }
            finally
            {
                if (success)
                { PathogenExtensions.pathogen_DeletePathogenConstantValueInfo(&info); }
            }
        }

        public static ConstantValue? TryComputeConstantValue(this VarDecl declaration, out TranslationDiagnostic? diagnostic)
            => TryComputeConstantValue(declaration.Handle, out diagnostic);

        public static ConstantValue? TryComputeConstantValue(this Expr expression, out TranslationDiagnostic? diagnostic)
            => TryComputeConstantValue(expression.Handle, out diagnostic);
    }
}
