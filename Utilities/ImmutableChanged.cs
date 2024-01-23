using System.Diagnostics.CodeAnalysis;

namespace YeahGame;

public struct ImmutableChanged<T> where T : notnull
{
    bool _wasChanged;

    public readonly T Value;
    public bool WasChanged
    {
        readonly get => _wasChanged;
        set => _wasChanged = true;
    }

    public ImmutableChanged(T value, bool wasChanged = true)
    {
        Value = value;
        _wasChanged = wasChanged;
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is ImmutableChanged<T> _v && _v.Value.Equals(Value);

    public override readonly int GetHashCode() => Value.GetHashCode();

    public override readonly string? ToString() => Value.ToString();

    public static bool operator ==(ImmutableChanged<T> left, ImmutableChanged<T> right) => left.Value.Equals(right.Value);
    public static bool operator !=(ImmutableChanged<T> left, ImmutableChanged<T> right) => !left.Value.Equals(right.Value);

    public static implicit operator T(ImmutableChanged<T> v) => v.Value;
    public static implicit operator ImmutableChanged<T>(T v) => new(v);
}
