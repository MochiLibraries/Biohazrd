using ClangSharp.Pathogen;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;

namespace Biohazrd
{
    public sealed class TranslatedMacro
    {
        public TranslatedFile File { get; }
        public string Name { get; }
        public bool WasUndefined { get; }
        public bool IsFunctionLike { get; }
        public ImmutableArray<string> ParameterNames { get; }
        public bool LastParameterIsVardic { get; }
        public bool IsUsedForHeaderGuard { get; }
        public bool HasValue { get; }

        /// <summary>The macro's value (if it has one) as C/C++ code.</summary>
        /// <remarks>You should not try to parse this for evaluating constants, use <see cref="TranslatedLibraryConstantEvaluator"/>.</remarks>
        public string? RawValueSourceString { get; }

        internal unsafe TranslatedMacro(TranslatedFile file, PathogenMacroInformation* macroInfo)
        {
            File = file;
            Name = macroInfo->Name;
            WasUndefined = macroInfo->WasUndefined;
            IsFunctionLike = macroInfo->IsFunctionLike;
            IsUsedForHeaderGuard = macroInfo->IsUsedForHeaderGuard;
            HasValue = macroInfo->TokenCount != 0;
            RawValueSourceString = HasValue ? macroInfo->RawValueSourceString : null;

            if (IsFunctionLike)
            {
                int effectiveParameterCount = macroInfo->ParameterCount;

                ImmutableArray<string>.Builder parameterNamesBuilder = ImmutableArray.CreateBuilder<string>(effectiveParameterCount);

                for (int i = 0; i < macroInfo->ParameterCount; i++)
                { parameterNamesBuilder.Add(macroInfo->GetParameterName(i)); }

                int lastParameterIndex = parameterNamesBuilder.Count - 1;

                switch (macroInfo->VardicKind)
                {
                    case PathogenMacroVardicKind.None:
                        LastParameterIsVardic = false;
                        break;
                    case PathogenMacroVardicKind.C99:
                        if (parameterNamesBuilder[lastParameterIndex] == "__VA_ARGS__")
                        { parameterNamesBuilder[lastParameterIndex] = "..."; }
                        else
                        { Debug.Assert(false, "The last parameter of a C99 vardic macro should be __VA_ARGS__"); }

                        LastParameterIsVardic = true;
                        break;
                    case PathogenMacroVardicKind.Gnu:
                        // GNU-style vardic parameters are named, we include the `...` for the sake of consistency.
                        parameterNamesBuilder[parameterNamesBuilder.Count - 1] += "...";
                        LastParameterIsVardic = true;
                        break;
                    default:
                        throw new NotSupportedException($"'{macroInfo->VardicKind}'-style vardic macros are not supported.");
                }

                ParameterNames = parameterNamesBuilder.MoveToImmutable();
            }
            else
            {
                Debug.Assert(macroInfo->ParameterCount == 0, "Only function-like macros should have parameters.");
                Debug.Assert(macroInfo->VardicKind == PathogenMacroVardicKind.None, "Only function-like macros can be vardic.");
                ParameterNames = ImmutableArray<string>.Empty;
                LastParameterIsVardic = false;
            }
        }

        public override string ToString()
        {
            if (!IsFunctionLike)
            { return Name; }

            StringBuilder result = new(Name.Length + 2 + ParameterNames.Length * 3);
            result.Append(Name);
            result.Append('(');

            bool first = true;
            foreach (string parameterName in ParameterNames)
            {
                if (first)
                { first = false; }
                else
                { result.Append(", "); }

                result.Append(parameterName);
            }

            result.Append(')');
            return result.ToString();
        }
    }
}
