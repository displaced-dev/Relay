using HttpServerLite;
using Newtonsoft.Json.Linq;

namespace PurrLay;

public static class HTTPRestAPI
{
    static readonly WebSockets _server = new(8081);
    
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
                var internalSec = req.RetrieveHeaderValue("internal_key_secret");
                
                if (string.IsNullOrWhiteSpace(name))
                    throw new Exception("Missing name");
                
                if (string.IsNullOrWhiteSpace(internalSec))
                    throw new Exception("Bad internal secret, -1");

                if (!string.Equals(internalSec, Program.SECRET_INTERNAL))
                    throw new Exception($"Bad internal secret, {internalSec.Length}");
                
                Lobby.CreateRoom(name, out var secret);

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