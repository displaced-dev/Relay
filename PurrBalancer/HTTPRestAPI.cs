using System.Net;
using HttpServerLite;
using Newtonsoft.Json.Linq;

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
            host = "asia.purrlay.riten.dev",
            restPort = 8081,
            udpPort = 8082,
            webSocketsPort = 8083,
            region = "ap-southeast"
        }
    ];
    
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
    
    static readonly WebClient _webClient = new ();
    
    public static JObject OnRequest(HttpRequest req)
    {
        if (req.Url == null)
            throw new Exception("Invalid URL");

        string path = req.Url.WithoutQuery;

        switch (path)
        {
            case "/ping": return new JObject();
            case "/servers": return new JObject { ["servers"] = JArray.FromObject(_relayServers) };
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
                
                _webClient.Headers.Clear();
                _webClient.Headers.Add("name", name);
                _webClient.Headers.Add("internal_key_secret", Program.SECRET_INTERNAL);
                
                var resp = _webClient.DownloadString($"https://{server.host}:{server.restPort}/allocate_ws");
                
                return JObject.Parse(resp);
            }
        }
        
        throw new Exception("Invalid path");
    }
}
