using System.Security.Cryptography;

namespace TOEFLSimulation.Core;

/// <summary>
/// The Mac app's <c>SeededGenerator</c> (SplitMix64) plus Swift's standard-library algorithms that consume it
/// (<c>Int.random(in:using:)</c> with Lemire's method and <c>shuffle(using:)</c>), so the same seed builds the same form.
/// </summary>
public sealed class SeededGenerator
{
    private ulong state;

    public SeededGenerator(ulong seed)
    {
        unchecked { state = seed + 0x9e3779b97f4a7c15UL; }
    }

    public ulong Next()
    {
        unchecked
        {
            state += 0x9e3779b97f4a7c15UL;
            var value = state;
            value = (value ^ (value >> 30)) * 0xbf58476d1ce4e5b9UL;
            value = (value ^ (value >> 27)) * 0x94d049bb133111ebUL;
            return value ^ (value >> 31);
        }
    }

    /// <summary>Swift <c>RandomNumberGenerator.next(upperBound:)</c> (Lemire's nearly divisionless method).</summary>
    public ulong Next(ulong upperBound)
    {
        if (upperBound == 0) throw new ArgumentOutOfRangeException(nameof(upperBound));
        var random = Next();
        var high = Math.BigMul(random, upperBound, out var low);
        if (low < upperBound)
        {
            var t = unchecked(0UL - upperBound) % upperBound;
            while (low < t)
            {
                random = Next();
                high = Math.BigMul(random, upperBound, out low);
            }
        }
        return high;
    }

    /// <summary>Swift <c>Int.random(in: 0..&lt;upperBound, using:)</c>.</summary>
    public int NextInt(int upperBound) => (int)Next((ulong)upperBound);

    /// <summary>Swift <c>Array.shuffled(using:)</c>.</summary>
    public List<T> Shuffled<T>(IEnumerable<T> source)
    {
        var list = source.ToList();
        Shuffle(list);
        return list;
    }

    /// <summary>Swift <c>MutableCollection.shuffle(using:)</c>.</summary>
    public void Shuffle<T>(IList<T> list)
    {
        if (list.Count <= 1) return;
        var amount = list.Count;
        var current = 0;
        while (amount > 1)
        {
            var random = NextInt(amount);
            amount--;
            (list[current], list[current + random]) = (list[current + random], list[current]);
            current++;
        }
    }

    /// <summary>Swift <c>UInt64.random(in: UInt64.min...UInt64.max)</c> from the system generator.</summary>
    public static ulong RandomSeed()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return BitConverter.ToUInt64(bytes);
    }
}
