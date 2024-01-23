using System.Net;
using YeahGame.Messages;

namespace YeahGame;

public class Chat
{
    const int MessagesShownWhenInactive = 5;
    const int MessagesShownWhenActive = 15;

    public struct Message
    {
        public (bool IsServer, IPEndPoint? EndPoint) Source;
        public float Time;
        public string Content;

        public readonly string TimePrefix => $"[{TimeSpan.FromSeconds(Time):hh\\:mm\\:ss}]";
        public readonly string SourcePrefix
        {
            get
            {
                if (Game.Connection.TryGetUserInfo(Source.EndPoint, out ConnectionUserInfo<PlayerInfo> sourceInfo) &&
                    sourceInfo.Info is not null)
                { return $"<{sourceInfo.Info.Username}>"; }
                else if (Source.EndPoint is not null)
                { return $"<{Source.EndPoint}>"; }
                else if (Source.IsServer)
                { return $"<SERVER>"; }
                else
                { return $"<?>"; }
            }
        }
        public readonly string ContentText => Content.Trim();

        public Message((bool IsServer, IPEndPoint? EndPoint) source, float time, string content)
        {
            Source = source;
            Time = time;
            Content = content;
        }
    }

    public SmallRect Rect
    {
        get
        {
            Message[] messages = GetMessages().ToArray();

            SmallRect result = new(
                1, Game.Renderer.Height - 2 - Math.Clamp(messages.Length, 0, _isChatting ? MessagesShownWhenActive : MessagesShownWhenInactive),
                0, Math.Clamp(messages.Length - 1, 0, _isChatting ? MessagesShownWhenActive : MessagesShownWhenInactive));

            for (int i = 0; i < messages.Length; i++)
            {
                result.Width = Math.Max(result.Width, (short)(messages[i].TimePrefix.Length + messages[i].SourcePrefix.Length + messages[i].ContentText.Length + 2));
            }

            if (_isChatting)
            {
                result.Height += 1;
                result.Width = Math.Max(result.Width, (short)(4 + 30));
            }

            result = result.Margin(-1);

            return result;
        }
    }
    public bool IsChatting => _isChatting;

    readonly List<Message> _chatMessages = new()
    {
        // new Message((true, null), (float)Time.NowNoCache, "a"),
        // new Message((true, null), (float)Time.NowNoCache, "b"),
        // new Message((true, null), (float)Time.NowNoCache, "c"),
        // new Message((true, null), (float)Time.NowNoCache, "d"),
        // new Message((true, null), (float)Time.NowNoCache, "e"),
        // new Message((true, null), (float)Time.NowNoCache, "f"),
        // new Message((true, null), (float)Time.NowNoCache, "g"),
        // new Message((true, null), (float)Time.NowNoCache, "h"),
        // new Message((true, null), (float)Time.NowNoCache, "i"),
        // new Message((true, null), (float)Time.NowNoCache, "j"),
        // new Message((true, null), (float)Time.NowNoCache, "k"),
        // new Message((true, null), (float)Time.NowNoCache, "l"),
        // new Message((true, null), (float)Time.NowNoCache, "m"),
        // new Message((true, null), (float)Time.NowNoCache, "n"),
        // new Message((true, null), (float)Time.NowNoCache, "o"),
        // new Message((true, null), (float)Time.NowNoCache, "p"),
    };
    bool _isChatting = false;
    readonly ConsoleInputField _chatInput = new(null) { NeverLoseFocus = true };
    int _scroll = 0;

    bool _isSending = false;

    void OnSent()
    {
        _isSending = false;
    }

    public void Render()
    {
        if (Keyboard.IsKeyDown('\r'))
        {
            if (_isChatting)
            {
                string msgContent = _chatInput.Value.ToString().Trim();
                _chatInput.Clear();

                if (!string.IsNullOrEmpty(msgContent))
                {
                    _isSending = true;
                    Game.Connection.Send(new ChatMessage()
                    {
                        Content = msgContent,
                        Source = Game.Connection.LocalEndPoint,
                        SourceIsServer = Game.Connection.IsServer,
                        Time = Time.Now,

                        ShouldAck = true,
                        Callback = OnSent,
                    });

                    if (Game.Connection.IsServer)
                    {
                        Add(new Message((true, Game.Connection.LocalEndPoint), Time.Now, msgContent));
                    }
                }

                _isChatting = false;
            }
            else
            {
                _chatInput.Clear();
                _isChatting = true;
            }
        }

        int y = Game.Renderer.Height - 4;
        Message[] messages = GetMessages().ToArray();
        for (int i = 0; i < messages.Length; i++)
        {
            ref Message message = ref messages[i];
            string timeText = message.TimePrefix;
            string sourceText = message.SourcePrefix;
            string contentText = message.ContentText;

            int x = 1;

            Game.Renderer.Text(x, y, timeText, CharColor.Gray);
            x += timeText.Length + 1;

            Game.Renderer.Text(x, y, sourceText, CharColor.Silver);
            x += sourceText.Length + 1;

            Game.Renderer.Text(x, y, contentText, CharColor.Silver);
            // x += contentText.Length + 1;
            y--;
        }

        if (_isSending)
        { Game.Renderer.Text(2, Game.Renderer.Height - 3, "Sending ..."); }

        if (_isChatting)
        {
            if (Rect.Contains(Mouse.RecordedConsolePosition))
            {
                _scroll += Mouse.ScrollDelta;
                _scroll = Math.Clamp(_scroll, 0, messages.Length);
            }

            _chatInput.IsActive = true;
            Game.Renderer.Text(2, Game.Renderer.Height - 2, ">");
            Game.Renderer.InputField(new SmallRect(4, Game.Renderer.Height - 2, 30, 1), Styles.InputFieldStyle, _chatInput);
        }
    }

    void Add(Message message)
    {
        _chatMessages.Add(message);
        _chatMessages.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    public void Feed(ChatMessage message, IPEndPoint source)
    {
        Add(new Message((message.SourceIsServer, message.Source ?? source), message.Time, message.Content));
    }

    public IEnumerable<Message> GetMessages()
    {
        if (_isChatting)
        {
            int last = Math.Max(0, _chatMessages.Count - MessagesShownWhenActive);

            for (int i = _chatMessages.Count - 1; i >= last; i--)
            {
                yield return _chatMessages[i];
            }
        }
        else
        {
            int last = Math.Max(0, _chatMessages.Count - MessagesShownWhenInactive);

            for (int i = _chatMessages.Count - 1; i >= last; i--)
            {
                if (Time.Now - _chatMessages[i].Time > 10) continue;

                yield return _chatMessages[i];
            }
        }
    }
}
