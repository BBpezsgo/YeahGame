using System.Text.Json;
using System.Text.Json.Serialization;

namespace YeahGame;

[JsonSerializable(typeof(Biscuit))]
public partial class BiscuitContext : JsonSerializerContext { }

public class Biscuit
{
    public static string Socket
    {
        get
        {
            Biscuit.Load();
            return _singleton._socket ?? throw new NullReferenceException();
        }
        set
        {
            _singleton._socket = value;
            Biscuit.Save();
        }
    }
    public static string Username
    {
        get
        {
            Biscuit.Load();
            return _singleton._username ?? throw new NullReferenceException();
        }
        set
        {
            _singleton._username = value;
            Biscuit.Save();
        }
    }

    [JsonPropertyName("socket"), JsonRequired, JsonInclude]
    public string? _socket;

    [JsonPropertyName("username"), JsonRequired, JsonInclude]
    public string? _username;

    const string FileName = "biscuit.json";

    static Biscuit _default => new()
    {
        _socket = "127.0.0.1:7776",
        _username = "Bruh",
    };
    static Biscuit _singleton = Biscuit._default;

    static void Load()
    {
        _singleton = Biscuit._default;

        if (!File.Exists(FileName))
        { return; }

        try
        {
            _singleton = JsonSerializer.Deserialize<Biscuit>(File.ReadAllText(FileName), BiscuitContext.Default.Biscuit) ?? Biscuit._default;
        }
        catch (JsonException)
        { }
    }

    static void Save()
    {
        File.WriteAllText(FileName, JsonSerializer.Serialize<Biscuit>(_singleton, BiscuitContext.Default.Biscuit));
    }
}
