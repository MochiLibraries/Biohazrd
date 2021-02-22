using Biohazrd.Transformation.Infrastructure;
using System;
using System.Diagnostics;
using System.Linq;

namespace Biohazrd.Transformation.Common
{
    public sealed class AutoNameUnnamedParametersTransformation : TransformationBase
    {
        private readonly string Prefix;

        public AutoNameUnnamedParametersTransformation(string prefix)
        {
            if (String.IsNullOrEmpty(prefix))
            { throw new ArgumentException("Prefix must not be null or empty.", nameof(prefix)); }

            Prefix = prefix;
        }

        public AutoNameUnnamedParametersTransformation()
            : this("arg")
        { }

        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            ArrayTransformHelper<TranslatedParameter> newParameters = new(declaration.Parameters);
            for (int i = 0; i < declaration.Parameters.Length; i++)
            {
                TranslatedParameter parameter = declaration.Parameters[i];

                if (parameter.IsUnnamed)
                {
                    string parameterName = $"{Prefix}{i}";

                    // Handle weird case where the automatic name conflicts with another parameter
                    while (declaration.Parameters.Any(p => p.Name == parameterName))
                    { parameterName = $"_{parameterName}"; }

                    parameter = parameter with { Name = parameterName };
                }

                newParameters.Add(parameter);
            }

            Debug.Assert(!newParameters.HasOtherDeclarations);
            if (newParameters.WasChanged)
            {
                return declaration with { Parameters = newParameters.MoveToImmutable() };
            }
            else
            { return declaration; }
        }
    }
}
