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
                var region = req.RetrieveHeaderValue("region");
                var secret = req.RetrieveHeaderValue("secret");

                if (region == null || secret == null)
                    throw new Exception("Missing region or secret");

                var response = new JObject
                {
                    ["region"] = region,
                    ["secret"] = secret,
                    ["internal"] = Program.SECRET_INTERNAL
                };

                return response;
            }
        }
        
        throw new Exception("Invalid path");
    }
}