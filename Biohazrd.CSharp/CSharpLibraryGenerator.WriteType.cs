using ClangSharp.Pathogen;
using System.Runtime.InteropServices;

namespace Biohazrd.CSharp
{
    partial class CSharpLibraryGenerator
    {
        private void WriteTypeAsReference(VisitorContext context, TranslatedDeclaration declaration, TypeReference type)
        {
            WriteType(context, declaration, type);
            Writer.Write('*');
        }

        private void WriteType(VisitorContext context, TranslatedDeclaration declaration, TypeReference type)
        {
            switch (type)
            {
                case VoidTypeReference voidType:
                    Writer.Write("void");
                    return;
                case CSharpBuiltinTypeReference cSharpBuiltin:
                    Writer.Write(cSharpBuiltin.Type.CSharpKeyword);
                    return;
                case PointerTypeReference pointer:
                {
                    WriteType(context, declaration, pointer.Inner);
                    Writer.Write('*');
                }
                return;
                case TranslatedTypeReference translatedType:
                {
                    VisitorContext referencedContext;
                    TranslatedDeclaration? referenced = translatedType.TryResolve(context.Library, out referencedContext);

                    if (referenced is null)
                    {
                        Writer.Write("int");
                        Fatal(context, declaration, $"Failed to resolve {translatedType} during emit time.");
                    }
                    // If the reference is to an enum translated as loose constants, we write out the underlying type instead
                    else if (referenced is TranslatedEnum { TranslateAsLooseConstants: true } referencedEnum)
                    {
                        WriteType(context, declaration, referencedEnum.UnderlyingType);
                    }
                    else
                    {
                        int i = 0;
                        bool canSkipCommonParents = true;
                        foreach (TranslatedDeclaration parentDeclaration in referencedContext.Parents)
                        {
                            // Skip the portion of the referenced context which matches our current context
                            if (canSkipCommonParents && i < context.Parents.Length && ReferenceEquals(parentDeclaration, context.Parents[i]))
                            {
                                i++;
                                continue;
                            }

                            canSkipCommonParents = false;

                            // Skip enums translated as loose constants since they are not represented in the output
                            if (parentDeclaration is TranslatedEnum { TranslateAsLooseConstants: true })
                            { continue; }

                            Writer.WriteIdentifier(parentDeclaration.Name);
                            Writer.Write('.');
                        }

                        Writer.WriteIdentifier(referenced.Name);
                    }
                }
                return;
                case FunctionPointerTypeReference functionPointer:
                {
                    //TODO: Rework the calling convention to match the final C#9 syntax
                    string errorMessage;
                    CallingConvention callingConvention = functionPointer.CallingConvention.ToDotNetCallingConvention(out errorMessage);

                    if (errorMessage is not null)
                    {
                        Fatal(context, declaration, errorMessage);
                        Writer.Write("void*");
                    }
                    else
                    {
                        Writer.Write($"delegate* {callingConvention.ToString().ToLowerInvariant()}<");

                        foreach (TypeReference parameterType in functionPointer.ParameterTypes)
                        {
                            WriteType(context, declaration, parameterType);
                            Writer.Write(", ");
                        }

                        WriteType(context, declaration, functionPointer.ReturnType);

                        Writer.Write('>');
                    }
                }
                return;
                default:
                    //TODO: It'd be nice if this was some sort of marker type to indicate its unusability.
                    Writer.Write("int");
                    Fatal(context, declaration, $"{type.GetType().Name} is not supported by the C# output generator.");
                    return;
            }
        }
    }
}
