enum MyEnum
{
	Apple,
	Orange,
	Banana
};

enum class MyEnumClass
{
	Red,
	Green = 1, // <-- Explicit value that is the same as the automatic one.
	Blue,
	Yellow = 99,
};

enum class MyEnumClassShort : short
{
	Hello = 99,
	World = (short)0x3226
};

enum EmptyEnum { };
