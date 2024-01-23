using System.Diagnostics.CodeAnalysis;

namespace YeahGame;

public struct Changed<T> where T : notnull
{
    T _value;
    bool _wasChanged;

    public T Value
    {
        readonly get => _value;
        set
        {
            _value = value;
            _wasChanged = true;
        }
    }

    public bool WasChanged
    {
        readonly get => _wasChanged;
        set => _wasChanged = true;
    }

    public Changed(T value, bool wasChanged = true)
    {
        _value = value;
        _wasChanged = wasChanged;
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj) => obj is Changed<T> _v && _v._value.Equals(_value);

    public override readonly int GetHashCode() => _value.GetHashCode();

    public override readonly string? ToString() => _value.ToString();

    public static bool operator ==(Changed<T> left, Changed<T> right) => left._value.Equals(right._value);
    public static bool operator !=(Changed<T> left, Changed<T> right) => !left._value.Equals(right._value);

    public static implicit operator T(Changed<T> v) => v._value;
    public static implicit operator Changed<T>(T v) => new(v);
}
