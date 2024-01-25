namespace YeahGame;
struct ColoredGraph
{
    readonly struct ColoredGraphRecord
    {
        public readonly int Value;
        public readonly byte Color;

        public ColoredGraphRecord(int value, byte color)
        {
            Value = value;
            Color = color;
        }
    }

    public readonly int Max => _max;
    public readonly int Min => _min;

    ColoredGraphRecord[] _records;
    ColoredGraphRecord[] _recordsCopy;
    int _max;
    int _min;

    public ColoredGraph(int length)
    {
        if (length < 1) throw new ArgumentException("Length must be more than zero", nameof(length));

        _records = new ColoredGraphRecord[length];
        _recordsCopy = new ColoredGraphRecord[length];
    }

    public void Append(int value, byte color = CharColor.White)
    {
        Array.Copy(_records, 0, _recordsCopy, 1, _records.Length - 1);

        ColoredGraphRecord[] temp = _records;
        _records = _recordsCopy;
        _recordsCopy = temp;

        _records[0] = new ColoredGraphRecord(value, color);

        int _currMin = int.MaxValue;
        int _currMax = int.MinValue;
        for (int i = 0; i < _records.Length; i++)
        {
            _currMax = Math.Max(_currMax, _records[i].Value);
            _currMin = Math.Min(_currMin, _records[i].Value);
        }
        _max = _currMax;
        _min = _currMin;
    }

    public readonly void Render(SmallRect rect, IRenderer<ConsoleChar> renderer, bool labels)
    {
        int max = Math.Max(rect.Height - 1, _max);

        renderer.Box(rect, CharColor.Black, CharColor.Gray, in Ascii.BoxSides);

        for (int i = 0; i < _records.Length; i++)
        {
            float value = (max == 0) ? 0f : (float)_records[i].Value / (float)max;
            value = 1f - value;

            int y = (int)MathF.Round(rect.Height * value);
            int x = (int)MathF.Round(((float)i / (float)_records.Length) * rect.Width);

            for (int j = rect.Y + y; j <= rect.Bottom; j++)
            {
                if (renderer.IsVisible(rect.X + x, j))
                {
                    if (j == rect.Bottom)
                    { renderer[rect.X + x, j] = new ConsoleChar(Ascii.Blocks.Top, _records[i].Color); }
                    else if (j == rect.Y + y)
                    { renderer[rect.X + x, j] = new ConsoleChar(Ascii.Blocks.Bottom, _records[i].Color); }
                    else
                    { renderer[rect.X + x, j] = new ConsoleChar(Ascii.Blocks.Full, _records[i].Color); }
                }
            }
        }

        if (labels)
        {
            renderer.Text(rect.Right + 1, rect.Top, _max.ToString(), CharColor.Gray);
        }
    }
}
