using Biohazrd.Transformation;
using System.Collections.Immutable;
using System.Diagnostics;

namespace Biohazrd.CSharp
{
    internal sealed class CSharpTranslationVerifierPass2 : CSharpTransformationBase
    {
        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            // Remove default parameter values for situations where C# doesn't allow them (IE: when a defaulted parameter is followed by one which isn't.)
            // This has to be in the 2nd pass because the first pass removes unsupported default parameter values.
            TranslatedParameter? lastNonDefaultParameter = null;
            bool haveDefaultParameter = false;
            bool haveDefaultParametersThatMustBeRemoved = false;

            foreach (TranslatedParameter parameter in declaration.Parameters)
            {
                if (parameter.DefaultValue is not null)
                { haveDefaultParameter = true; }
                else
                {
                    lastNonDefaultParameter = parameter;

                    // If we just found a non-defaulted parameter when we've seen a defaulted one, we'll need to remove some
                    if (haveDefaultParameter)
                    { haveDefaultParametersThatMustBeRemoved = true; }
                }
            }

            if (!haveDefaultParametersThatMustBeRemoved)
            { return declaration; }

            Debug.Assert(lastNonDefaultParameter is not null, "There must be a last non-default parameter by this point.");

            // Make new parameter list without illegal defaults
            ImmutableArray<TranslatedParameter>.Builder newParameters = declaration.Parameters.ToBuilder();
            int i = 0;
            foreach (TranslatedParameter parameter in declaration.Parameters)
            {
                // Once we've found the last non-defaulted, we're done modifying the list
                if (ReferenceEquals(parameter, lastNonDefaultParameter))
                { break; }

                if (parameter.DefaultValue is not null)
                {
                    //TODO: Technically this isn't necessary during verification anymore as it happens automatically during trampoline emit.
                    // However you don't get any warnings when it happens during trampoline emit so let's keep it for now.
                    newParameters[i] = parameter with
                    {
                        DefaultValue = null,
                        Diagnostics = newParameters[i].Diagnostics.Add
                        (
                            Severity.Warning,
                            $"Dropped default parameter value '{parameter.DefaultValue}' because parameter comes before non-defaulted parameter '{lastNonDefaultParameter.Name}'."
                        )
                    };
                }

                i++;
            }

            // Return the modified function
            return declaration with
            {
                Parameters = newParameters.MoveToImmutable()
            };
        }
    }
}
