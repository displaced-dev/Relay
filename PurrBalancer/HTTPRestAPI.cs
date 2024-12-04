using System.Text;
using HttpServerLite;
using Newtonsoft.Json.Linq;
using HttpMethod = System.Net.Http.HttpMethod;

namespace PurrBalancer;

public struct RelayServer
{
    public string host;
    public int restPort;
    public int udpPort;
    public int webSocketsPort;
    public string region;
}

public static class HTTPRestAPI
{
#if DEBUG
    private static readonly RelayServer[] _relayServers =
    [
        new()
        {
            host = "localhost",
            restPort = 8081,
            udpPort = 8082,
            webSocketsPort = 8083,
            region = "local"
        }
    ];
#else
    static readonly RelayServer[] _relayServers =
    [
        new() {
            host = "eu1.purrlay.riten.dev",
            restPort = 8081,
            udpPort = 8082,
            webSocketsPort = 8083,
            region = "eu-central"
        },
        new() {
            host = "us-east.purrlay.riten.dev",
            restPort = 8081,
            udpPort = 8082,
            webSocketsPort = 8083,
            region = "us-east"
        },
        new() {
            host = "us-west.purrlay.riten.dev",
            restPort = 8081,
            udpPort = 8082,
            webSocketsPort = 8083,
            region = "us-west"
        },
        new() {
            host = "asia.purrlay.riten.dev",
            restPort = 8081,
            udpPort = 8082,
            webSocketsPort = 8083,
            region = "ap-southeast"
        }
    ];
#endif
    
    static bool TryGetServer(string region, out RelayServer server)
    {
        foreach (var s in _relayServers)
        {
            if (s.region == region)
            {
                server = s;
                return true;
            }
        }
        
        server = default;
        return false;
    }
    
    static readonly Dictionary<string, string> _roomToRegion = new();
              
#if DEBUG
    const string PROTOCOL = "http";
#else
    const string PROTOCOL = "https";
#endif
    
    public static async Task<JObject> OnRequest(HttpRequest req)
    {
        if (req.Url == null)
            throw new Exception("Invalid URL");

        string path = req.Url.WithoutQuery;

        switch (path)
        {
            case "/ping": return new JObject();
            case "/servers": return new JObject { ["servers"] = JArray.FromObject(_relayServers) };
            case "/registerRoom":
            {
                var region = req.RetrieveHeaderValue("region");
                var name = req.RetrieveHeaderValue("name");
                var internalSecret = req.RetrieveHeaderValue("internal_key_secret");
                
                if (string.IsNullOrEmpty(region) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(internalSecret))
                    throw new Exception("Invalid headers");
                
                if (!TryGetServer(region, out _))
                    throw new Exception("Invalid region");
                
                if (!_roomToRegion.TryAdd(name, region))
                    throw new Exception("Room already registered");

                return new JObject
                {
                    ["status"] = "ok"
                };
            }
            case "/unregisterRoom":
            {
                var name = req.RetrieveHeaderValue("name");
                var internalSecret = req.RetrieveHeaderValue("internal_key_secret");
                
                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(internalSecret))
                    throw new Exception("Invalid headers");
                
                if (!_roomToRegion.Remove(name, out _))
                    throw new Exception("Room not found");

                return new JObject
                {
                    ["status"] = "ok"
                };
            }
            case "/join":
            {
                var name = req.RetrieveHeaderValue("name");
                
                if (string.IsNullOrEmpty(name))
                    throw new Exception("Invalid headers");
                
                if (!_roomToRegion.TryGetValue(name, out var region))
                    throw new Exception("Room not found");
                
                if (!TryGetServer(region, out var server))
                    throw new Exception("Invalid region");

                using HttpClient client = new();
                
                client.DefaultRequestHeaders.Add("name", name);
                client.DefaultRequestHeaders.Add("region", region);
                client.DefaultRequestHeaders.Add("internal_key_secret", Program.SECRET_INTERNAL);
                
                var resp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, 
                    $"{PROTOCOL}://{server.host}:{server.restPort}/getJoinDetails"));
                
                if (!resp.IsSuccessStatusCode)
                {
                    var content = resp.Content.ReadAsByteArrayAsync();
                    var contentStr = Encoding.UTF8.GetString(content.Result);
                    throw new Exception(contentStr);
                }
                
                try
                {
                    var respStr = await resp.Content.ReadAsStringAsync();
                    var obj = JObject.Parse(respStr);
                    obj["host"] = server.host;
                    return obj;
                }
                catch (Exception e)
                {
                    throw new Exception("Invalid response " + e.Message + "\n" + e.StackTrace);
                }
            }
            case "/allocate_ws":
            {
                var region = req.RetrieveHeaderValue("region");
                var name = req.RetrieveHeaderValue("name");
                
                if (string.IsNullOrEmpty(region))
                    throw new Exception("Invalid headers");
                
                if (!TryGetServer(region, out var server))
                    throw new Exception("Invalid region");
                
                if (string.IsNullOrEmpty(name))
                    throw new Exception("Invalid name");
                
                using HttpClient client = new();
                
                client.DefaultRequestHeaders.Add("name", name);
                client.DefaultRequestHeaders.Add("region", region);
                client.DefaultRequestHeaders.Add("internal_key_secret", Program.SECRET_INTERNAL);
                
                var resp = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, 
                    $"{PROTOCOL}://{server.host}:{server.restPort}/allocate_ws"));

                if (!resp.IsSuccessStatusCode)
                {
                    var content = resp.Content.ReadAsByteArrayAsync();
                    var contentStr = Encoding.UTF8.GetString(content.Result);
                    throw new Exception(contentStr);
                }

                try
                {
                    var respStr = await resp.Content.ReadAsStringAsync();
                    return JObject.Parse(respStr);
                }
                catch (Exception e)
                {
                    throw new Exception("Invalid response " + e.Message + "\n" + e.StackTrace);
                }
            }
        }
        
        return new JObject();
    }
}
