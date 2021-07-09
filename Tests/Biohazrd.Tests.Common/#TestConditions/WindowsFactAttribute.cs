using System;
using Xunit;

namespace Biohazrd.Tests.Common
{
    public sealed class WindowsFactAttribute : FactAttribute
    {
        public WindowsFactAttribute()
        {
            if (Skip is null && !OperatingSystem.IsWindows())
            { Skip = "This test can only be executed on Windows."; }
        }
    }
}
