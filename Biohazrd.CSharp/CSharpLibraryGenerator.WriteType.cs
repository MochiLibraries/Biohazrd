using Biohazrd.CSharp.Infrastructure;
using ClangSharp.Pathogen;
using System.Runtime.InteropServices;
using static Biohazrd.CSharp.CSharpCodeWriter;

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
            => Writer.Write(GetTypeAsString(context, declaration, type));

        /// <summary>Gets the specified type reference as a string which can be written directly to <see cref="Writer"/></summary>
        /// <remarks>
        /// When the type cannot be written, an error will be written to <see cref="Writer"/> using <see cref="Fatal"/>.
        /// As such, you should call this method at a time when it will make sense for that error to appear.
        /// </remarks>
        private string GetTypeAsString(VisitorContext context, TranslatedDeclaration declaration, TypeReference type)
        {
            switch (type)
            {
                case VoidTypeReference voidType:
                    return "void";
                case CSharpBuiltinTypeReference cSharpBuiltin:
                    return cSharpBuiltin.Type.CSharpKeyword;
                case PointerTypeReference pointer:
                    return $"{GetTypeAsString(context, declaration, pointer.Inner)}*";
                case TranslatedTypeReference translatedType:
                {
                    VisitorContext referencedContext;
                    TranslatedDeclaration? referenced = translatedType.TryResolve(context.Library, out referencedContext);

                    if (referenced is null)
                    {
                        Fatal(context, declaration, $"Failed to resolve {translatedType} during emit time.");
                        return "int";
                    }
                    // If the reference is to an enum translated as loose constants, we write out the underlying type instead
                    else if (referenced is TranslatedEnum { TranslateAsLooseConstants: true } referencedEnum)
                    {
                        return GetTypeAsString(context, declaration, referencedEnum.UnderlyingType);
                    }
                    // If the reference is to a custom C# declaration, allow it to override how it is referenced.
                    // Note that GetReferenceTypeAsString is allowed to have side-effects (namely adding usings) and still return null to indicate the normal emit logic should run.
                    // As such, this should be the final check before the standard emit logic runs.
                    else if (referenced is ICustomCSharpTranslatedDeclaration cSharpDeclaration && cSharpDeclaration.GetReferenceTypeAsString(this, context, declaration) is string customResult)
                    { return customResult; }
                    else
                    {
                        string result = "";
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

                            result += SanitizeIdentifier(parentDeclaration.Name);
                            result += '.';
                        }

                        result += SanitizeIdentifier(referenced.Name);
                        return result;
                    }
                }
                case FunctionPointerTypeReference functionPointer:
                {
                    string errorMessage;
                    CallingConvention callingConvention = functionPointer.CallingConvention.ToDotNetCallingConvention(out errorMessage);

                    if (errorMessage is not null)
                    {
                        Fatal(context, declaration, errorMessage);
                        return "void*";
                    }
                    else
                    {
                        string? callingConventionString = callingConvention switch
                        {
                            CallingConvention.Cdecl => "unmanaged[Cdecl]",
                            CallingConvention.StdCall => "unmanaged[Stdcall]",
                            CallingConvention.ThisCall => "unmanaged[Thiscall]",
                            CallingConvention.FastCall => "unmanaged[Fastcall]",
                            _ => null
                        };

                        if (callingConventionString is null)
                        {
                            Fatal(context, declaration, $"The {callingConvention} convention is not supported.");
                            return "void*";
                        }

                        string functionPointerResult = $"delegate* {callingConventionString}<";

                        // This is a workaround for the fact that VTable function pointers have been modified to add the implicit this and retbuf parameters
                        // The retbuf parameter is expected to be translated as a C# out parameter, but we have no way to express this in FunctionPointerTypeReference.
                        bool __HACK__emitSecondParameterAsOut = declaration is TranslatedVTableEntry { MethodDeclaration: not null } vTableEntry
                            // https://github.com/dotnet/roslyn/issues/47676
                            && vTableEntry.MethodDeclaration!.ReturnType.MustBePassedByReference();

                        int i = 0;
                        foreach (TypeReference parameterType in functionPointer.ParameterTypes)
                        {
                            if (__HACK__emitSecondParameterAsOut && i == 1)
                            { functionPointerResult += "out "; }

                            functionPointerResult += GetTypeAsString(context, declaration, parameterType);
                            functionPointerResult += ", ";
                            i++;
                        }

                        functionPointerResult += GetTypeAsString(context, declaration, functionPointer.ReturnType);

                        functionPointerResult += '>';
                        return functionPointerResult;
                    }
                }
                case ICustomCSharpTypeReference customCSharpTypeReference:
                    return customCSharpTypeReference.GetTypeAsString(this, context, declaration);
                default:
                    //TODO: It'd be nice if this was some sort of marker type to indicate its unusability.
                    Fatal(context, declaration, $"{type.GetType().Name} is not supported by the C# output generator.");
                    return "int";
            }
        }
    }
}
