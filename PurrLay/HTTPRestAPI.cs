using HttpServerLite;
using Newtonsoft.Json.Linq;

namespace PurrLay;

public static class HTTPRestAPI
{
    static readonly Dictionary<ushort, Webserver> _servers = new();
    
    static ushort _nextPort = 8081;

    static ushort GetNextPort()
    {
        const int maxAttempts = ushort.MaxValue - 8081; // Total available port range
        int attempts = 0;
        
        lock (_servers)
        {
            while (_servers.ContainsKey(_nextPort))
            {
                _nextPort++;
                
                if (_nextPort == ushort.MaxValue)
                    _nextPort = 8081;
                
                if (++attempts > maxAttempts)
                    throw new InvalidOperationException("No available ports.");
            }
            return _nextPort;
        }
    }
    
    public static JObject OnRequest(HttpRequest req)
    {
        if (req.Url == null)
            throw new Exception("Invalid URL");

        string path = req.Url.WithoutQuery;

        switch (path)
        {
            case "/ping": return new JObject();
            case "/allocate_ws":
            {
                var name = req.RetrieveHeaderValue("name");
                var internalSec = req.RetrieveHeaderValue("internal");
                
                if (string.IsNullOrWhiteSpace(name))
                    throw new Exception("Missing name");
                
                if (string.IsNullOrWhiteSpace(internalSec))
                    throw new Exception("Bad internal secret, -1");

                if (!string.Equals(internalSec, Program.SECRET_INTERNAL))
                    throw new Exception($"Bad internal secret, {internalSec.Length}");
                
                Lobby.CreateRoom(name, out var secret);

                var response = new JObject
                {
                    ["secret"] = secret
                };

                return response;
            }
        }
        
        throw new Exception("Invalid path");
    }
}