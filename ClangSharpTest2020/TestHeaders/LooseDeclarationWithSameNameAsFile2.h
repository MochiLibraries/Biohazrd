#pragma once

// This checks if the translator properly avoids CS0542 (member names cannot be the same as their enclosing type)
// In particular, this file checks the behavior when there's a record to have the members associated with it.
struct LooseDeclarationWithSameNameAsFile2
{
};

void LooseDeclarationWithSameNameAsFile2();
