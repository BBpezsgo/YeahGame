namespace YeahGame;

public static class Interval
{
    public static Interval<T2> GetFixed<T2>(Interval<T2> range) where T2 : IEqualityOperators<T2, T2, bool>, IComparisonOperators<T2, T2, bool>
    {
        if (range.Min < range.Max) return new Interval<T2>(range.Min, range.Max);
        else return new Interval<T2>(range.Max, range.Min);
    }
}

public readonly struct Interval<T> : IEquatable<Interval<T>> where T : IEqualityOperators<T, T, bool>
{
    public readonly T Min;
    public readonly T Max;

    public Interval(T min, T max)
    {
        Min = min;
        Max = max;
    }

    public override bool Equals(object? obj) => obj is Interval<T> range && Equals(range);
    public bool Equals(Interval<T> other) => Min == other.Min && Max == other.Max;
    public override int GetHashCode() => HashCode.Combine(Min, Max);

    public static bool operator ==(Interval<T> left, Interval<T> right) => left.Equals(right);
    public static bool operator !=(Interval<T> left, Interval<T> right) => !left.Equals(right);

    public override string ToString() => $"({Min} -> {Max})";

    public static implicit operator Interval<T>(ValueTuple<T, T> v) => new(v.Item1, v.Item2);
    public static implicit operator Interval<T>(T v) => new(v, v);
    public static implicit operator ValueTuple<T, T>(Interval<T> v) => new(v.Min, v.Max);

    public void Deconstruct(out T min, out T max)
    {
        min = Min;
        max = Max;
    }
}
