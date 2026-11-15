using System.Collections.Generic;

namespace Receive.Core
{
    /// <summary>種から決まる乱数。実行環境が違っても同じ種から同じシナリオができる。</summary>
    public sealed class Rng
    {
        private ulong _s;

        public Rng(long seed)
        {
            _s = unchecked((ulong)seed * 0x9E3779B97F4A7C15UL + 0xD1B54A32D192ED03UL);
            if (_s == 0) _s = 0x2545F4914F6CDD1DUL;
            for (int i = 0; i < 8; i++) NextU();
        }

        private ulong NextU()
        {
            _s ^= _s << 13;
            _s ^= _s >> 7;
            _s ^= _s << 17;
            return _s;
        }

        public int Next(int n) { return n <= 1 ? 0 : (int)((NextU() >> 16) % (ulong)n); }

        public int Range(int minInclusive, int maxInclusive) { return minInclusive + Next(maxInclusive - minInclusive + 1); }

        public bool Chance(double p) { return (NextU() >> 11) * (1.0 / 9007199254740992.0) < p; }

        public T Pick<T>(IReadOnlyList<T> list) { return list[Next(list.Count)]; }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Next(i + 1);
                T t = list[i]; list[i] = list[j]; list[j] = t;
            }
        }
    }
}
