namespace Biohazrd
{
    public record TranslationOptions
    {
        /// <summary>If true (the default), Biohazrd will never process declarations from system headers.</summary>
        /// <remarks>
        /// "System headers" uses the definition Clang uses for system headers: https://clang.llvm.org/docs/UsersManual.html#controlling-diagnostics-in-system-headers
        /// 
        /// Generally you should leave this enabled unless you're intentionally processing system headers with Biohazrd.
        /// </remarks>
        public bool SystemHeadersAreAlwaysOutOfScope { get; init; } = true;

        /// <summary>If true, Biohazrd will process macros even if they are defined out of scope. (Default is false.)</summary>
        public bool IncludeMacrosDefinedOutOfScope { get; init; } = false;

        /// <summary>If true, Biohazrd will process macros which were <c>#undef</c>'d. (Default is false.)</summary>
        /// <remarks>Macros which have been undefined cannot be evaluated and will have <see cref="TranslatedMacro.WasUndefined"/> set to true.</remarks>
        public bool IncludeUndefinedMacros { get; init; } = false;

        /// <summary>If true, Biohazrd will process macros which were synthesized by Clang or defined on the command line. (Default is false.)</summary>
        public bool IncludeSynthesizedMacros { get; init; } = false;

        /// <summary>If true, Biohazrd will process supported C++ templates. (Default is false since this feature is experimental and not always desired.)</summary>
        /// <remarks>
        /// This only affects if <see cref="TranslatedTemplateSpecialization"/> will appear in the declaration tree.
        /// Implicitly specialized templates will still be late-instantiated.
        /// </remarks>
        public bool EnableTemplateSupport { get; init; } = false;
    }
}
