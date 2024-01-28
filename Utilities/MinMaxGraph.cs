namespace YeahGame;

using MinMaxGraphRecord = (int Min, int Max);

struct MinMaxGraph
{
    public readonly int Max => _max;
    public readonly int Min => _min;

    MinMaxGraphRecord[] _records;
    MinMaxGraphRecord[] _recordsCopy;
    int _max;
    int _min;

    public MinMaxGraph(int length)
    {
        if (length < 1) throw new ArgumentException("Length must be more than zero", nameof(length));

        _records = new MinMaxGraphRecord[length];
        _recordsCopy = new MinMaxGraphRecord[length];
    }

    public void Append(MinMax<int> record) => Append((record.Min, record.Max));
    public void Append(MinMaxGraphRecord record)
    {
        Array.Copy(_records, 0, _recordsCopy, 1, _records.Length - 1);

        MinMaxGraphRecord[] temp = _records;
        _records = _recordsCopy;
        _recordsCopy = temp;

        _records[0] = record;

        int _currMin = int.MaxValue;
        int _currMax = int.MinValue;
        for (int i = 0; i < _records.Length; i++)
        {
            _currMax = Math.Max(_currMax, _records[i].Max);
            _currMin = Math.Min(_currMin, _records[i].Min);
        }
        _max = _currMax;
        _min = _currMin;
    }

    public readonly void Render(SmallRect rect, Renderer<ConsoleChar> renderer, bool labels)
    {
        int max = Math.Max(rect.Height, _max);

        renderer.Box(rect, CharColor.Black, CharColor.Gray);

        for (int i = 0; i < _records.Length; i++)
        {
            float maxValue = (max == 0) ? 0f : (float)_records[i].Max / (float)max;
            float minValue = (max == 0) ? 0f : (float)_records[i].Min / (float)max;

            maxValue = 1f - maxValue;
            minValue = 1f - minValue;

            int yMax = (int)MathF.Round(rect.Height * maxValue);
            yMax += rect.Y;

            int yMin = (int)MathF.Round(rect.Height * minValue);
            yMin += rect.Y;

            int x = (int)MathF.Round(((float)i / (float)_records.Length) * rect.Width);
            x += rect.X;

            for (int j = yMax; j <= rect.Bottom; j++)
            {
                if (renderer.IsVisible(x, j))
                {
                    char character;
                    if (j == rect.Bottom) character = Ascii.Blocks.Top;
                    else if (j == yMax) character = Ascii.Blocks.Bottom;
                    else character = Ascii.Blocks.Full;

                    byte color;
                    if (j < yMin) color = CharColor.Silver;
                    else color = CharColor.White;

                    renderer[x, j] = new ConsoleChar(character, color);
                }
            }
        }

        if (labels)
        {
            renderer.Text(rect.Right + 1, rect.Top, _max.ToString(), CharColor.Gray);
        }
    }
}
