using Microsoft.VisualStudio.Setup.Configuration;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Biohazrd.Tests.Common
{
    internal class MsvcLocator : VisualStudioLocator
    {
        private static string GetMsvcToolsComponentName()
            => RuntimeInformation.OSArchitecture switch
            {
                // Need to check the folder names for the ARM since it's not clear if ARM-native tools are provided.
                // (They probably aren't considering Visual Studio is not officially supported on ARM devices.)
                //Architecture.Arm => "Microsoft.VisualStudio.Component.VC.Tools.ARM",
                //Architecture.Arm64 => "Microsoft.VisualStudio.Component.VC.Tools.ARM64",
                Architecture.X86 => "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
                Architecture.X64 => "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
                _ => throw new PlatformNotSupportedException($"{RuntimeInformation.OSArchitecture} is not supported.")
            };

        public MsvcLocator()
            : base(GetMsvcToolsComponentName())
        { }

        protected override string FindSpecificToolPath(ISetupInstance visualStudioInstance)
        {
            string vcPath = visualStudioInstance.ResolvePath("VC");
            string defaultToolsVersionPath = Path.Combine(vcPath, "Auxiliary", "Build", "Microsoft.VCToolsVersion.default.txt");

            if (!File.Exists(defaultToolsVersionPath))
            { throw new FileNotFoundException($"Could not find default VC tools version for {visualStudioInstance.GetInstallationName()}"); }

            string defaultToolsVersion = File.ReadAllText(defaultToolsVersionPath).Trim();

            string msvcToolchainRoot = Path.Combine(vcPath, "Tools", "MSVC", defaultToolsVersion, "bin");

            switch (RuntimeInformation.OSArchitecture)
            {
                case Architecture.X86:
                    msvcToolchainRoot = Path.Combine(msvcToolchainRoot, "Hostx86", "x86");
                    break;
                case Architecture.X64:
                    msvcToolchainRoot = Path.Combine(msvcToolchainRoot, "Hostx64", "x64");
                    break;
                default:
                    throw new PlatformNotSupportedException($"{RuntimeInformation.OSArchitecture} is not supported.");
            }

            if (!Directory.Exists(msvcToolchainRoot))
            { throw new DirectoryNotFoundException($"Could not locate MSVC toolchain directory at expected path '{msvcToolchainRoot}'"); }

            return msvcToolchainRoot;
        }
    }
}
