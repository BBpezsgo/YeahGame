using System.Net;

namespace YeahGame;

public static class Ipify
{
    static readonly Uri ApiUri = new("http://api.ipify.org", UriKind.Absolute);
    static IPAddress? SavedExternalAddress;

    public static IPAddress? ExternalAddress
    {
        get
        {
            if (SavedExternalAddress is not null)
            { return SavedExternalAddress; }

            using HttpClient client = new();
            try
            {
                Task<string> task = client.GetStringAsync(ApiUri);
                task.Wait();

                if (!task.IsCompletedSuccessfully)
                { return null; }

                if (!IPAddress.TryParse(task.Result, out IPAddress? address))
                { return null; }
                
                return SavedExternalAddress = address;
            }
            catch (Exception)
            { return null; }
        }
    }
}
