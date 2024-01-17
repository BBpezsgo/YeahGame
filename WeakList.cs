using System.Collections;

namespace YeahGame;

public struct WeakListEnumerator<T> : IEnumerator<T?> where T : class
{
    readonly List<WeakReference<T>> _list;
    private int _position = -1;

    public WeakListEnumerator(List<WeakReference<T>> list) => _list = list;

    public readonly T? Current
    {
        get
        {
            try
            {
                return _list[_position].TryGetTarget(out T? value) ? value : null;
            }
            catch (IndexOutOfRangeException)
            {
                throw new InvalidOperationException();
            }
        }
    }

    readonly object? IEnumerator.Current => Current;

    public void Dispose()
    {
        _position = -1;
    }

    public bool MoveNext()
    {
        if (_list == null) { return false; }
        _position++;
        return _position < _list.Count;
    }

    public void Reset()
    {
        for (int i = _list.Count - 1; i >= 0; i--)
        {
            if (!_list[i].TryGetTarget(out _))
            { _list.RemoveAt(i); }
        }

        _position = -1;
    }
}

public class WeakList<T> : IList<T?>, IReadOnlyList<T?> where T : class
{
    readonly List<WeakReference<T>> _list;

    public T? this[int index]
    {
        get => _list[index].TryGetTarget(out T? item) ? item : null;

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _list[index].SetTarget(value);
        }
    }

    public int Count => _list.Count;

    public bool IsReadOnly => false;

    public WeakList() => _list = new List<WeakReference<T>>();
    public WeakList(int capacity) => _list = new List<WeakReference<T>>(capacity);

    public void Add(T? item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _list.Add(new WeakReference<T>(item));
    }

    public void Clear()
    {
        _list.Clear();
    }

    public bool Contains(T? item) => Count != 0 && IndexOf(item) >= 0;

    public void CopyTo(T?[] array, int arrayIndex)
    {
        lock (array.SyncRoot)
        {
            int i = 0;
            while (i < _list.Count && i + arrayIndex < array.Length)
            {
                T? v = this[i];
                while (v == null && i < _list.Count)
                {
                    _list.RemoveAt(i);
                    v = this[i];
                }

                if (v != null)
                { array.SetValue(v, i + arrayIndex); }

                i++;
            }
        }
    }

    public int IndexOf(T? item)
    {
        ArgumentNullException.ThrowIfNull(item);

        int i = 0;
        while (i < _list.Count)
        {
            T? current = this[i];

            if (current == null)
            {
                _list.RemoveAt(i);
                i--;
            }
            else if (current == item)
            {
                return i;
            }

            i++;
        }

        return -1;
    }

    public void Insert(int index, T? item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _list.Insert(index, new WeakReference<T>(item));
    }

    public bool Remove(T? item)
    {
        int index = IndexOf(item);
        if (index == -1) return false;
        _list.RemoveAt(index);
        return true;
    }

    public void RemoveAt(int index) => _list.RemoveAt(index);

    public IEnumerator<T?> GetEnumerator() => new WeakListEnumerator<T>(_list);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
