using Xunit;
using Xunit.Sdk;

namespace Biohazrd.Tests.Common
{
    /// <summary>An attribute which marks a test that intentionally fail as they represent an unimplemented feature.</summary>
    [XunitTestCaseDiscoverer("Biohazrd.Tests.Common.XunitExtensions.FutureFactDiscoverer", "Biohazrd.Tests.Common")]
    public sealed class FutureFactAttribute : FactAttribute
    { }
}
