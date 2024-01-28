using Win32.Common;

namespace YeahGame;

public struct Touch
{
    #region Static Stuff

    static readonly Dictionary<int, Point> _touches = new();
    public static bool IsTouchDevice { get; private set; } = false;

    public static bool TryGetTouch(int id, out Point point) => _touches.TryGetValue(id, out point);

    public static void Feed(Touch[] touches)
    {
        IsTouchDevice = true;
        _touches.Clear();
        for (int i = 0; i < touches.Length; i++)
        {
            _touches[touches[i].Id] = touches[i].Position;
        }
    }

    public static Dictionary<int, Point> UnsafeGetTouches() => _touches;
    public static void UnsafeSetIsTouchDevice(bool value) => IsTouchDevice = value;

    public static (bool IsAny, Touch Touch) First
    {
        get
        {
            if (_touches.Count == 0) return (false, default);
            (int id, Point position) = _touches.First();
            return (true, new Touch(id, position));
        }
    }

    public static IEnumerable<Touch> Touches => _touches.Select(item => new Touch(item.Key, item.Value));

    #endregion

    #region Instance Stuff

    public int Id;
    public Point Position;

    public Touch(int id, Point position)
    {
        Id = id;
        Position = position;
    }

    #endregion
}
