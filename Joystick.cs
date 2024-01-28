using Win32.Common;

namespace YeahGame;

public class Joystick
{
    public SmallRect Rect;
    public int DotSize;
    public Vector2 Input => _input;

    readonly CapturedTouch capturedTouch;
    Vector2 _input;

    public Joystick(SmallRect rect, int dotSize)
    {
        this.capturedTouch = new CapturedTouch();
        this.Rect = rect;
        this.DotSize = dotSize;
        this._input = default;
    }

    public void Render(Renderer<ConsoleChar> renderer)
    {
        renderer.Box(Rect, CharColor.Gray);

        SmallRect joystickDot = new(Rect.Center - new Coord(1, 1), new Coord(3, 2));

        Coord originalPosition = joystickDot.Center;

        capturedTouch.Tick(joystickDot.Contains);

        if (capturedTouch.Has)
        {
            joystickDot.Center = (Coord)capturedTouch.Position;

            {
                Coord _ah = joystickDot.Center;
                _ah.X = Math.Clamp(_ah.X, Rect.Left, Rect.Right);
                _ah.Y = Math.Clamp(_ah.Y, Rect.Top, Rect.Bottom);
                joystickDot.Center = _ah;
            }

            _input = (Vector2)joystickDot.Center - (Vector2)originalPosition;
            _input = Vector2.Normalize(_input);
        }
        else
        {
            _input = default;
        }

        renderer.Fill(joystickDot, ConsoleChar.Empty);
        renderer.Box(joystickDot, CharColor.Black, CharColor.White, SideCharacters.BoxSidesShadow);
    }
}
