using System.Diagnostics.CodeAnalysis;

namespace YeahGame;

public struct Bitfield
{
    uint _value;

    const uint Zero = 0;
    const uint One = 1;

    public bool this[int offset]
    {
        readonly get
        {
            if (offset < 0 || offset >= 32) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be greater than or equal to 0 and less than 32");

            return (_value & (One << offset)) != Zero;
        }
        set
        {
            if (offset < 0 || offset >= 32) throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be greater than or equal to 0 and less than 32");

            byte v = (byte)(One << offset);
            if (value)
            { _value |= v; }
            else
            { _value = (_value | v) ^ v; }
        }
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is uint _uint) return _uint == _value;
        if (obj is int _int) return _int == _value;
        if (obj is Bitfield _bitField) return _bitField._value == _value;
        return false;
    }
    public override readonly int GetHashCode() => unchecked((int)_value);
    public override readonly string ToString() => Convert.ToString(_value, 2).PadLeft(8, '0');

    public static bool operator ==(Bitfield left, Bitfield right) => left._value == right._value;
    public static bool operator !=(Bitfield left, Bitfield right) => left._value != right._value;

    public static implicit operator uint(Bitfield bitfield) => bitfield._value;
    public static implicit operator Bitfield(uint bitfield) => new() { _value = bitfield };
}
