typedef unsigned int TypedefAtRoot;

class Typedefs
{
public:
    typedef unsigned int TypedefInClass;

    void Method()
    {
        typedef unsigned int TypedefInMethodBody;
    }
private:
    typedef unsigned int PrivateTypedefInClass;
};

namespace TypedefNamespace
{
    typedef unsigned int TypedefInNamespace;
}

static TypedefAtRoot TypedefAtRootVariable;
static Typedefs::TypedefInClass TypedefInClassVariable;
static TypedefNamespace::TypedefInNamespace TypedefInNamespaceVariable;
