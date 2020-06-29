#pragma once

// Some libraries use forward declarations of types which aren't meant to be interacted with directly to hide the functionality of a type.
// Therefore, we want to translate them as *something* so that pointers to them can be passed around.
// (This should be named `UndefinedType`, but this messes with the generation of the loose members container right now.)
class TheUndefinedType;

TheUndefinedType* GetPointer();
TheUndefinedType& GetReference();

class DefinedLaterType;

DefinedLaterType* GetPointer2();
DefinedLaterType& GetReference2();

class DefinedLaterType
{
public:
    int x;
};
