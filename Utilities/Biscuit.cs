using System.Text.Json;
using System.Text.Json.Serialization;

namespace YeahGame;

[JsonSerializable(typeof(Biscuit))]
public partial class BiscuitContext : JsonSerializerContext { }

public class Biscuit
{
    public static string? Socket
    {
        get
        {
            Biscuit.Load();
            return _singleton._socket;
        }
        set
        {
            _singleton._socket = value;
            Biscuit.Save();
        }
    }

    public static string? Username
    {
        get
        {
            Biscuit.Load();
            return _singleton._username;
        }
        set
        {
            _singleton._username = value;
            Biscuit.Save();
        }
    }

    public static string? ConnectionType
    {
        get
        {
            Biscuit.Load();
            return _singleton._connectionType;
        }
        set
        {
            _singleton._connectionType = value;
            Biscuit.Save();
        }
    }

    public static int PlayerColor
    {
        get
        {
            Biscuit.Load();
            return _singleton._playerColor;
        }
        set
        {
            _singleton._playerColor = value;
            Biscuit.Save();
        }
    }

    [JsonPropertyName("socket"), JsonRequired, JsonInclude]
    public string? _socket;

    [JsonPropertyName("username"), JsonRequired, JsonInclude]
    public string? _username;

    [JsonPropertyName("connection_type"), JsonRequired, JsonInclude]
    public string? _connectionType;

    [JsonPropertyName("player_color"), JsonRequired, JsonInclude]
    public int _playerColor;

    const string FileName = "biscuit.json";

    static Biscuit _singleton = new();

    public static Action<string>? Saver;
    public static Func<string?>? Loader;

    static void Load()
    {
        string? data = null;

        if (Loader is not null)
        {
            data = Loader.Invoke();
        }

        if (data is null)
        {
            if (!File.Exists(FileName))
            { return; }

            data = File.ReadAllText(FileName);
        }

        try
        {
            _singleton = JsonSerializer.Deserialize<Biscuit>(data, BiscuitContext.Default.Biscuit) ?? new Biscuit();
        }
        catch (JsonException)
        { }
    }

    static void Save()
    {
        string data = JsonSerializer.Serialize<Biscuit>(_singleton, BiscuitContext.Default.Biscuit);

        if (Saver is not null)
        {
            Saver.Invoke(data);
            return;
        }

        File.WriteAllText(FileName, data);
    }
}
