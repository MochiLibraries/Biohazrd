using System.Runtime.InteropServices;

namespace Biohazrd.CSharp.Metadata
{
    /// <summary>The presence of this metadata on a function indicates <see cref="DllImportAttribute.SetLastError"/> will be set for the corresponding P/Invoke.</summary>
    /// <remarks>
    /// Unless targeting .NET 6 or later, this metadata item has no affect on virtual methods.
    ///
    /// This metadata must be applied prior to <see cref="CreateTrampolinesTransformation"/>.
    /// </remarks>
    public struct SetLastErrorFunction : IDeclarationMetadataItem
    {
        /// <summary>Skips clearing the last system error before the P/Invoke</summary>
        /// <remarks>
        /// Leaving this as <c>false</c> will clear the last system error before invoking the native function.
        /// This matches the behavior of the built-in .NET marshaler but is generally not needed.
        ///
        /// Only applicable when targeting .NET 6 or later.
        /// </remarks>
        public bool SkipPedanticClear { get; init; }
    }
}
