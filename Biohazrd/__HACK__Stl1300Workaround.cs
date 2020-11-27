using ClangSharp.Interop;
using Microsoft.VisualStudio.Setup.Configuration;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Biohazrd
{
    /// <summary>
    /// Implements a workaround for https://github.com/microsoft/STL/issues/1300
    /// </summary>
    /// <remarks>
    /// This class contains a workaround for https://github.com/microsoft/STL/issues/1300
    /// See https://github.com/InfectedLibraries/Biohazrd/issues/98 for the corresponding Biohazrd issue.
    /// This bug affects libclang-based tools (like Biohazrd) in conjunection with Visual Studio 16.8.
    /// It will be fixed in Visual Studio 16.9 Preview 2, but for now we work around the issue by automatically
    /// applying the patch provided in https://github.com/microsoft/STL/issues/1300#issuecomment-718065833
    ///
    /// The workaround works by checking for what version of Visual Studio Clang is expected to use.
    /// If this version is older than 16.9 Preview 2, it opens `intrin0.h` and applies the diff to it.
    /// This diff is then provided to Clang as an unsaved file in <see cref="TranslatedLibraryBuilder.Create"/>.
    /// </remarks>
    internal sealed class __HACK__Stl1300Workaround
    {
        private volatile static __HACK__Stl1300Workaround? _Instance = null;
        private static object _InstanceLock = new();
        public static __HACK__Stl1300Workaround Instance
        {
            get
            {
                if (_Instance is not null)
                { return _Instance; }

                lock (_InstanceLock)
                {
                    if (_Instance is not null)
                    { return _Instance; }

                    return _Instance = new __HACK__Stl1300Workaround();
                }
            }
        }

        public ImmutableArray<TranslationDiagnostic> Diagnostics { get; } = ImmutableArray<TranslationDiagnostic>.Empty;
        public bool ShouldBeApplied { get; } = false;
        public CXUnsavedFile UnsavedFile { get; } = default;

        private const string DebugPrefix = "GH-98: ";
        private const string DiagnosticPrefix = "Failed to apply https://github.com/InfectedLibraries/Biohazrd/issues/98 workaround: ";

        private readonly byte[]? FileNameArray = null;
        private readonly byte[]? FileContentsArray = null;

        private enum PatchKind
        {
            Delete,
            AddBefore,
            AddAfter
        }

        private __HACK__Stl1300Workaround()
        {
            try
            {
                Debug.WriteLine($"{DebugPrefix}Applying STL#1300 workaround, see https://github.com/InfectedLibraries/Biohazrd/issues/98 for details.");

                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // First we locate Visual Studio using logic similar to Clang
                // https://github.com/InfectedLibraries/llvm-project/blob/6d5c430eb3c0bd49f6f5bda4b0d2d8aa79b0fa3f/clang/lib/Driver/ToolChains/MSVC.cpp#L178-L268
                // This is not the full logic of how Clang discovers Visual Studio installations.
                // In particular it does not discover Visual Studio in environment variables (IE: running from developer command prompt) or using the legacy registry discovery.
                // The former isn't ideal, but this is only meant to be a short-term fix. Nothing terrible will happen if we should've done this, but the patch won't be applied if it was necessary.
                // The latter only applies to legacy verisons of Visual Studio that we don't support anyway.
                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                ISetupInstance visualStudioInstance;
                try
                {
                    SetupConfiguration query = new();
                    IEnumSetupInstances enumInstances = query.EnumAllInstances();
                    ISetupHelper helper = (ISetupHelper)query;

                    ISetupInstance[] instance = new ISetupInstance[1];

                    ISetupInstance? newestInstance = null;
                    ulong newestVersionNum = 0;

                    while (true)
                    {
                        int fetched;
                        enumInstances.Next(1, instance, out fetched);

                        if (fetched == 0)
                        { break; }

                        string versionString = instance[0].GetInstallationVersion();
                        ulong versionNum = helper.ParseVersion(versionString);

                        if (newestVersionNum == 0 || versionNum > newestVersionNum)
                        {
                            newestInstance = instance[0];
                            newestVersionNum = versionNum;
                        }
                    }

                    if (newestInstance is null)
                    {
                        Diagnostics = Diagnostics.Add(Severity.Warning, $"{DiagnosticPrefix}No instances of Visual Studio were found.");
                        return;
                    }

                    visualStudioInstance = newestInstance;
                }
                catch (COMException ex) when (ex.HResult == unchecked((int)0x80040154)) // REGDB_E_CLASSNOTREG
                {
                    Diagnostics = Diagnostics.Add(Severity.Warning, $"{DiagnosticPrefix}Could not locate the Visual Studio setup configuration COM service. {ex}");
                    return;
                }
                catch (Exception ex) when (!Debugger.IsAttached)
                {
                    Diagnostics = Diagnostics.Add(Severity.Warning, $"{DiagnosticPrefix}An exception ocurred while attempting to locate Visual Studio installs: {ex}");
                    return;
                }

                string visualStudioDescription = visualStudioInstance.GetInstallationName();
                Debug.WriteLine($"{DebugPrefix}Found Visual Studio installation {visualStudioDescription}");

                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Check if the workaround applies to this version of Visual Studio
                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                Version version = new(visualStudioInstance.GetInstallationVersion());
                Version minVersion = new(16, 8); // This issue started in 16.8: https://developercommunity.visualstudio.com/content/problem/1144026/visual-studio-version-1680-preview-10-no-longer-co.html
                Version maxVersion = new(16, 9, 30709, 64); // Visual Studio 16.9 Preview 1, this issue will be fixed in 16.9 Preview 2 https://github.com/microsoft/STL/issues/1300#issuecomment-703027203

                if (version < minVersion)
                {
                    Debug.WriteLine($"{DebugPrefix}Visual Studio installation is older than {minVersion}, workaround is not applicable.");
                    return;
                }

                if (version > maxVersion)
                {
                    Debug.WriteLine($"{DebugPrefix}Visual Studio version is newer than {maxVersion}, workaround should no longer be needed.");
                    return;
                }

                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Find intrin0.h
                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                string vcPath = visualStudioInstance.ResolvePath("VC");
                string defaultToolsVersionPath = Path.Combine(vcPath, "Auxiliary", "Build", "Microsoft.VCToolsVersion.default.txt");

                if (!File.Exists(defaultToolsVersionPath))
                {
                    Diagnostics = Diagnostics.Add(Severity.Warning, $"{DiagnosticPrefix}Could not find default VC tools version for {visualStudioDescription}");
                    return;
                }

                string defaultToolsVersion = File.ReadAllText(defaultToolsVersionPath).Trim();
                Debug.WriteLine($"{DebugPrefix}Found default VC tools version '{defaultToolsVersion}' via '{defaultToolsVersionPath}'");

                string intrin0Path = Path.Combine(vcPath, "Tools", "MSVC", defaultToolsVersion, "include", "intrin0.h");

                if (!File.Exists(intrin0Path))
                {
                    Diagnostics = Diagnostics.Add(Severity.Warning, $"{DiagnosticPrefix}Could not locate intrin0.h at expected path '{intrin0Path}'");
                    return;
                }

                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Apply the patch
                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                StringBuilder patchedIntrin0Builder = new();
                patchedIntrin0Builder.AppendLine("// This file was automatically patched by Biohazrd.");
                patchedIntrin0Builder.AppendLine("// See https://github.com/InfectedLibraries/Biohazrd/issues/98 for details.");

                Queue<(PatchKind Kind, string Search, string Addition)> patches = new();
                patches.Enqueue((PatchKind.Delete, "#ifdef __clang__", ""));
                patches.Enqueue((PatchKind.Delete, "// This looks like a circular include but it is not because clang overrides <intrin.h> with their specific version.", ""));
                patches.Enqueue((PatchKind.Delete, "// See further discussion in LLVM-47099.", ""));
                patches.Enqueue((PatchKind.Delete, "#include <intrin.h>", ""));
                patches.Enqueue((PatchKind.Delete, "#else /* ^^^ __clang__ // !__clang__ vvv */", ""));
                patches.Enqueue((PatchKind.AddBefore, "__MACHINEX86_X64(unsigned int _tzcnt_u32(unsigned int))", "#ifndef __clang__"));
                patches.Enqueue((PatchKind.AddAfter, "__MACHINEX64(unsigned __int64 _tzcnt_u64(unsigned __int64))", "#endif // __clang__"));
                patches.Enqueue((PatchKind.Delete, "#endif /* ^^^ !__clang__ */", ""));

                using (StreamReader input = new(intrin0Path))
                {
                    while (!input.EndOfStream)
                    {
                        string? line = input.ReadLine();

                        if (line is null)
                        { break; }

                        if (patches.Count > 0)
                        {
                            (PatchKind kind, string search, string addition) = patches.Peek();

                            if (line == search)
                            {
                                patches.Dequeue();

                                switch (kind)
                                {
                                    case PatchKind.Delete:
                                        continue;
                                    case PatchKind.AddBefore:
                                        patchedIntrin0Builder.AppendLine(addition);
                                        patchedIntrin0Builder.AppendLine(line);
                                        break;
                                    case PatchKind.AddAfter:
                                        patchedIntrin0Builder.AppendLine(line);
                                        patchedIntrin0Builder.AppendLine(addition);
                                        break;
                                    default:
                                        throw new InvalidOperationException("Malformed patch");
                                }

                                continue;
                            }
                        }

                        patchedIntrin0Builder.AppendLine(line);
                    }
                }

                // If there's still patches remaining, the file doesn't match what we were expecting
                // (Unfortunately there's not really an easy way for us to check if this file was changed between 16.8 and 16.9p1 so we just assume it either didn't or it's close enough for our patch.)
                if (patches.Count > 0)
                {
                    Diagnostics = Diagnostics.Add(Severity.Warning, $"{DiagnosticPrefix}Failed to apply the patch automatically. (Was intrin0.h modified by hand?)");
                    return;
                }

                // Add a warning so we know if the patch gets used
                patchedIntrin0Builder.AppendLine($"#warning \"Using intrin0.h patched by Biohazrd to work around STL#1300, see https://github.com/InfectedLibraries/Biohazrd/issues/98 for details.\"");

                string patchedIntrin0 = patchedIntrin0Builder.ToString();

                Debug.WriteLine($"{DebugPrefix}Finished patching intrin0.h");

                // Write out the patched file so it can be inspected
                const string patchedFileName = "intrin0.patched.h";
                try
                {
                    File.WriteAllText(patchedFileName, patchedIntrin0);
                    Debug.WriteLine($"{DebugPrefix}Wrote patched file out to '{Path.GetFullPath(patchedFileName)}'");
                }
                catch
                { Debug.WriteLine($"{DebugPrefix}Failed to write patched file to '{Path.GetFullPath(patchedFileName)}'"); }

                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Create the unsaved file
                //-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
                // Allocate the file name and contents on the pinned object heap so we can just forget about them
                FileNameArray = Encoding.UTF8.GetBytesNullTerminated(intrin0Path, pinned: true);
                FileContentsArray = Encoding.UTF8.GetBytesNullTerminated(patchedIntrin0, pinned: true);

                unsafe
                {
                    UnsavedFile = new CXUnsavedFile()
                    {
                        Filename = (sbyte*)Unsafe.AsPointer(ref FileNameArray[0]),
                        Contents = (sbyte*)Unsafe.AsPointer(ref FileContentsArray[0]),
                        Length = (UIntPtr)(FileContentsArray.Length - 1) // -1 to remove null terminator
                    };
                }
                ShouldBeApplied = true;
            }
            catch (Exception ex) when (!Debugger.IsAttached) // We don't want to bring down the generator process failing to apply this workaround
            {
                Diagnostics = Diagnostics.Add(Severity.Warning, $"{DiagnosticPrefix}An exception ocurred while applying the workaround: {ex}");
                ShouldBeApplied = false;
                return;
            }
        }
    }
}
