using System.Text;
using HttpServerLite;
using Newtonsoft.Json.Linq;
using HttpMethod = System.Net.Http.HttpMethod;

namespace PurrLay;

public static class HTTPRestAPI
{
    static WebSockets? _server;
    
#if DEBUG
    const string PROTOCOL = "http";
#else
    const string PROTOCOL = "https";
#endif
    
    public static async Task RegisterRoom(string region, string roomName)
    {
        using HttpClient client = new();
                
        client.DefaultRequestHeaders.Add("name", roomName);
        client.DefaultRequestHeaders.Add(region, region);
        client.DefaultRequestHeaders.Add("internal_key_secret", Program.SECRET_INTERNAL);
        
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, 
            $"{PROTOCOL}://purrbalancer.riten.dev:8080/registerRoom"));
        
        if (!response.IsSuccessStatusCode)
        {
            var content = response.Content.ReadAsByteArrayAsync();
            var contentStr = Encoding.UTF8.GetString(content.Result);
            throw new Exception(contentStr);
        }
    }
    
    public static async Task unegisterRoom(string roomName)
    {
        using HttpClient client = new();
                
        client.DefaultRequestHeaders.Add("name", roomName);
        client.DefaultRequestHeaders.Add("internal_key_secret", Program.SECRET_INTERNAL);
        
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, 
            $"{PROTOCOL}://purrbalancer.riten.dev:8080/unregisterRoom"));
        
        if (!response.IsSuccessStatusCode)
        {
            var content = response.Content.ReadAsByteArrayAsync();
            var contentStr = Encoding.UTF8.GetString(content.Result);
            throw new Exception(contentStr);
        }
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
                
                var secret = await Lobby.CreateRoom(region, name);
                
                _server ??= new WebSockets(6942);

                return new JObject
                {
                    ["secret"] = secret,
                    ["port"] = _server.port
                };
            }
        }
        
        return new JObject();
    }
}