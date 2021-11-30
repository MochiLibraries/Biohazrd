using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace Biohazrd
{
    /// <summary>Provides utilities for handling Biohazrd's interactions with Apple Xcode.</summary>
    internal static class Xcode
    {
        private static object Lock = new();
        private static ImmutableArray<string> CommandLineArguments;
        private static ImmutableArray<TranslationDiagnostic> Diagnostics;

        // On macOS Clang needs help locating the SDK since it only does this automagically if you use Apple Clang
        // (Homebrew Clang also works by distributing its own copy of libc++ and hard-coding a path to the Xcode command line tools -- which is safe for it since Homebrew implicitly installs them.)
        // See https://github.com/MochiLibraries/Biohazrd/issues/226 for details.
        // Ideally in the future this would all be replaced by a formalized, cross-platform SDK provider API.
        internal static void PrepareLibrary(TranslatedLibraryBuilder builder)
        {
            // Lazy escape hatch since it's hard to override the command line flags added by this method
            // (Eventually we want a more flexible SDK provider interface for all platforms.)
            if (Environment.GetEnvironmentVariable("BIOHAZRD_DO_NOT_DISCOVER_MACOS_SDK")?.Equals("true", StringComparison.InvariantCultureIgnoreCase) ?? false)
            { return; }

            lock (Lock)
            {
                if (CommandLineArguments.IsDefault)
                {
                    // Invoking xcrun like this is not super ideal, but as far as I could find this is the canonical way to locate SDKs on macOS.
                    static (string? output, Exception? error) ExecuteXcrun(string args)
                    {
                        // It's not super clear if Win32Exception.NativeErrorCode is actually errno on Unix-like systems when Process.Start fails due to a missing executable,
                        // but it doesn't actually matter since ERROR_FILE_NOT_FOUND and ENOENT are both 2.
                        const int ERROR_FILE_NOT_FOUND = 2;

                        try
                        {
                            string errorOutput = "";
                            using Process xcrun = new()
                            {
                                StartInfo = new ProcessStartInfo("xcrun", args)
                                {
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true
                                }
                            };
                            xcrun.ErrorDataReceived += (sender, e) => errorOutput += e.Data;

                            xcrun.Start();
                            xcrun.BeginErrorReadLine();

                            string result = xcrun.StandardOutput.ReadToEnd().Trim();
                            xcrun.WaitForExit();
                            errorOutput = errorOutput.Trim();

                            if (xcrun.ExitCode != 0)
                            {
                                string message = $"`xcrun {args}` failed with exit code {xcrun.ExitCode}: ";

                                if (result is not "")
                                { message += result; }

                                if (errorOutput is not "")
                                {
                                    if (result is not "")
                                    { message += Environment.NewLine; }

                                    message += errorOutput;
                                }

                                if (result is "" && errorOutput is "")
                                { message += "<Command did not write any output>"; }

                                // Don't try to use the output when we get a non-zero exit code.
                                return (null, new Exception(message));
                            }

                            // If we got a success code but something was written to standard out we return the result *and* an errror
                            if (errorOutput is not "")
                            { return (result, new Exception($"`xcrun {args}` was seemingly successful but wrote to standard error: {errorOutput}")); }

                            return (result, null);
                        }
                        catch (Win32Exception ex) when (ex.NativeErrorCode == ERROR_FILE_NOT_FOUND)
                        { return (null, new FileNotFoundException("xcrun was not found on the system PATH.")); }
                        catch (Exception ex)
                        { return (null, ex); }
                    }

                    [MethodImpl(MethodImplOptions.NoInlining)]
                    static void Initialize()
                    {
                        ImmutableArray<string>.Builder commandLineArguments = ImmutableArray.CreateBuilder<string>();
                        ImmutableArray<TranslationDiagnostic>.Builder diagnostics = ImmutableArray.CreateBuilder<TranslationDiagnostic>();

                        // Locate the macOS SDK
                        // (Clang will implicitly add system includes for it.)
                        // Specifying `--sdk macosx` seems tempting here, but I don't think it's actually 100% correct because it will select the newest macOS SDK even if the user has
                        // configured a different one as default. I believe not having it will cause this to select the iPhone SDK which isn't exactly ideal either.
                        // For now we just respect whatever SDK the user has selected. We can revisit this as we polish macOS support.
                        (string? sdkPath, Exception? sdkError) = ExecuteXcrun("--show-sdk-path");

                        if (sdkPath is null)
                        {
                            Debug.Assert(sdkError is not null);

                            string message = $"Failed to locate the macOS SDK, system includes may not be available.";

                            if (sdkError is not Exception)
                            { message += $" {sdkError.GetType().Name}: "; }
                            message += $" {sdkError.Message}";

                            diagnostics.Add(Severity.Warning, message);
                        }
                        else
                        {
                            // We might have a result and an error at the same time when xcrun writes to standard error
                            if (sdkError is not null)
                            { diagnostics.Add(Severity.Warning, sdkError.Message); }

                            commandLineArguments.Add("-isysroot");
                            commandLineArguments.Add(sdkPath);

                            // This is seemingly clobbered by `-isysroot`. It's not super important but Apple Clang includes it so we do too.
                            commandLineArguments.Add("-I/usr/local/include");
                        }

                        // Locate Apple Clang in order to find the location of libc++ and other toolchain includes
                        (string? appleClangPath, Exception? appleClangError) = ExecuteXcrun("--find clang");

                        if (appleClangPath is null)
                        {
                            Debug.Assert(appleClangError is not null);

                            string message = $"Failed to locate the Xcode Toolchain, system includes may not be available.";

                            if (appleClangError is not Exception)
                            { message += $" {appleClangError.GetType().Name}: "; }
                            message += $" {appleClangError.Message}";

                            diagnostics.Add(Severity.Warning, message);
                        }
                        else
                        {
                            // We might have a result and an error at the same time when xcrun writes to standard error
                            if (appleClangError is not null)
                            { diagnostics.Add(Severity.Warning, appleClangError.Message); }

                            // Find the Xcode Toolchain path using the path to Apple Clang
                            // This is basically Clang does it:
                            // https://github.com/MochiLibraries/llvm-project/blob/de0b94898a446be9d45323673a4a8ea9a4d24d5c/clang/lib/Driver/ToolChains/Darwin.cpp#L2045-L2052
                            // (We don't handle the second case because Clang will do that automatically.)
                            string? toolchainPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(appleClangPath)));
                            if (toolchainPath is null)
                            { diagnostics.Add(Severity.Warning, $"Could not locate the Xcode toolchain via the Apple Clang path '{appleClangPath}', system includes may not be available."); }
                            else
                            {
                                //TODO: We should really only do this if libc++ is the selected standard library but at this stage we don't actually know.
                                commandLineArguments.Add("-isystem");
                                commandLineArguments.Add(Path.Combine(toolchainPath, "include", "c++", "v1"));

                                // Ideally we'd also be adding Path.Combine(toolchainPath, "include") just like Apple Clang does
                                // Unfortunately it adds this path using a cc1 option `--internal-externc-isystem` which seemingly does not have a public equivalent.
                                // This path does not seem to be particularly important though, so it's probably fine to skip it. (Homebrew Clang misses it too.)
                            }
                        }

                        Debug.Assert(CommandLineArguments.IsDefault);
                        Debug.Assert(Diagnostics.IsDefault);
                        CommandLineArguments = commandLineArguments.MoveToImmutableSafe();
                        Diagnostics = diagnostics.MoveToImmutableSafe();
                    }

                    Initialize();
                }

                Debug.Assert(!CommandLineArguments.IsDefault);
                Debug.Assert(!Diagnostics.IsDefault);
            }

            builder.AddCommandLineArguments(CommandLineArguments);
            builder.AddPreparseDiagnostics(Diagnostics);
        }
    }
}
