using System;

namespace Biohazrd.Tests.Common
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class RelatedIssueAttribute : Attribute
    {
        public string Url { get; }

        public RelatedIssueAttribute(string url)
            => Url = url;
    }
}
