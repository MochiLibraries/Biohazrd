using Biohazrd.Transformation.Infrastructure;
using Kaisa;
using Kaisa.Elf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace Biohazrd.Transformation.Common
{
    // This transformation uses some confusing nomenclature due to being originally designed for Windows-only and using the same terminology as the Microsoft .lib file specification does.
    // In this file:
    // A symbol which is exported is one which is exported by one of the static libraries passed to AddLibrary.
    // A symbol which is imported is one which is imported from either a .dll pointed to by an Microsoft import .lib passed to AddLibrary OR exported by a .so passed to AddLibrary.
    // In a future cleanup of this transformation, we should probably change it to ExportedFromStaticLibrary and ExportedFromDynamicLibrary respectively.
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

        public void AddLibrary(string filePath)
        {
            using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read);

            // Determine the name to use for the import in the case of ELF files or Linux-style archives
            // In theory we could strip off the lib prefix and .so suffix here, however that will remove information required for loading the library via NativeLibrary.Load(string)
            // If we want to strip these we should handle it in the output stage instead (that way it's consistent across the entire library too.)
            string libraryFileName = Path.GetFileName(filePath);

            if (Archive.IsArchiveFile(stream))
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
                    else if (member is ElfArchiveMember elfMember)
                    {
                        ProcessElfFile(elfMember.ElfFile);
                    }
                }
            }
            else if (ElfFile.IsElfFile(stream))
            {
                ElfFile elf = new(stream);
                ProcessElfFile(elf);
            }
            else
            { throw new ArgumentException("The specified file does not appear to be in a compatible format.", nameof(filePath)); }

            void ProcessElfFile(ElfFile elf)
            {
                bool isSharedObject = elf.Header.Type == ElfType.SharedObjectFile;

                // Get the symbol table
                // For shared objects we prefer the dynamic symbol table, for static libraries we prefer the normal one
                // (I don't think there's a situation where a static library even has a dynamic symbol table, much less a dynamic one but not a normal one,
                //   but we're not here to decide what makes a valid Linux ELF.)
                // Note that there is not a reason we'd want to process both tables (assuming the ELF file is sane) since the dynamic symbol table should always be a subset of the main one.
                ElfSymbolTableSection? symbolTable;
                if (isSharedObject)
                { symbolTable = elf.DynamicSymbolTable ?? elf.SymbolTable; }
                else
                { symbolTable = elf.SymbolTable ?? elf.DynamicSymbolTable; }

                if (symbolTable is null)
                {
                    // I'm sure you could probably craft a valid shared object ELF that doesn't have a symbol table of any kind, but that'd be weird and the caller probably wants to know.
                    if (isSharedObject)
                    { throw new ArgumentException($"'{filePath}' does not have a symbol table of any kind.", nameof(filePath)); }
                    else
                    { return; } // Don't throw on library archives since they can contain many separate ELF files and we don't want to blow up with just a single unusual one.
                }

                ProcessElfSymbolSection(libraryFileName, isSharedObject, symbolTable);
            }

            void ProcessElfSymbolSection(string libraryFileName, bool isFromSharedObject, ElfSymbolTableSection symbolTable)
            {
                // See https://refspecs.linuxbase.org/elf/gabi4+/ch4.symtab.html for details on what the symbol table entires mean
                // Note that the spec is not very specific about what is and isn't allowed in the dynamic symbol table, so we're probably overly defensive here
                // In general we ignore symbols which have OS and architecture-specific types or binding since we can't safely understand what they're for.
                foreach (ElfSymbol symbol in symbolTable)
                {
                    ElfSection? section = symbol.DefinedIn;

                    // Ignore unnamed symbols
                    if (String.IsNullOrEmpty(symbol.Name))
                    { continue; }

                    // Ignore symbols which are not publically accessible
                    // (Default effectively means public)
                    if (symbol.Visibility != ElfSymbolVisibility.Default && symbol.Visibility != ElfSymbolVisibility.Protected)
                    { continue; }

                    // Ignore symbols which are undefined
                    if (section is null || section.Type is ElfSectionType.Null)
                    { continue; }

                    // Ignore symbols which are not functions or objects (variables)
                    if (symbol.Type != ElfSymbolType.Function && symbol.Type != ElfSymbolType.Object)
                    { continue; }

                    // Ignore symbols which are not global or weak
                    // Note that we do not actually differentiate between global and weak symbols, the distinction only matters for static linking
                    // See https://www.bottomupcs.com/libraries_and_the_linker.xhtml#d0e10440
                    if (symbol.Binding != ElfSymbolBinding.Global && symbol.Binding != ElfSymbolBinding.Weak)
                    { continue; }

                    // Determine the import type of the symbol based on its section
                    ImportType importType;
                    Debug.Assert(section.Type is ElfSectionType.ProgramSpecificData or ElfSectionType.NoData, $"Symbols should be exported from program-specific data or no-data sections. '{symbol}' '{section}'");
                    const ElfSectionFlags codeFlags = ElfSectionFlags.Allocated | ElfSectionFlags.ExecutableInstructions;
                    const ElfSectionFlags constFlags = ElfSectionFlags.Allocated;
                    const ElfSectionFlags dataFlags = ElfSectionFlags.Allocated | ElfSectionFlags.Writeable;

                    if ((section.Header.Flags & codeFlags) == codeFlags)
                    { importType = ImportType.Code; }
                    else if ((section.Header.Flags & constFlags) == constFlags)
                    { importType = ImportType.Const; }
                    else if ((section.Header.Flags & dataFlags) == dataFlags)
                    { importType = ImportType.Data; }
                    else
                    {
                        Debug.Fail("Could not determine symbol type based on section flags.");
                        continue;
                    }

                    // Assert on unusual symbols since we might not be handling them appropriately.
                    // (The symbol *is* still being exported by the file though, so it's probably fine -- don't throw.)
                    Debug.Assert(section.IsWellKnownSystemSection, $"Symbols '{symbol.Name}' is defined in '{section.Name}', which is not a well-known system section.");
                    Debug.Assert(!section.IsNonStandard, $"Symbol '{symbol.Name}' is defined in an extension section '{section.Name}'");

                    // Add the symbol to our lookup
                    if (isFromSharedObject)
                    {
                        SymbolImportExportInfo info = new(libraryFileName, symbol, importType);
                        GetOrCreateSymbolEntry(symbol.Name).AddImport(filePath, info);
                    }
                    else
                    {
                        // Due to a weird historical quirk of the design of this transformation, symbols from static libraries don't keep track of the same details as
                        // ones imported from dynamic ones. We still do this here so all the debug-build validaiton above still happens, but it's currently discarded.
                        // (Only advanced users will intentionally pass `.a` archives to this transformation anyway since this transformation doesn't *actually* handle
                        //   symbols only exported from static libraries anyway.)
                        GetOrCreateSymbolEntry(symbol.Name).AddExport(filePath);
                    }
                }
            }

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
                if (!isVirtualMethod || ErrorOnMissingVirtualMethods)
                {
                    Severity severity = ErrorOnMissing ? Severity.Error : Severity.Warning;
                    diagnostics.Add(new TranslationDiagnostic(severity, MakeErrorMessage($"No import sources found for '{symbolName}'.", symbolEntry)));
                }

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

            static string TrimFirstMangleCharacter(string name)
            {
                if (name.Length > 0 && name[0] is '?' or '@' or '_')
                { return name.Substring(1); }

                return name;
            }

            return true;
        }

        /// <summary>Checks whether any libraries registered with this transformation contain the specified symbol</summary>
        /// <remarks>
        /// This method does not necessarily indicate the symbol will resolve successfully, only that some library registered with the transformation contains it.
        /// It does not check things like whether or not the symbol is ambiguous or if it's only provided by a static export.
        /// </remarks>
        public bool ContainsSymbol(string mangledSymbol)
            => Imports.ContainsKey(mangledSymbol);

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
