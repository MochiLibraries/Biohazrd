#pragma once

// This file ensures that using macros in other files (IncludesSomeFiles.h in this case) doesn't trip up the logic responsible for associating cursors with files.
// Even though it messes up libclang's main file check (see ClangSharpExtensions.IsFromMainFile), the file information on the cursor is fine.

// This file also tests TranslatedLibrary's diagnostic emitted when a file has no cursors since this file contains nothing but preprocessor candy.

#define MACRO_DEFINING_FUNCTION(ret_type) ret_type MacroDefininingFunction();