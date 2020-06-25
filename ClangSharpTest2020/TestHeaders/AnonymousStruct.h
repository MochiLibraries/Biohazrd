class AnonymousStruct
{
public:
    struct
    {
        int FieldInAnonymousStruct;
    } AnonymousStructField;

    struct
    {
        int FieldInAnonymousStruct;
    } AnonymousStructField2;
};

class AnonymousStructInception
{
public:
    struct
    {
        struct
        {
            int FieldInAnonymousStruct;
        } AnonymousStructField;
    } AnonymousStructField;
};
