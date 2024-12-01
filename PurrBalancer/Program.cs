using System.Security.Cryptography.X509Certificates;
using System.Text;
using Newtonsoft.Json;
using WatsonWebserver.Core;
using Webserver = WatsonWebserver.Webserver;

namespace PurrBalancer;

internal static class Program
{
    static async Task HandleIncomingConnections(HttpContextBase ctx)
    {
        // Peel out the requests and response objects
        var req = ctx.Request;
        var resp = ctx.Response;
        
        resp.Headers.Add("Access-Control-Allow-Methods", "GET");
        resp.Headers.Add("Access-Control-Allow-Origin", "*");
        
        try
        {
            var response = HTTPRestAPI.OnRequest(req);
            var data = Encoding.UTF8.GetBytes(response.ToString(Formatting.None));

            resp.ContentType = "application/json";
            resp.StatusCode = 200;
            resp.ContentLength = data.LongLength;
            await resp.Send(data);
        }
        catch (Exception e)
        {
            var data = Encoding.UTF8.GetBytes(e.Message);
            resp.StatusCode = 500;
            resp.ContentType = "text/plain";
            resp.ContentLength = data.LongLength;
            await resp.Send(data);
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
        
        X509Certificate2? cert = null;
        
        if (https)
            cert = new X509Certificate2(certPath, keyPath);

        var settings = new WebserverSettings(host, _Port)
        {
            Ssl =
            {
                Enable = https,
                SslCertificate = cert
            }
        };
        
        var server = new Webserver(settings, HandleIncomingConnections);
        
        server.Start();
        
        new ManualResetEvent(false).WaitOne();
    }
}