using Win32.Common;

namespace YeahGame;

public class CapturedTouch
{
    int _id;
    bool _has;

    public bool Has => _has;
    public int Id => _id;
    public Point Position => Touch.TryGetTouch(_id, out Point position) ? position : default;

    static readonly WeakList<CapturedTouch> capturedTouches = new();

    public CapturedTouch()
    {
        _id = -1;
        _has = false;
        capturedTouches.Add(this);
    }

    public static IEnumerable<Touch> UnusedTouches
    {
        get
        {
            foreach (Touch touch in Touch.Touches)
            {
                if (IsTouchUsed(touch.Id)) continue;
                yield return touch;
            }
        }
    }

    public static bool IsTouchUsed(int id)
    {
        foreach (CapturedTouch? item in capturedTouches)
        {
            if (item is null) continue;
            if (item._has && item._id == id) return true;
        }
        return false;
    }

    public static (bool IsAny, Touch Touch) FirstUnusedTouch()
    {
        foreach (Touch touch in Touch.Touches)
        {
            if (IsTouchUsed(touch.Id)) continue;
            return (true, touch);
        }
        return (false, default);
    }

    public static bool FirstUnusedTouch(out Touch touch)
    {
        foreach (Touch _touch in Touch.Touches)
        {
            if (IsTouchUsed(_touch.Id)) continue;

            touch = _touch;
            return true;
        }

        touch = default;
        return false;
    }

    public void Tick(Func<Point, bool>? captureIf)
    {
        if (_has && Touch.TryGetTouch(_id, out _))
        { return; }

        _id = -1;
        _has = false;

        if (captureIf is not null)
        {
            foreach (Touch touch in CapturedTouch.UnusedTouches)
            {
                if (captureIf.Invoke(touch.Position))
                {
                    _has = true;
                    _id = touch.Id;
                    break;
                }
            }
        }
    }
}
