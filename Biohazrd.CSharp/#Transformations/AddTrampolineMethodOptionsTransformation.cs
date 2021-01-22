using Biohazrd.CSharp.Metadata;
using Biohazrd.Transformation;
using System.Runtime.CompilerServices;

namespace Biohazrd.CSharp
{
    /// <summary>Adds <see cref="MethodImplOptions"/> to all functions in the library</summary>
    /// <remarks>
    /// Adds <see cref="TrampolineMethodImplOptions"/> to all functions in the library to indicate <see cref="MethodImplOptions"/> to be applied to their trampolines methods (in one is emitted.)
    ///
    /// Any existing <see cref="TrampolineMethodImplOptions"/> metadata is updated to include the new options.
    /// </remarks>
    public sealed class AddTrampolineMethodOptionsTransformation : TransformationBase
    {
        public MethodImplOptions OptionsToAdd { get; }

        public AddTrampolineMethodOptionsTransformation(MethodImplOptions optionsToAdd)
            => OptionsToAdd = optionsToAdd;

        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            MethodImplOptions options = OptionsToAdd;

            if (declaration.Metadata.TryGet(out TrampolineMethodImplOptions oldOptions))
            { options |= oldOptions.Options; }

            return declaration with
            {
                Metadata = declaration.Metadata.Set(new TrampolineMethodImplOptions(options))
            };
        }
    }
}
