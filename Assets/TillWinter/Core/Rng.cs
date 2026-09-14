namespace TillWinter.Core
{
    /// <summary>Tiny xorshift32 RNG with an exportable state so saves stay deterministic across platforms.</summary>
    public sealed class Rng
    {
        public uint State { get; private set; }

        public Rng(int seed) : this(unchecked((uint)seed * 2654435761u + 0x9E3779B9u)) { }

        public Rng(uint state)
        {
            State = state == 0 ? 0x9E3779B9u : state;
        }

        public uint NextUInt()
        {
            uint x = State;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State = x;
            return x;
        }

        /// <summary>0 &lt;= result &lt; maxExclusive.</summary>
        public int Next(int maxExclusive) => maxExclusive <= 0 ? 0 : (int)(NextUInt() % (uint)maxExclusive);

        /// <summary>[0, 1).</summary>
        public double NextDouble() => (NextUInt() >> 8) / (double)(1u << 24);
    }
}
