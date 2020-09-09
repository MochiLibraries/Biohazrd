using System;

namespace Biohazrd.OutputGeneration
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class ProvidesOutputSessionFactoryAttribute : Attribute
    { }
}
