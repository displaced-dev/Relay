using System.Text;
using HttpServerLite;
using Newtonsoft.Json;

namespace PurrBalancer;

internal static class Program
{
    static async Task HandleIncomingConnections(HttpContext ctx)
    {
        // Peel out the requests and response objects
        var req = ctx.Request;
        var resp = ctx.Response;
        
        resp.Headers.Add("Access-Control-Allow-Methods", "GET");
        resp.Headers.Add("Access-Control-Allow-Origin", "*");
        req.Headers.Add("Accept-Charset", "utf-8");
        
        try
        {
            var response = HTTPRestAPI.OnRequest(req);
            var data = Encoding.UTF8.GetBytes(response.ToString(Formatting.None));

            resp.ContentType = "application/json";
            resp.StatusCode = 200;
            resp.ContentLength = data.LongLength;
            await resp.SendAsync(data);
            resp.Close();
        }
        catch (Exception e)
        {
            var data = Encoding.UTF8.GetBytes(e.Message);
            resp.StatusCode = 500;
            resp.StatusDescription = "Internal Server Error";
            resp.ContentType = "text/plain";
            resp.ContentLength = data.LongLength;
            await resp.SendAsync(data);
            resp.Close();
        }
    }
    
    static void Main(string[] args)
    {
        string certPath = string.Empty;
        string keyPath = string.Empty;
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--cert" when i + 1 < args.Length:
                    certPath = args[++i];
                    break;
                case "--key" when i + 1 < args.Length:
                    keyPath = args[++i];
                    break;
            }
        }
        
        bool https = !string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(keyPath);
        
        switch (https)
        {
            case true when !File.Exists(certPath):
                Console.WriteLine("Certificate file not found");
                return;
            case true when !File.Exists(keyPath):
                Console.WriteLine("Key file not found");
                return;
        }

        var host = https ? "purrbalancer.riten.dev" : "localhost";
        const int _Port = 8080;

        var webserver = new Webserver(host, _Port, https, certPath, keyPath, HandleIncomingConnections);

        webserver.Settings.Headers.Host = $"{(https ? "https" : "http")}://{host}:{_Port}";
        Console.WriteLine($"Listening on {webserver.Settings.Headers.Host}");
        
        webserver.Start();
        Console.ReadKey();
    }
}