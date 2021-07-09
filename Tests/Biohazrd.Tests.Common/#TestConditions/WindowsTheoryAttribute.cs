using System;
using Xunit;

namespace Biohazrd.Tests.Common
{
    public sealed class WindowsTheoryAttribute : TheoryAttribute
    {
        public WindowsTheoryAttribute()
        {
            if (Skip is null && !OperatingSystem.IsWindows())
            { Skip = "This test can only be executed on Windows."; }
        }
    }
}
