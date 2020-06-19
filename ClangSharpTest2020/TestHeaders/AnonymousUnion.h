struct AnonymousUnionWithoutFieldName
{
public:
	union
	{
		int Integer;
		float Float;
	};
	int AfterUnion;
};

struct AnonymousUnionWithFieldName
{
public:
	union
	{
		int Integer;
		float Float;
	} UnionField;
	int AfterUnion;
};
