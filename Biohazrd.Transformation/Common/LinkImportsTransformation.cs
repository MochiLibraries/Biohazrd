using Biohazrd.Transformation.Infrastructure;
using Kaisa;
using LibObjectFile.Elf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace Biohazrd.Transformation.Common
{
    public sealed class LinkImportsTransformation : TransformationBase
    {
        private readonly Dictionary<string, SymbolEntry> Imports = new();

        private bool _TrackVerboseImportInformation = true;

        /// <summary>Enables tracking verbose import information at the expense of consuming more memory. Enabled by default.</summary>
        public bool TrackVerboseImportInformation
        {
            get => _TrackVerboseImportInformation;
            set
            {
                if (Imports.Count > 0)
                { throw new InvalidOperationException("You must configure verbose import information tracking before adding any library files."); }

                _TrackVerboseImportInformation = value;
            }
        }

        /// <summary>If true a warning will be issued if a symbol is ambiguous. Enabled by default.</summary>
        /// <remarks>
        /// A symbol always resolves to the first library where an import appears.
        ///
        /// If you enable this, consider enabling <see cref="TrackVerboseImportInformation"/> for more verbose messages.
        /// </remarks>
        public bool WarnOnAmbiguousSymbols { get; set; } = true;

        /// <summary>If true an error will be issued for symbols which cannot be resolved. Enabled by default.</summary>
        /// <remarks>
        /// This does not apply to virtual methods because they're generally not exported. If you have advanced needs and expect virtual methods to be exported, enable <see cref="ErrorOnMissingVirtualMethods"/>.
        /// </remarks>
        public bool ErrorOnMissing { get; set; } = true;

        /// <summary>If true, an error will be issued for virtual methods which cannot be resolved. Disabled by default.</summary>
        /// <remarks>You generally do not want to enable this option unless you have advanced needs for virtual methods to be exported.</remarks>
        public bool ErrorOnMissingVirtualMethods { get; set; }

        private static ReadOnlySpan<byte> ElfFileSignature => new byte[] { 0x7F, 0x45, 0x4C, 0x46 }; // 0x7F "ELF"
        private static ReadOnlySpan<byte> WindowsArchiveSignature => new byte[] { 0x21, 0x3C, 0x61, 0x72, 0x63, 0x68, 0x3E, 0xA };// "!<arch>\n"
        private static int LongestSignatureLength => WindowsArchiveSignature.Length;
        public void AddLibrary(string filePath)
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read);

            // Determine if the library is an ELF shared library or a Windows archive file
            Span<byte> header = stackalloc byte[LongestSignatureLength];
            if (stream.Read(header) != header.Length)
            { throw new ArgumentException("The specified file is too small to be a library.", nameof(filePath)); }

            stream.Position = 0;

            if (header.StartsWith(WindowsArchiveSignature))
            {
                Archive library = new(stream);

                // Enumerate all import and export symbols from the package
                foreach (ArchiveMember member in library.ObjectFiles)
                {
                    if (member is ImportArchiveMember importMember)
                    {
                        SymbolImportExportInfo info = new(importMember);
                        GetOrCreateSymbolEntry(importMember.Symbol).AddImport(filePath, info);
                    }
                    else if (member is CoffArchiveMember coffMember)
                    {
                        foreach (CoffSymbol coffSymbol in coffMember.Symbols)
                        { GetOrCreateSymbolEntry(coffSymbol.Name).AddExport(filePath); }
                    }
                }
            }
            else if (header.StartsWith(ElfFileSignature))
            {
                ElfReaderOptions options = new() { ReadOnly = true };
                ElfObjectFile elf = ElfObjectFile.Read(stream, options);

                // Determine the name to use for the import
                // In theory we could strip off the lib prefix and .so suffix here, however that will remove information required for loading the library via NativeLibrary.Load(string)
                // If we want to strip these we should handle it in the output stage instead (that way it's consistent across the entire library too.)
                string libraryFileName = Path.GetFileName(filePath);

                // Find the .dynsym table
                // (We do not bother with the symbol hash table because we'll most likely end up needing nearly every symbol as it is and it simplifies our architecture.)
                const string sectionName = ".dynsym";
                ElfSymbolTable? symbolTable = elf.Sections.OfType<ElfSymbolTable>().FirstOrDefault(s => s.Name == sectionName);

                if (symbolTable is null)
                { throw new ArgumentException($"The specified ELF file does not contain a '{sectionName}' section."); }

                // See https://refspecs.linuxbase.org/elf/gabi4+/ch4.symtab.html for details on what the symbol table entires mean
                // Note that the spec is not very specific about what is and isn't allowed in the dynamic symbol table, so we're probably overly defensive here
                // In general we ignore symbols which have OS and architecture-specific types or binding since we can't safely understand what they're for.
                foreach (ElfSymbol symbol in symbolTable.Entries)
                {
                    // Ignore unnamed symbols
                    if (String.IsNullOrEmpty(symbol.Name))
                    { continue; }

                    // Ignore symbols which are not publically accessible
                    // (Default effectively means public)
                    if (symbol.Visibility != ElfSymbolVisibility.Default && symbol.Visibility != ElfSymbolVisibility.Protected)
                    { continue; }

                    // Ignore symbols which are undefined
                    if (symbol.Section.IsSpecial && symbol.Section.SpecialIndex == ElfNative.SHN_UNDEF)
                    { continue; }

                    // Ignore symbols which are not functions or objects (variables)
                    if (symbol.Type != ElfSymbolType.Function && symbol.Type != ElfSymbolType.Object)
                    { continue; }

                    // Ignore symbols which are not global or weak
                    // Note that we do not actually differentiate between global and weak symbols, the distinction only matters for static linking
                    // See https://www.bottomupcs.com/libraries_and_the_linker.xhtml#d0e10440
                    if (symbol.Bind != ElfSymbolBind.Global && symbol.Bind != ElfSymbolBind.Weak)
                    { continue; }

                    // Determine the import type of the symbol based on its section
                    ImportType importType;
                    switch (symbol.Section.Section?.Name)
                    {
                        case ".text":
                            importType = ImportType.Code;
                            break;
                        case ".rodata":
                            importType = ImportType.Const;
                            break;
                        case ".data":
                        case ".bss":
                            importType = ImportType.Data;
                            break;
                        case null when symbol.Section.IsSpecial:
                            Debug.Fail($"ELF symbol from special section '{symbol.Section}'");
                            continue;
                        default:
                            if (symbol.Section.Section?.Name is not null)
                            { Debug.Fail($"ELF symbol from unrecognized section '{symbol.Section.Section.Name}'"); }
                            else
                            { Debug.Fail($"ELF symbol from unnamed/null section."); }
                            continue;
                    }

                    // Add the symbol to our lookup
                    SymbolImportExportInfo info = new(libraryFileName, symbol, importType);
                    GetOrCreateSymbolEntry(symbol.Name).AddImport(filePath, info);
                }
            }
            else
            { throw new ArgumentException("The specified file does not appear to be in a compatible format.", nameof(filePath)); }

            SymbolEntry GetOrCreateSymbolEntry(string symbol)
            {
                SymbolEntry? symbolEntry;
                if (!Imports.TryGetValue(symbol, out symbolEntry))
                {
                    symbolEntry = new SymbolEntry(TrackVerboseImportInformation);
                    Imports.Add(symbol, symbolEntry);
                }
                return symbolEntry;
            }
        }

        private bool Resolve(string symbolName, [NotNullWhen(true)] out string? dllFileName, [NotNullWhen(true)] out string? mangledName, ref DiagnosticAccumulator diagnosticsAccumulator, bool isFunction, bool isVirtualMethod)
        {
            DiagnosticAccumulatorRef diagnostics = new(ref diagnosticsAccumulator);

            // Try to resolve the symbol
            SymbolEntry? symbolEntry;
            if (!Imports.TryGetValue(symbolName, out symbolEntry))
            {
                // If the symbol could not be resolved, emit a diagnostic if requested and fail
                if ((ErrorOnMissing && !isVirtualMethod) || (ErrorOnMissingVirtualMethods && isVirtualMethod))
                { diagnostics.Add(new TranslationDiagnostic(Severity.Error, $"Could not resolve symbol '{symbolName}'")); }

                dllFileName = null;
                mangledName = null;
                return false;
            }

            SymbolImportExportInfo symbolInfo = symbolEntry.Info;

            static string MakeErrorMessage(string message, SymbolEntry symbolEntry)
            {
                if (symbolEntry.Sources is not null)
                {
                    StringBuilder builder = new(message);
                    builder.Append(" The following candidates were considered:");

                    foreach ((string library, SymbolImportExportInfo info) in symbolEntry.Sources)
                    {
                        if (info.IsFromElf)
                        { builder.Append($"{Environment.NewLine}    '{library}'"); } // The library and the DllFileName are the same for ELF sources, avoid printing the redundant info.
                        else if (info.IsImport)
                        { builder.Append($"{Environment.NewLine}    '{library}': Import from '{info.DllFileName}'"); }
                        else
                        { builder.Append($"{Environment.NewLine}    '{library}': Statically-linked export'"); }
                    }

                    return builder.ToString();
                }
                else
                { return $"{message} {symbolEntry.ImportCount} imports and {symbolEntry.ExportCount} exports were considered."; }
            }

            // If the symbol is only an export, emit a diagnostic and fail
            if (!symbolInfo.IsImport)
            {
                Severity severity = ErrorOnMissing ? Severity.Error : Severity.Warning;
                diagnostics.Add(new TranslationDiagnostic(severity, MakeErrorMessage($"No import sources found for '{symbolName}'.", symbolEntry)));
                dllFileName = null;
                mangledName = null;
                return false;
            }

            dllFileName = symbolInfo.DllFileName;
            mangledName = symbolName;

            // Warn if the symbol has multiple sources
            if (WarnOnAmbiguousSymbols && symbolEntry.TotalCount > 1)
            { diagnostics.Add(new TranslationDiagnostic(Severity.Warning, MakeErrorMessage($"'{symbolName}' was ambiguous. Picked import from '{dllFileName}'.", symbolEntry))); }

            // Warn if the desired symbol type doesn't match the expected type
            if (isFunction && symbolInfo.ImportType != ImportType.Code)
            { diagnostics.Add(new TranslationDiagnostic(Severity.Warning, $"Function '{symbolName}' resolved to non-code symbol in '{dllFileName}'.")); }
            else if (!isFunction && symbolInfo.ImportType == ImportType.Code)
            { diagnostics.Add(new TranslationDiagnostic(Severity.Warning, $"Non-function '{symbolName}' resolved to a code symbol in '{dllFileName}'.")); }

            // Adjust the name if necessary
            switch (symbolInfo.ImportNameType)
            {
                case ImportNameType.Name:
                    // Nothing to do
                    break;
                // This is sort-of leaking a C# output generator implementation detail
                // Ordinals can be imported in C# by specifying `#n` for the DllImportAttribute.EntryPoint, so we stick them into the mangled name
                // Note that ordinals cannot be imported via System.Runtime.InteropServices.NativeLibrary, so this doesn't work everywhere. (Hence the warning message.)
                case ImportNameType.Ordinal:
                    diagnostics.Add(new TranslationDiagnostic(Severity.Warning, $"'{symbolName}' resolved to ordinal #{symbolInfo.OrdinalOrHint} in '{dllFileName}'. " +
                        "Biohazrd may not handle ordinal imports appropriately in all contexts."));
                    mangledName = $"#{symbolInfo.OrdinalOrHint}";
                    break;
                // These names are a bit odd. Not sure if/when they occur in practice.
                // Implementation based on LLVM since the documentation is a little vauge.
                // https://github.com/llvm/llvm-project/blob/62ec4ac90738a5f2d209ed28c822223e58aaaeb7/lld/COFF/InputFiles.cpp#L985-L991
                case ImportNameType.NameNoPrefix:
                    Debug.Fail("These name types are not common and need verification.");
                    mangledName = TrimFirstMangleCharacter(mangledName);
                    break;
                case ImportNameType.NameUndecorate:
                    Debug.Fail("These name types are not common and need verification.");
                    mangledName = TrimFirstMangleCharacter(mangledName);

                    int indexOfAt = mangledName.IndexOf('@');
                    if (indexOfAt >= 0)
                    { mangledName = mangledName.Substring(0, indexOfAt); }
                    break;
            }

            return true;
        }

        private string TrimFirstMangleCharacter(string name)
        {
            if (name.Length > 0 && name[0] is '?' or '@' or '_')
            { return name.Substring(1); }

            return name;
        }

        protected override TransformationResult TransformFunction(TransformationContext context, TranslatedFunction declaration)
        {
            string? resolvedDll;
            string? resolvedName;
            DiagnosticAccumulator diagnostics = new();

            if (!Resolve(declaration.MangledName, out resolvedDll, out resolvedName, ref diagnostics, isFunction: true, isVirtualMethod: declaration.IsVirtual))
            {
                // If there's no changes, don't modify the function
                if (!diagnostics.HasDiagnostics)
                { return declaration; }

                resolvedDll = declaration.DllFileName;
                resolvedName = declaration.MangledName;
            }

            return declaration with
            {
                DllFileName = resolvedDll,
                MangledName = resolvedName,
                Diagnostics = declaration.Diagnostics.AddRange(diagnostics.MoveToImmutable())
            };
        }

        protected override TransformationResult TransformStaticField(TransformationContext context, TranslatedStaticField declaration)
        {
            string? resolvedDll;
            string? resolvedName;
            DiagnosticAccumulator diagnostics = new();

            if (!Resolve(declaration.MangledName, out resolvedDll, out resolvedName, ref diagnostics, isFunction: false, isVirtualMethod: false))
            {
                // If there's no changes, don't modify the field
                if (!diagnostics.HasDiagnostics)
                { return declaration; }

                resolvedDll = declaration.DllFileName;
                resolvedName = declaration.MangledName;
            }

            return declaration with
            {
                DllFileName = resolvedDll,
                MangledName = resolvedName,
                Diagnostics = declaration.Diagnostics.AddRange(diagnostics.MoveToImmutable())
            };
        }

        private class SymbolEntry
        {
            public SymbolImportExportInfo Info { get; private set; } = default;
            public int ExportCount { get; private set; } = 0;
            public int ImportCount { get; private set; } = 0;
            public int TotalCount => ExportCount + ImportCount;
            private List<(string Library, SymbolImportExportInfo Info)>? _Sources { get; }
            public IReadOnlyList<(string Lbirary, SymbolImportExportInfo Info)>? Sources => _Sources;

            public SymbolEntry(bool trackAllSources)
                => _Sources = trackAllSources ? new() : null;

            public void AddImport(string library, SymbolImportExportInfo info)
            {
                if (!Info.IsImport)
                {
                    Info = info;
                    ImportCount++;
                }
                // Only increment the import count for redundant imports if the import is effectively different
                // This way we don't emit unecessary warnings when multiple import libraries provide the same symbol with the same DLL
                else if (!info.IsEquivalentTo(this.Info))
                { ImportCount++; }

                _Sources?.Add((library, info));
            }

            public void AddExport(string library)
            {
                ExportCount++;
                _Sources?.Add((library, default));
            }
        }

        private readonly struct SymbolImportExportInfo
        {
            [MemberNotNullWhen(true, nameof(DllFileName))]
            public bool IsImport { get; }
            public ImportType ImportType { get; }
            public ImportNameType ImportNameType { get; }
            public string? DllFileName { get; }
            public ushort OrdinalOrHint { get; }
            public bool IsFromElf { get; }

            public SymbolImportExportInfo(ImportArchiveMember importMember)
            {
                IsImport = true;
                ImportType = importMember.ImportHeader.Type;
                ImportNameType = importMember.ImportHeader.NameType;
                DllFileName = importMember.Dll;
                OrdinalOrHint = importMember.ImportHeader.OrdinalOrHint;
                IsFromElf = false;
            }

            public SymbolImportExportInfo(string libraryFileName, ElfSymbol symbol, ImportType importType)
            {
                IsImport = true;
                ImportNameType = ImportNameType.Name; // ELFs do not have ordinal members
                DllFileName = libraryFileName;
                OrdinalOrHint = 0;
                ImportType = importType;
                IsFromElf = true;
            }

            public bool IsEquivalentTo(SymbolImportExportInfo other)
            {
                // Don't consider other fields for exports
                if (!IsImport)
                { return IsImport == other.IsImport; }

                // Only check OrdinalOrHint if the name type is ordinal
                if (ImportNameType == ImportNameType.Ordinal && this.OrdinalOrHint != other.OrdinalOrHint)
                { return false; }

                return this.IsImport == other.IsImport
                    && this.ImportType == other.ImportType
                    && this.ImportNameType == other.ImportNameType
                    && this.DllFileName.Equals(other.DllFileName, StringComparison.InvariantCultureIgnoreCase);
            }
        }
    }
}
