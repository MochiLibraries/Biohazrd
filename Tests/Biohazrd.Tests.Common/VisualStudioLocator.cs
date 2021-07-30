using Microsoft.VisualStudio.Setup.Configuration;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Biohazrd.Tests.Common
{
    internal class VisualStudioLocator
    {
        private string? VisualStudioRoot = null;
        private Exception? VisualStudioLocationFailureException = null;
        private readonly string RequiredComponentName;

        public VisualStudioLocator(string requiredComponentName)
            => RequiredComponentName = requiredComponentName;

        public string LocateVisualStudio()
        {
            if (VisualStudioRoot is not null)
            { return VisualStudioRoot; }

            if (VisualStudioLocationFailureException is not null)
            { throw VisualStudioLocationFailureException; }

            if (!OperatingSystem.IsWindows())
            { throw VisualStudioLocationFailureException = new PlatformNotSupportedException($"{nameof(VisualStudioLocator)} is only supported on Windows."); }

            try
            {
                ISetupInstance visualStudioInstance;

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

                        // Skip Visual Studio installations which do not have the requested component installed
                        if (instance is ISetupInstance2 instance2)
                        {
                            if (!instance2.GetPackages().Any(p => p.GetId() == RequiredComponentName))
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
                    { throw new Exception("No instances of Visual Studio with the MSVC Toolchain were found."); }

                    visualStudioInstance = newestInstance;
                }
                catch (COMException ex) when (ex.HResult == unchecked((int)0x80040154)) // REGDB_E_CLASSNOTREG
                { throw new Exception($"Could not locate the Visual Studio setup configuration COM service. {ex}"); }
                catch (Exception ex) when (!Debugger.IsAttached)
                { throw new Exception($"An exception ocurred while attempting to locate Visual Studio installs: {ex}"); }

                return VisualStudioRoot = FindSpecificToolPath(visualStudioInstance);
            }
            catch (Exception ex) when (!Debugger.IsAttached) // Cache the exception so it can be re-thrown for subsequent requests
            {
                VisualStudioLocationFailureException = ex;
                throw;
            }
        }

        protected virtual string FindSpecificToolPath(ISetupInstance visualStudioInstance)
            => visualStudioInstance.ResolvePath();
    }
}
