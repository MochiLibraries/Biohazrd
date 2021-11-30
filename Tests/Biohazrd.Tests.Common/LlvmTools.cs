using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Biohazrd.Tests.Common
{
    public static partial class LlvmTools
    {
        private static string? ClangPath = null;
        private static Exception? ClangLocationFailureException = null;

        private static string? LlvmArPath = null;
        private static Exception? LlvmArLocationFailureException = null;

        private static string? LdLldPath = null;
        private static Exception? LdLldLocationFailureException = null;

        public const string ExplicitToolchainRootEnvironmentVariable = "BIOHAZRD_FULL_LLVM_TOOLCHAIN_PATH";

        private static string? TryFindLlvmTool(string friendlyName, string commandName, ref string? cachedPath, ref Exception? cachedException, out Exception? exception)
        {
            if (cachedPath is not null)
            {
                exception = null;
                return cachedPath;
            }

            if (cachedException is not null)
            {
                exception = cachedException;
                return null;
            }

            // If a LLVM toolchain is explicitly configured, use it instead of searching the system path
            if (Environment.GetEnvironmentVariable(ExplicitToolchainRootEnvironmentVariable) is string explicitToolchainRoot)
            {
                if (!Path.IsPathFullyQualified(explicitToolchainRoot))
                {
                    cachedException = exception = new InvalidOperationException($"LLVM toolchain provided by {ExplicitToolchainRootEnvironmentVariable} must be a fully-qualified path.");
                    return null;
                }

                string fileExtension = OperatingSystem.IsWindows() ? ".exe" : "";
                string explicitToolPath = Path.Combine(explicitToolchainRoot, "bin", $"{commandName}{fileExtension}");

                if (!File.Exists(explicitToolPath))
                {
                    cachedException = exception = new FileNotFoundException($"LLVM toolchain provided by {ExplicitToolchainRootEnvironmentVariable} is incomplete. '{explicitToolPath}' not found.");
                    return null;
                }

                exception = null;
                return explicitToolPath;
            }

            // It's not super clear if Win32Exception.NativeErrorCode is actually errno on Unix-like systems when Process.Start fails due to a missing executable,
            // but it doesn't actually matter since ERROR_FILE_NOT_FOUND and ENOENT are both 2.
            const int ERROR_FILE_NOT_FOUND = 2;

            // Check if the tool is present on the system PATH
            try
            {
                using Process toolProcess = Process.Start(commandName, "--version");
                toolProcess.WaitForExit();

                if (toolProcess.ExitCode == 0)
                {
                    exception = null;
                    return cachedPath = commandName;
                }
                else
                {
                    exception = new Exception($"The {friendlyName} found on the system PATH appears to be non-functional.");
                    cachedException = exception;
                    return null;
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ERROR_FILE_NOT_FOUND)
            { exception = new FileNotFoundException($"{friendlyName} was not found on the system PATH."); }
            catch (Exception ex)
            {
                exception = new Exception($"The {friendlyName} found on the system PATH appears to be unusable: {ex.Message}.", ex);
                cachedException = exception;
                return null;
            }

            // Find the tool from Visual Studio if the appropriate component is installed
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    // The other LLVM-related component (Microsoft.VisualStudio.Component.VC.Llvm.ClangToolset) is only for using clang-cl for building C++ MSBuild projects.
                    VisualStudioLocator locator = new("Microsoft.VisualStudio.Component.VC.Llvm.Clang");
                    string visualStudioRoot = locator.LocateVisualStudio();
                    string visualStudioToolPath = Path.Combine(visualStudioRoot, "VC", "Tools", "Llvm", "bin", $"{commandName}.exe");

                    if (!File.Exists(visualStudioToolPath))
                    { throw new FileNotFoundException($"Visual Studio install claims to have LLVM toolchain but {commandName}.exe was not found.", visualStudioToolPath); }

                    exception = null;
                    return cachedPath = visualStudioToolPath;
                }
                catch (Exception ex)
                {
                    if (exception is not null)
                    { exception = new AggregateException(exception, ex); }
                    else
                    { exception = ex; }
                }
            }

            // The tool is not installed
            cachedException = exception;
            return null;
        }

        private static string? TryFindClang(out Exception? exception)
            => TryFindLlvmTool("Clang", "clang", ref ClangPath, ref ClangLocationFailureException, out exception);

        public static Exception? IsClangAvailable()
        {
            string? path = TryFindClang(out Exception? exception);
            Debug.Assert(exception is not null || path is not null);
            return exception;
        }

        public static string GetClangPath()
        {
            string? path = TryFindClang(out Exception? exception);

            if (exception is not null)
            {
                Debug.Assert(path is null);
                throw exception;
            }

            Debug.Assert(path is not null);
            return path;
        }

        private static string? TryFindLlvmAr(out Exception? exception)
            => TryFindLlvmTool("LLVM Archiver (llvm-ar)", "llvm-ar", ref LlvmArPath, ref LlvmArLocationFailureException, out exception);

        public static Exception? IsLlvmArAvailable()
        {
            string? path = TryFindLlvmAr(out Exception? exception);
            Debug.Assert(exception is not null || path is not null);
            return exception;
        }

        public static string GetLlvmArPath()
        {
            string? path = TryFindLlvmAr(out Exception? exception);

            if (exception is not null)
            {
                Debug.Assert(path is null);
                throw exception;
            }

            Debug.Assert(path is not null);
            return path;
        }

        private static string? TryFindLdLld(out Exception? exception)
            => TryFindLlvmTool("LLVM ELF Linker (ld.lld)", "ld.lld", ref LdLldPath, ref LdLldLocationFailureException, out exception);

        public static Exception? IsLdLldAvailable()
        {
            string? path = TryFindLdLld(out Exception? exception);
            Debug.Assert(exception is not null || path is not null);
            return exception;
        }

        public static string GetLdLldPath()
        {
            string? path = TryFindLdLld(out Exception? exception);

            if (exception is not null)
            {
                Debug.Assert(path is null);
                throw exception;
            }

            Debug.Assert(path is not null);
            return path;
        }
    }
}
