using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace PurrBalancer;

internal static class Program
{
    static HttpListener? _listener;
    
    static async Task HandleIncomingConnections()
    {
        if (_listener == null)
            return;
        
        // While a user hasn't visited the `shutdown` url, keep on handling requests
        while (true)
        {
            // Will wait here until we hear from a connection
            var ctx = await _listener.GetContextAsync();

            // Peel out the requests and response objects
            var req = ctx.Request;
            var resp = ctx.Response;
            
            resp.AddHeader("Access-Control-Allow-Methods", "GET");
            resp.AppendHeader("Access-Control-Allow-Origin", "*");
            
            try
            {
                var response = HTTPRestAPI.OnRequest(req);
                var data = Encoding.UTF8.GetBytes(response.ToString(Formatting.None));

                resp.ContentType = "application/json";
                resp.ContentEncoding = Encoding.UTF8;
                resp.StatusCode = 200;
                resp.ContentLength64 = data.LongLength;
                await resp.OutputStream.WriteAsync(data);
                resp.Close();
            }
            catch (Exception e)
            {
                var data = Encoding.UTF8.GetBytes(e.Message);
                resp.StatusCode = 500;
                resp.StatusDescription = "Internal Server Error";
                resp.ContentType = "text/plain";
                resp.ContentEncoding = Encoding.UTF8;
                resp.ContentLength64 = data.LongLength;
                resp.OutputStream.Write(data);
                resp.Close();
            }
        }
    }
    
    static void Main()
    {
        _listener = new HttpListener();
        
        _listener.Prefixes.Add("http://*:8080/");
        _listener.Prefixes.Add("https://*:8443/");
        
        _listener.Start();
        
        // Handle requests
        var listenTask = HandleIncomingConnections();
        listenTask.GetAwaiter().GetResult();

        // Close the listener
        _listener.Close();
    }
}