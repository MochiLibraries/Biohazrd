using System.Diagnostics;
using System.Text;

namespace Biohazrd
{
    /// <summary>Represents a reference to a type.</summary>
    /// <remarks>
    /// Notes to implementors:
    /// Type references and its children are expected to implement logical value equality.
    /// If your implementation has inconsequential private members (such as caching for lazy evaluations), you must manually implement <c>Equals(T)</c> and <see cref="GetHashCode"/>.
    /// </remarks>
    public abstract record TypeReference
    {
        /// <summary>True if, this type must be passed/returned by reference.</summary>
        public bool MustBePassedByReference { get; init; }

        public override string ToString()
            => GetType().Name;

        protected virtual bool PrintMembers(StringBuilder builder)
        {
            Debug.Fail("Records which inherit from TypeReference are expected to override ToString.");
            return false;
        }
    }
}
