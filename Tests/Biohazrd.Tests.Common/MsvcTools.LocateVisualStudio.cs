using Microsoft.VisualStudio.Setup.Configuration;
using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit.Sdk;

namespace Biohazrd.Tests.Common
{
    partial class MsvcTools
    {
        private static string? MsvcToolchainRoot = null;
        private static Exception? MsvcToolchainRootLocationFailureException = null;

        [MemberNotNull(nameof(MsvcToolchainRoot))]
        private static void LocateVisualStudio()
        {
            if (MsvcToolchainRoot is not null)
            { return; }

            if (MsvcToolchainRootLocationFailureException is not null)
            { throw MsvcToolchainRootLocationFailureException; }

            try
            {
                ISetupInstance visualStudioInstance;

                string msvcToolsComponentName = RuntimeInformation.OSArchitecture switch
                {
                    // Need to check the folder names for the ARM since it's not clear if ARM-native tools are provided.
                    // (They probably aren't considering Visual Studio is not officially supported on ARM devices.)
                    //Architecture.Arm => "Microsoft.VisualStudio.Component.VC.Tools.ARM",
                    //Architecture.Arm64 => "Microsoft.VisualStudio.Component.VC.Tools.ARM64",
                    Architecture.X86 => "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
                    Architecture.X64 => "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
                    _ => throw new PlatformNotSupportedException($"{RuntimeInformation.OSArchitecture} is not supported.")
                };

                try
                {
                    SetupConfiguration query = new();
                    IEnumSetupInstances enumInstances = query.EnumAllInstances();
                    ISetupHelper helper = (ISetupHelper)query;

                    ISetupInstance[] _instance = new ISetupInstance[1];

                    ISetupInstance? newestInstance = null;
                    ulong newestVersionNum = 0;

                    while (true)
                    {
                        int fetched;
                        enumInstances.Next(1, _instance, out fetched);

                        if (fetched == 0)
                        { break; }

                        ISetupInstance instance = _instance[0];

                        // Skip Visual Studio installations which do not have a MSVC toolchain installed
                        if (instance is ISetupInstance2 instance2)
                        {
                            if (!instance2.GetPackages().Any(p => p.GetId() == msvcToolsComponentName))
                            { continue; }
                        }
                        // If this instance is not a v2 instance and the newest instance is, don't consider this instance
                        else if (newestInstance is ISetupInstance2)
                        { continue; }

                        string versionString = instance.GetInstallationVersion();
                        ulong versionNum = helper.ParseVersion(versionString);

                        if (newestVersionNum == 0 || versionNum > newestVersionNum)
                        {
                            newestInstance = instance;
                            newestVersionNum = versionNum;
                        }
                    }

                    if (newestInstance is null)
                    { throw new FailException("No instances of Visual Studio with the MSVC Toolchain were found."); }

                    visualStudioInstance = newestInstance;
                }
                catch (COMException ex) when (ex.HResult == unchecked((int)0x80040154)) // REGDB_E_CLASSNOTREG
                { throw new FailException($"Could not locate the Visual Studio setup configuration COM service. {ex}"); }
                catch (Exception ex) when (!Debugger.IsAttached)
                { throw new FailException($"An exception ocurred while attempting to locate Visual Studio installs: {ex}"); }

                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Find toolchain root
                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                string vcPath = visualStudioInstance.ResolvePath("VC");
                string defaultToolsVersionPath = Path.Combine(vcPath, "Auxiliary", "Build", "Microsoft.VCToolsVersion.default.txt");

                if (!File.Exists(defaultToolsVersionPath))
                { throw new FailException($"Could not find default VC tools version for {visualStudioInstance.GetInstallationName()}"); }

                string defaultToolsVersion = File.ReadAllText(defaultToolsVersionPath).Trim();

                MsvcToolchainRoot = Path.Combine(vcPath, "Tools", "MSVC", defaultToolsVersion, "bin");

                switch (RuntimeInformation.OSArchitecture)
                {
                    case Architecture.X86:
                        MsvcToolchainRoot = Path.Combine(MsvcToolchainRoot, "Hostx86", "x86");
                        break;
                    case Architecture.X64:
                        MsvcToolchainRoot = Path.Combine(MsvcToolchainRoot, "Hostx64", "x64");
                        break;
                    default:
                        throw new PlatformNotSupportedException($"{RuntimeInformation.OSArchitecture} is not supported.");
                }

                if (!Directory.Exists(MsvcToolchainRoot))
                { throw new FailException($"Could not locate MSVC toolchain directory at expected path '{MsvcToolchainRoot}'"); }
            }
            catch (Exception ex) when (!Debugger.IsAttached) // Cache the exception so it can be re-thrown on subsequent MSVC-required tests
            {
                MsvcToolchainRootLocationFailureException = ex;
                throw;
            }
        }
    }
}
