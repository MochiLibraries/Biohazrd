using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Biohazrd.Tests.Common.XunitExtensions
{
    internal sealed class FutureFactTestCase : XunitTestCase
    {
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public FutureFactTestCase()
        { }

        public FutureFactTestCase(IMessageSink diagnosticMessageSink, TestMethodDisplay defaultMethodDisplay, TestMethodDisplayOptions defaultMethodDisplayOptions, ITestMethod testMethod, object[]? testMethodArguments = null)
            : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod, testMethodArguments)
        { }

        protected override string GetDisplayName(IAttributeInfo factAttribute, string displayName)
        {
            // Prefix the names of FutureFact tests so they stand out as speical.
            string result = base.GetDisplayName(factAttribute, displayName);

            // Naive prefixing interferes with the Test Explorer's ability to strip off the namespace portion of the test name, so we strip it off ourselves.
            // (This check may not be prefect, but it's probably good enough.)
            if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("VisualStudioEdition")))
            {
                int lastDot = result.LastIndexOf('.');
                if (lastDot > 0)
                { result = result.Substring(lastDot + 1); }
            }

            return $"🔮 {result}";
        }

        public override async Task<RunSummary> RunAsync
        (
            IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            object[] constructorArguments,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource
        )
        {
            FutureFactMessageBus futureFactMessageBus = new(messageBus);
            RunSummary result = await base.RunAsync(diagnosticMessageSink, futureFactMessageBus, constructorArguments, aggregator, cancellationTokenSource);

            // Tests which fail should succeed and vice-versa
            // (These summary values are not affected by the messages modified by our message bus proxy, so we need to fix them here.)
            int succeeded = result.Total - result.Failed - result.Skipped;
            result.Failed = succeeded;

            return result;
        }
    }
}
