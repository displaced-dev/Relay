using System.Text;
using HttpServerLite;
using Newtonsoft.Json;
 
namespace PurrBalancer;

internal static class Program   
{ 
    public static string SECRET_INTERNAL { get; private set; } = "PURRNET"; 

    static async Task HandleIncomingConnections(HttpContext ctx)
    {
        Console.WriteLine($"Received request: {ctx.Request.Method} {ctx.Request.Url.Full}");
        
        // Peel out the re quests and response objects 
        var req = ctx.Request;
        var resp = ctx.Response;
        
        try
        {
            var response = await HTTPRestAPI.OnRequest(req);
            var data = Encoding.UTF8.GetBytes(response.ToString(Formatting.None));

            resp.ContentType = "application/json";
            resp.StatusCode = 200;
            resp.ContentLength = data.LongLength;
            await resp.SendAsync(data);
        }
        catch (Exception e)
        {
            var data = Encoding.UTF8.GetBytes(e.Message);
            
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
                case "--secret" when i + 1 < args.Length:
                    SECRET_INTERNAL = args[++i];
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

        var host = https ? "purrbalancer.riten.dev" : "localhost";
        const int _Port = 8080;
        
        var server = new Webserver(host, _Port, https, certPath, keyPath, HandleIncomingConnections); 
        server.Settings.Headers.AccessControlAllowHeaders = "*";
        server.Settings.Headers.AccessControlAllowMethods = "*";
        server.Settings.Headers.AccessControlAllowOrigin = "*";
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
