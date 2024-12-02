using HttpServerLite;
using Newtonsoft.Json.Linq;

namespace PurrLay;

public static class HTTPRestAPI
{
    public static JObject OnRequest(HttpRequest req)
    {
        if (req.Url == null)
            throw new Exception("Invalid URL");

        string path = req.Url.WithoutQuery;

        switch (path)
        {
            case "/ping": return new JObject();
        }
        
        throw new Exception("Invalid path");
    }
}