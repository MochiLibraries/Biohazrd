using System.Runtime.CompilerServices;

namespace Biohazrd.CSharp.Metadata
{
    /// <summary>This metadata specifies <see cref="MethodImplOptions"/> to be used for any trampolines for functions.</summary>
    /// <remarks>This metadata has no affect on declarations other than <see cref="TranslatedFunction"/> or functions which do not require trampolines.</remarks>
    public struct TrampolineMethodImplOptions : IDeclarationMetadataItem
    {
        public MethodImplOptions Options { get; }

        public TrampolineMethodImplOptions(MethodImplOptions options)
            => Options = options;
    }
}
