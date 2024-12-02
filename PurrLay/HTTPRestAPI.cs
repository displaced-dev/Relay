using HttpServerLite;
using Newtonsoft.Json.Linq;

namespace PurrLay;

public static class HTTPRestAPI
{
    static WebSockets? _server;
    private static string? _region;
    
    public static async Task RegisterRoom(string roomName)
    {
        using HttpClient client = new();
                
        client.DefaultRequestHeaders.Add("name", roomName);
        client.DefaultRequestHeaders.Add("region", _region);
        client.DefaultRequestHeaders.Add("internal_key_secret", Program.SECRET_INTERNAL);
        
        var response = await client.PostAsync("https://purrbalancer.riten.dev:8080/registerRoom", null);
        
        if (!response.IsSuccessStatusCode)
            throw new Exception("Failed to register room");
    }
    
    public static async Task unegisterRoom(string roomName)
    {
        using HttpClient client = new();
                
        client.DefaultRequestHeaders.Add("name", roomName);
        client.DefaultRequestHeaders.Add("internal_key_secret", Program.SECRET_INTERNAL);
        
        var response = await client.PostAsync("https://purrbalancer.riten.dev:8080/runegisterRoom", null);
        
        if (!response.IsSuccessStatusCode)
            throw new Exception("Failed to register room");
    }
    
    [Serializable]
    internal struct ClientJoinInfo
    {
        public string? secret;
        public int port;
    }
    
    public static async Task<JObject> OnRequest(HttpRequest req)
    {
        if (req.Url == null)
            throw new Exception("Invalid URL");

        string path = req.Url.WithoutQuery;

        switch (path)
        {
            case "/ping": return new JObject();
            case "/getJoinDetails":
            {
                var name = req.RetrieveHeaderValue("name");

                if (string.IsNullOrWhiteSpace(name))
                    throw new Exception("Missing name");

                if (_server == null)
                    throw new Exception("No rooms available");
                
                if (!Lobby.TryGetRoom(name, out var room) || room == null)
                    throw new Exception("Room not found");

                return JObject.FromObject(new ClientJoinInfo
                {
                    port = _server.port,
                    secret = room.clientSecret
                });
            }
            case "/allocate_ws":
            {
                var name = req.RetrieveHeaderValue("name");
                var region = req.RetrieveHeaderValue("region");
                var internalSec = req.RetrieveHeaderValue("internal_key_secret");
                
                if (string.IsNullOrWhiteSpace(name))
                    throw new Exception("Missing name");
                
                if (string.IsNullOrWhiteSpace(region))
                    throw new Exception("Missing region");
                
                if (string.IsNullOrWhiteSpace(internalSec))
                    throw new Exception("Bad internal secret, -1");

                if (!string.Equals(internalSec, Program.SECRET_INTERNAL))
                    throw new Exception($"Bad internal secret, {internalSec.Length}");
                
                _region ??= region;
                
                var secret = await Lobby.CreateRoom(name);
                
                _server ??= new WebSockets(6942);

                return new JObject
                {
                    ["secret"] = secret,
                    ["port"] = _server.port
                };
            }
        }
        
        throw new Exception("Invalid path");
    }
}