namespace YeahGame;

struct MinMax<T> where T : IComparisonOperators<T, T, bool>, IAdditionOperators<T, T, T>, ISubtractionOperators<T, T, T>, IDivisionOperators<T, int, T>
{
    public readonly T Min => _min;
    public readonly T Max => _max;
    public readonly T Average => (_min + _max) / 2;
    public readonly T Difference => _max - _min;

    T _min;
    T _max;
    bool _shouldReset;

    public void Set(T value)
    {
        if (_shouldReset)
        {
            _min = value;
            _max = value;
            _shouldReset = false;
            return;
        }
        if (_min > value) _min = value;
        if (_max < value) _max = value;
    }

    public void Reset()
    {
        _shouldReset = true;
    }
}
