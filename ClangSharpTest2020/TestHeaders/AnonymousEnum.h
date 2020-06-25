class AnonymousEnum
{
public:
    enum
    {
        Red,
        Green,
        Blue
    };

    int FieldAfterEnum;
};


class AnonymousEnumWithField
{
public:
    enum
    {
        Red,
        Green,
        Blue
    } AnonymousEnumField;

    int FieldAfterEnum;
};
