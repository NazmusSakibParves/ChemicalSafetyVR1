using System;

namespace ChemicalSafety.Localization
{
    /// <summary>
    /// Deterministic option order per (session, question). The same seed always gives the same order, so a wrong
    /// retry inside one visit keeps the layout; a new session (new seed) moves the correct answer.
    /// </summary>
    public static class OptionShuffle
    {
        public static int[] Order(int count, string sessionId, string questionId, bool shuffle)
        {
            var order = new int[count];
            for (var i = 0; i < count; i++)
                order[i] = i;
            if (!shuffle || count < 2)
                return order;

            var state = Hash(sessionId + "|" + questionId);
            for (var i = count - 1; i > 0; i--)
            {
                state = Next(state);
                var j = (int)(state % (uint)(i + 1));
                (order[i], order[j]) = (order[j], order[i]);
            }

            return order;
        }

        // FNV-1a, stable across platforms (string.GetHashCode is not)
        private static uint Hash(string s)
        {
            var h = 2166136261u;
            foreach (var c in s)
            {
                h ^= c;
                h *= 16777619u;
            }

            return h == 0 ? 1u : h;
        }

        private static uint Next(uint x)
        {
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return x;
        }
    }
}
