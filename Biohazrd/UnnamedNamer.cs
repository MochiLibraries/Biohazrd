using System.Collections.Generic;

namespace Biohazrd
{
    internal sealed class UnnamedNamer
    {
        // category => count
        private Dictionary<string, int> UnnamedNameCounts = new Dictionary<string, int>();
        public string GetName(string category)
        {
            int oldCount;

            if (!UnnamedNameCounts.TryGetValue(category, out oldCount))
            { oldCount = 0; }

            UnnamedNameCounts[category] = oldCount + 1;
            return $"__unnamed{category}{oldCount}";
        }
    }
}
