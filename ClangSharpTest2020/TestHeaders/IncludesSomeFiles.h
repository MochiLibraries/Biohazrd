#pragma once
#include "PodClass.h"
// This include is intentionally incorrectly cased to see how Clang reacts, findings are:
// * CXFile.Name will be the incorrectly cased name, but CXFile.TryGetRealPathName will be correct.
// * Clang reports a warning: non-portable path to file '"Enum.h"'; specified path differs in case from file name on disk [-Wnonportable-include-path]
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wnonportable-include-path"
#include "eNUM.h"
#pragma clang diagnostic pop
#include "MacroDefiningAFunction.h"
#include "VirtualChild.h"

MACRO_DEFINING_FUNCTION(long long);
