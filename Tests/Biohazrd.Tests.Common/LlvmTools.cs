using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace Biohazrd.Tests.Common
{
    public static partial class LlvmTools
    {
        private static string? ClangPath = null;
        private static Exception? LlvmToolchainRootLocationFailureException = null;

        private static string? TryFindClang(out Exception? exception)
        {
            if (ClangPath is not null)
            {
                exception = null;
                return ClangPath;
            }

            if (LlvmToolchainRootLocationFailureException is not null)
            {
                exception = LlvmToolchainRootLocationFailureException;
                return null;
            }

            // It's not super clear if Win32Exception.NativeErrorCode is actually errno on Unix-like systems when Process.Start fails due to a missing executable,
            // but it doesn't actually matter since ERROR_FILE_NOT_FOUND and ENOENT are both 2.
            const int ERROR_FILE_NOT_FOUND = 2;

            // Check if Clang is present on the system PATH
            try
            {
                using Process clang = Process.Start("clang", "--version");
                clang.WaitForExit();

                if (clang.ExitCode == 0)
                {
                    exception = null;
                    return ClangPath = "clang";
                }
                else
                {
                    exception = new Exception("The Clang install found on the system PATH appears to be non-functional.");
                    LlvmToolchainRootLocationFailureException = exception;
                    return null;
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == ERROR_FILE_NOT_FOUND)
            { exception = new FileNotFoundException("Clang was not found on the system PATH."); }
            catch (Exception ex)
            {
                exception = new Exception($"The Clang install found on the system PATH appears to be unusable: {ex.Message}.", ex);
                LlvmToolchainRootLocationFailureException = exception;
                return null;
            }

            // Find Clang from Visual Studio if the appropriate component is installed
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    // The other LLVM-related component (Microsoft.VisualStudio.Component.VC.Llvm.ClangToolset) is only for using clang-cl for building C++ MSBuild projects.
                    VisualStudioLocator locator = new("Microsoft.VisualStudio.Component.VC.Llvm.Clang");
                    string visualStudioRoot = locator.LocateVisualStudio();
                    string visualStudioClangPath = Path.Combine(visualStudioRoot, "VC", "Tools", "Llvm", "bin", "clang.exe");

                    if (!File.Exists(visualStudioClangPath))
                    { throw new FileNotFoundException("Visual Studio install claims to have LLVM toolchain but clang.exe was not found.", visualStudioClangPath); }

                    exception = null;
                    return ClangPath = visualStudioClangPath;
                }
                catch (Exception ex)
                {
                    if (exception is not null)
                    { exception = new AggregateException(exception, ex); }
                    else
                    { exception = ex; }
                }
            }

            // Clang is not installed
            LlvmToolchainRootLocationFailureException = exception;
            return null;
        }

        public static Exception? IsClangAvailable()
        {
            string? clangPath = TryFindClang(out Exception? exception);
            Debug.Assert(exception is not null || clangPath is not null);
            return exception;
        }

        public static string GetClangPath()
        {
            string? clangPath = TryFindClang(out Exception? exception);

            if (exception is not null)
            {
                Debug.Assert(clangPath is null);
                throw exception;
            }

            Debug.Assert(clangPath is not null);
            return clangPath;
        }
    }
}
