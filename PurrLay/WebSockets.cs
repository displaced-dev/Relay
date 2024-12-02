using System.Security.Authentication;
using JamesFrowen.SimpleWeb;

namespace PurrLay;

public class WebSockets : IDisposable
{
    private readonly SimpleWebServer _server;
    
    readonly TcpConfig _tcpConfig = new (noDelay: true, sendTimeout: 5000, receiveTimeout: 20000);
    
    private int? _hostConnId;
    
    private readonly List<int> _clientConnIds = [];
    
    public event Action? onClosed;
    
    public int port { get; private set; }
    
    public WebSockets(int port)
    {
        this.port = port;
        
        var sslConfig = new SslConfig(true, Program.certPath, Program.keyPath, SslProtocols.Tls13);
        _server = new SimpleWebServer(int.MaxValue, _tcpConfig, ushort.MaxValue, 5000, sslConfig);
        _server.Start((ushort)port);
        _server.onConnect += OnClientConnectedToServer;
        _server.onDisconnect += OnClientDisconnectedFromServer;
        _server.onData += OnServerReceivedData;
    }

    private void OnClientConnectedToServer(int connId)
    {
        _clientConnIds.Add(connId);
    }

    private void OnClientDisconnectedFromServer(int connId)
    {
        if (_hostConnId == connId)
        {
            Dispose();
            return;
        }
        
        _clientConnIds.Remove(connId);
    }

    private void OnServerReceivedData(int connId, ArraySegment<byte> data)
    {
        if (data.Array == null)
            return;
        
        if (!_hostConnId.HasValue)
        {
            /*if (data.Count != _hostSecret.Length)
                return;
            
            var secret = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            if (secret != _hostSecret)
                return;*/
            
            _hostConnId = connId;
            _clientConnIds.Remove(connId);
            Console.WriteLine("Host connected");
            return;
        }
        
        if (connId == _hostConnId)
             _server.SendAll(_clientConnIds, data);
        else _server.SendOne(_hostConnId.Value, data);
    }

    public void Dispose()
    {
        if (!_server.Active)
            return;
        
        _server.Stop();
        onClosed?.Invoke();
    }
}