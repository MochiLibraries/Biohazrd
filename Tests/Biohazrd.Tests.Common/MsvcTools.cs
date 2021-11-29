using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit.Sdk;

namespace Biohazrd.Tests.Common
{
    public static partial class MsvcTools
    {
        private static MsvcLocator MsvcLocator = new();

        private static void RunTool(string fileName, params string[] arguments)
        {
            string msvcToolchainRoot = MsvcLocator.LocateVisualStudio();
            string fullPath = Path.Combine(msvcToolchainRoot, fileName);

            if (!File.Exists(fullPath))
            { throw new FailException($"Could not find required MSVC tool '{fileName}' at '{fullPath}'"); }

            Process toolProcess = Process.Start(fullPath, arguments);
            toolProcess.WaitForExit();

            if (toolProcess.ExitCode != 0)
            { throw new ToolProcessFailureException(toolProcess); }
        }

        public static void Lib(params string[] arguments)
            => RunTool("lib.exe", arguments);

        public static void Lib(IEnumerable<string> arguments)
            => Lib(arguments.ToArray());

        public static void Cl(params string[] arguments)
            => RunTool("cl.exe", arguments);

        public static void Cl(IEnumerable<string> arguments)
            => Cl(arguments.ToArray());
    }
}
