using System.Runtime.InteropServices;

namespace Biohazrd.CSharp.Metadata
{
    /// <summary>The presence of this metadata on a function indicates <see cref="DllImportAttribute.SetLastError"/> will be set for the corresponding P/Invoke.</summary>
    /// <remarks>This metadata item has no affect on virtual methods.</remarks>
    public struct SetLastErrorFunction : IDeclarationMetadataItem
    { }
}
