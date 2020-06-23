class Accessibility
{
private:
	int PrivateField;
	void PrivateMethod();
	enum PrivateEnum { PrivateA, PrivateB, PrivateC };
	enum { PrivateAnonymousA, PrivateAnonymousB, PrivateAnonymousC };
	enum class PrivateEnumClass { A, B, C };
	class PrivateClass { };
	struct PrivateStruct { };
protected:
	int ProtectedField;
	void ProtectedMethod();
	enum ProtectedEnum { ProtectedA, ProtectedB, ProtectedC };
	enum { ProtectedAnonymousA, ProtectedAnonymousB, ProtectedAnonymousC };
	enum class ProtectedEnumClass { A, B, C };
	class ProtectedClass { };
	struct ProtectedStruct { };
public:
	int PublicField;
	void PublicMethod();
	enum PublicEnum { PublicA, PublicB, PublicC };
	enum { PublicAnonymousA, PublicAnonymousB, PublicAnonymousC };
	enum class PublicEnumClass { A, B, C };
	class PublicClass { };
	struct PublicStruct { };
};
