using System;
using Xunit;

namespace Biohazrd.Tests.Common
{
    public sealed class LinuxFactAttribute : FactAttribute
    {
        public LinuxFactAttribute()
        {
            if (Skip is null && !OperatingSystem.IsLinux())
            { Skip = "This test can only be executed on Linux."; }
        }
    }
}
