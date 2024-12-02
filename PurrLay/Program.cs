using System.Text;
using HttpServerLite;
using Newtonsoft.Json;

namespace PurrLay;

internal static class Program
{
    public const string SECRET_INTERNAL = "XBf5mFfcCLmTuuCRTT8WbeymWbt5yyi3fcVq2Tu0WO924ZWkWxZXV337LzYLeg2F";

    static async Task HandleIncomingConnections(HttpContext ctx)
    {
        Console.WriteLine($"Received request: {ctx.Request.Method} {ctx.Request.Url}");
        
        try
        {
            // Peel out the requests and response objects
            var req = ctx.Request;
            var resp = ctx.Response;
        
            resp.Headers.Add("Access-Control-Allow-Methods", "GET");
            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            
            var response = HTTPRestAPI.OnRequest(req);
            var data = Encoding.UTF8.GetBytes(response.ToString(Formatting.None));

            resp.ContentType = "application/json";
            resp.StatusCode = 200;
            resp.ContentLength = data.LongLength;
            await resp.SendAsync(data);
        }
        catch (Exception e)
        {
            var resp = ctx.Response;
            var data = Encoding.UTF8.GetBytes(e.Message + "\n" + e.StackTrace);
            
            resp.StatusCode = 500;
            resp.ContentType = "text/plain";
            resp.ContentLength = data.LongLength;
            await resp.SendAsync(data);
        }
    }
    
    public static string certPath = string.Empty;
    public static string keyPath = string.Empty;
    
    static void Main(string[] args)
    {
        string domain = string.Empty;
        
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
                case "--url" when i + 1 < args.Length:
                    domain = args[++i];
                    break;
            }
        }
        
        bool https = !string.IsNullOrEmpty(certPath) && !string.IsNullOrEmpty(keyPath);
        
        switch (https)
        {
            case true when !File.Exists(certPath):
                Console.WriteLine("Certificate file not found");
                return;
        }

        var host = string.IsNullOrWhiteSpace(domain) ? (https ? "purrbalancer.riten.dev" : "localhost") : domain;
        const int _Port = 8081;
        
        var server = new Webserver(host, _Port, https, certPath, keyPath, HandleIncomingConnections); 
        server.Settings.Headers.Host = $"{(https?"https":"http")}://{host}:{_Port}";
        
        Console.WriteLine($"Starting server on {host}:{_Port}, HTTPS: {https}");

        if (https)
        {
            Console.WriteLine($"Using PFX Certificate: {certPath}");
            Console.WriteLine($"Using password: {keyPath}");
        }
        
        server.Start();
        
        new ManualResetEvent(false).WaitOne();
    }
}