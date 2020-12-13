using System;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Biohazrd.Tests.Common.XunitExtensions
{
    internal sealed class FutureFactMessageBus : IMessageBus
    {
        private readonly IMessageBus InnerBus;

        public FutureFactMessageBus(IMessageBus innerBus)
            => InnerBus = innerBus;

        public bool QueueMessage(IMessageSinkMessage message)
        {
            // Flip test results
            switch (message)
            {
                case ITestPassed passed:
                    Exception passedException = new("This test represents an unimplemented feature and as such it should be failing.");
                    return InnerBus.QueueMessage(new TestFailed(passed.Test, passed.ExecutionTime, passed.Output, passedException));
                case ITestFailed failed:
                    return InnerBus.QueueMessage(new TestPassed(failed.Test, failed.ExecutionTime, failed.Output));
                default:
                    return InnerBus.QueueMessage(message);
            }
        }

        public void Dispose()
        { }
    }
}
