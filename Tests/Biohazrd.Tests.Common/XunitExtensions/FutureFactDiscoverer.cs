using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Biohazrd.Tests.Common.XunitExtensions
{
    public sealed class FutureFactDiscoverer : IXunitTestCaseDiscoverer
    {
        private readonly IMessageSink DiagnosticMessageSink;

        public FutureFactDiscoverer(IMessageSink diagnosticMessageSink)
            => DiagnosticMessageSink = diagnosticMessageSink;

        public IEnumerable<IXunitTestCase> Discover(ITestFrameworkDiscoveryOptions discoveryOptions, ITestMethod testMethod, IAttributeInfo factAttribute)
        {
            yield return new FutureFactTestCase(DiagnosticMessageSink, discoveryOptions.MethodDisplayOrDefault(), discoveryOptions.MethodDisplayOptionsOrDefault(), testMethod);
        }
    }
}
