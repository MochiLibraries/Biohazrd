using ClangSharp.Pathogen;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Biohazrd
{
    partial class TranslatedLibraryBuilder
    {
        private static Exception MakeExceptionForMissingClangSharpPathogenNativeRuntime()
        {
            // Get an inner exception from the runtime (which may have additional details)
            Exception? innerException = null;
            {
                try
                { LibClangSharpResolver.VerifyResolverWasUsed(); }
                catch (Exception resolveException)
                { innerException = resolveException; }

                Debug.Assert(innerException is not null, "LibClangSharpResolver.TryLoadExplicitly fails we expect LibClangSharpResolver.VerifyResolverWasUsed to throw.");
            }

            // Figure out the "basic" runtime identifier for this platform
            // (RuntimeInformation.RuntimeIdentifier is too specific, it's something like win10-x64 or ubuntu.20.04-arm64.)
            string? runtimeIdentifier;
            string? friendlyName;
            {
                // The use of OSArchitecture is intentional here. We'll recommend the developer use the more appropriate runtime if they're running 32 bit on a 64 bit OS.
                (string? architectureName, string? friendlyArchitecture) = RuntimeInformation.OSArchitecture switch
                {
                    Architecture.X64 => ("x64", "x64"),
                    Architecture.Arm64 => ("arm64", "ARM64"),
                    Architecture.Arm => ("arm", "ARM32"),
                    Architecture.X86 => ("x86", "x86"),
                    _ => (null, null)
                };

                if (architectureName is null)
                { friendlyName = runtimeIdentifier = RuntimeInformation.RuntimeIdentifier; }
                else if (OperatingSystem.IsWindows())
                {
                    runtimeIdentifier = $"win-{architectureName}";
                    friendlyName = $"Windows {friendlyArchitecture}";
                }
                else if (OperatingSystem.IsLinux())
                {
                    runtimeIdentifier = $"linux-{architectureName}";
                    friendlyName = $"Linux {friendlyArchitecture}";
                }
                else if (OperatingSystem.IsMacOS())
                {
                    runtimeIdentifier = $"osx-{architectureName}";
                    friendlyName = $"macOS {friendlyArchitecture}";
                }
                else
                { friendlyName = runtimeIdentifier = RuntimeInformation.RuntimeIdentifier; }
            }

            // Figure out if support for this platform is provided by our NuGet packages
            bool isSupportedPlatform = runtimeIdentifier switch
            {
                "win-x64" => true,
                "linux-x64" => true,
                "linux-arm64" => true,
                "osx-x64" => true,
                _ => false
            };

            // Figure out if this NuGet package would've been referenced by default with our official packages
            bool isEnabledByDefault = runtimeIdentifier is "win-x64" or "linux-x64";

            // The runtime identifier should never be null or empty, but we check for the sake of making error messages legible in the event something really weird is happening
            if (runtimeIdentifier is null or "")
            {
                Debug.Fail("Runtime identifier should never be null or empty!");
                isSupportedPlatform = false;
                isEnabledByDefault = false;
                friendlyName = runtimeIdentifier = "unknown-platform";
            }

            // Figure out the package the developer needs to reference
            string packageToReference = $"ClangSharp.Pathogen.Native.{runtimeIdentifier}";
            if (typeof(LibClangSharpResolver).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is string clangSharpPathogenVersion)
            {
                int metadataSeparatorIndex = clangSharpPathogenVersion.IndexOf('+');
                if (metadataSeparatorIndex != -1)
                { clangSharpPathogenVersion = clangSharpPathogenVersion.Substring(0, metadataSeparatorIndex); }

                packageToReference += $" version [{clangSharpPathogenVersion}]";
            }

            // Build the exception message
            string message = "Could not load ClangSharp.Pathogen's native runtime.";

            // Handle developer running with a 32-bit runtime on a supported 64-bit OS
            if (isSupportedPlatform && RuntimeInformation.OSArchitecture != RuntimeInformation.ProcessArchitecture)
            {
                message += " You can resolve this issue by running this application using a 64-bit .NET runtime";

#if BIOHAZRD_IS_OFFICIAL_PACKAGE
                if (!isEnabledByDefault)
                { message += $" and adding a NuGet reference to {packageToReference}"; }
#endif

                message += ".";

                return new PlatformNotSupportedException(message, innerException);
            }

            // Handle developer running on a supported platform with a default-enabled platform
#if BIOHAZRD_IS_OFFICIAL_PACKAGE
            if (isSupportedPlatform && isEnabledByDefault)
            {
                message += $" This should not happen on {friendlyName} when using the official Biohazrd packages. The inner exception may provide more details.";
                return new PlatformNotSupportedException(message, innerException);
            }
#endif

            // Handle developer running on a supported platform
            if (isSupportedPlatform)
            {
                message += $" You can resolve this issue by adding a NuGet reference to {packageToReference}";

                // Make sure the developer knows macOS isn't fully supported yet
                if (OperatingSystem.IsMacOS())
                { message += ", although keep in mind that macOS is not fully supported by Biohazrd yet. Consider sponsoring if macOS support is important to you: https://github.com/sponsors/PathogenDavid"; }
                else
                { message += "."; }

                return new PlatformNotSupportedException(message, innerException);
            }

            // Handle developer running on an unsupported platform
            // I considered having generating a permalink here using the Git commit hash this assembly was built with, but I think it's probably better for the developer to get the latest
            // version of this document in case support for thier platform was added in newer versions of Biohazrd.
            message += $" Unfortunately Biohazrd does not currently provide a native runtime for {friendlyName} yet,"
                + $" see https://github.com/MochiLibraries/Biohazrd/blob/main/docs/PlatformSupport.md for details."
            ;

            // Suggest using emulation capabilities and/or sponsoring if the developer is running on one of those shiny newfangled ARM computerators that we can't support
            if (RuntimeInformation.OSArchitecture is Architecture.Arm64)
            {
                if (OperatingSystem.IsMacOS())
                {
                    message += " You might also be able to use Biohazrd on Apple Silicon via Rosetta 2."
                        + " (If you're interested in using Biohazrd on Apple Silicon devices, consider sponsoring development as we don't have access to the relevant hardware for development or CI: "
                        + "https://github.com/sponsors/PathogenDavid )"
                    ;
                }
                else if (OperatingSystem.IsWindows())
                {
                    message += " You might also be able to use Biohazrd on Windows on ARM via x64 emulation on Windows 11 or Windows 10 insider."
                        + " (If you're interested in using Biohazrd on Windows on ARM, consider sponsoring development as we don't have access to the relevant hardware for development or CI: "
                        + "https://github.com/sponsors/PathogenDavid )"
                    ;
                }
            }

            return new PlatformNotSupportedException(message, innerException);
        }
    }
}
