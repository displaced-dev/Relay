using System.Security.Authentication;
using System.Text;
using JamesFrowen.SimpleWeb;
using LiteNetLib.Utils;
using Newtonsoft.Json;

namespace PurrLay;

public struct ClientAuthenticate
{
    public string roomName;
    public string clientSecret;
}

public class WebSockets : IDisposable
{
    private SimpleWebServer? _server;
    
    readonly TcpConfig _tcpConfig = new (noDelay: true, sendTimeout: 0, receiveTimeout: 0);
    
    private readonly Dictionary<int, ulong> _clientToRoom = new();
    private readonly Dictionary<ulong, List<int>> _roomToClients = new();
    private readonly Dictionary<ulong, int> _roomToHost = new();
    
    public event Action? onClosed;
    
    public int port { get; private set; }
    
    private bool _disposed;

    public WebSockets(int port)
    {
        this.port = port;
        var thread = new Thread(Start);
        thread.Start();
    }

    private void Start()
    {
#if DEBUG
        var sslConfig = new SslConfig(false, null!, null!, SslProtocols.None);
#else
        var sslConfig = new SslConfig(true, Program.certPath, Program.keyPath, SslProtocols.Tls12);
#endif

        _server = new SimpleWebServer(int.MaxValue, _tcpConfig, ushort.MaxValue, 5000, sslConfig);
        _server.Start((ushort)port);
        _server.onDisconnect += OnClientDisconnectedFromServer;
        _server.onData += OnServerReceivedData;

        while (!_disposed)
        {
            try
            {
                Thread.Sleep(16);
                _server.ProcessMessageQueue();
            }
            catch
            {
                break;
            }
        }
        
        Dispose();
    }

    private void OnClientDisconnectedFromServer(int connId)
    {
        if (_clientToRoom.Remove(connId, out var roomId))
        {
            if (_roomToHost.TryGetValue(roomId, out var hostId) && hostId == connId)
            {
                // Remove room host
                _roomToHost.Remove(roomId);
                
                // Kick all clients and remove room
                if (_roomToClients.TryGetValue(roomId, out var list))
                {
                    foreach (var id in list)
                        _server?.KickClient(id);
                    _roomToClients.Remove(roomId);
                }
                
                Lobby.RemoveRoom(roomId);
            }
            else if (_roomToClients.TryGetValue(roomId, out var list))
            {
                SendClientsDisconnected(roomId, connId);
                list.Remove(connId);
            }
        }
    }

    public enum SERVER_PACKET_TYPE : byte
    {
        SERVER_CLIENT_CONNECTED = 0,
        SERVER_CLIENT_DISCONNECTED = 1,
        SERVER_CLIENT_DATA = 2,
        SERVER_AUTHENTICATED = 3,
        SERVER_AUTHENTICATION_FAILED = 4
    }
    
    enum HOST_PACKET_TYPE : byte
    {
        SEND_KEEPALIVE = 0,
        SEND_ONE = 1
    }

    private void OnServerReceivedData(int connId, ArraySegment<byte> data)
    {
        if (data.Array == null)
            return;
        
        bool authenticated = _clientToRoom.ContainsKey(connId);
        
        if (!authenticated)
        {
            TryToAuthenticate(connId, data);
            return;
        }
        
        if (!_clientToRoom.TryGetValue(connId, out var roomId))
            return;
        
        if (!_roomToHost.TryGetValue(roomId, out var hostId))
            return;
        
        if (hostId == connId)
        {
            var type = (HOST_PACKET_TYPE)data.Array[data.Offset];
            var subData = new ArraySegment<byte>(data.Array, data.Offset + 1, data.Count - 1);
            
            if (subData.Array == null)
                return;

            switch (type)
            {
                case HOST_PACKET_TYPE.SEND_KEEPALIVE:
                    break;
                case HOST_PACKET_TYPE.SEND_ONE:
                {
                    const int metdataLength = sizeof(int);
                    
                    int target = subData.Array[subData.Offset + 0] | 
                                 subData.Array[subData.Offset + 1] << 8 | 
                                 subData.Array[subData.Offset + 2] << 16 | 
                                 subData.Array[subData.Offset + 3] << 24;

                    if (_clientToRoom.TryGetValue(target, out var room) && room == roomId)
                    {
                        _server?.SendOne(target, new ArraySegment<byte>(
                            subData.Array, subData.Offset + metdataLength, subData.Count - metdataLength));
                    }
                    break;
                }
            }
        }
        else
        {
            // Client
            if (_roomToHost.TryGetValue(roomId, out var host))
            {
                var buffer = new byte[data.Count + sizeof(byte) + sizeof(int)];
                
                buffer[0] = (byte)SERVER_PACKET_TYPE.SERVER_CLIENT_DATA;
                
                buffer[1] = (byte)connId;
                buffer[2] = (byte)(connId >> 8);
                buffer[3] = (byte)(connId >> 16);
                buffer[4] = (byte)(connId >> 24);
                
                Buffer.BlockCopy(data.Array, data.Offset, buffer, 1 + 4, data.Count);
                _server?.SendOne(host, buffer);
            }
        }
    }

    private void TryToAuthenticate(int connId, ArraySegment<byte> data)
    {
        try
        {
            if (data.Array == null)
            {
                SendSingleCode(connId, SERVER_PACKET_TYPE.SERVER_AUTHENTICATION_FAILED);
                return;
            }
            
            var str = Encoding.UTF8.GetString(data.Array, data.Offset, data.Count);
            var auth = JsonConvert.DeserializeObject<ClientAuthenticate>(str);

            if (string.IsNullOrWhiteSpace(auth.roomName) || string.IsNullOrWhiteSpace(auth.clientSecret))
            {
                SendSingleCode(connId, SERVER_PACKET_TYPE.SERVER_AUTHENTICATION_FAILED);
                return;
            }

            if (!Lobby.TryGetRoom(auth.roomName, out var room) || room == null)
            {
                SendSingleCode(connId, SERVER_PACKET_TYPE.SERVER_AUTHENTICATION_FAILED);
                return;
            }

            bool isHost = false;

            if (room.clientSecret == auth.clientSecret)
            {
                _clientToRoom.Add(connId, room.roomId);
            }
            else if (room.hostSecret == auth.clientSecret)
            {
                _clientToRoom.Add(connId, room.roomId);
                _roomToHost.Add(room.roomId, connId);
                isHost = true;
            }
            else
            {
                SendSingleCode(connId, SERVER_PACKET_TYPE.SERVER_AUTHENTICATION_FAILED);
                return;
            }
                
            if (!_roomToClients.TryGetValue(room.roomId, out var list))
                _roomToClients.Add(room.roomId, [connId]);
            else
            {
                if (isHost)
                     SendClientsConnected(room.roomId, list);
                else SendClientsConnected(room.roomId, connId);
                list.Add(connId);
            }

            SendSingleCode(connId, SERVER_PACKET_TYPE.SERVER_AUTHENTICATED);
        }
        catch
        {
            SendSingleCode(connId, SERVER_PACKET_TYPE.SERVER_AUTHENTICATION_FAILED);
        }
    }
    
    readonly NetDataWriter _writer = new();

    private void SendClientsConnected(ulong roomId, List<int> connected)
    {
        if (!_roomToHost.TryGetValue(roomId, out var connId))
            return;
        
        _writer.Reset();
        _writer.Put((byte)SERVER_PACKET_TYPE.SERVER_CLIENT_CONNECTED);
        
        for (int i = 0; i < connected.Count; i++)
            _writer.Put(connected[i]);

        _server?.SendOne(connId, _writer.CopyData());
    }
    
    private void SendClientsConnected(ulong roomId, int connected)
    {
        if (!_roomToHost.TryGetValue(roomId, out var connId))
            return;
        
        _writer.Reset();
        _writer.Put((byte)SERVER_PACKET_TYPE.SERVER_CLIENT_CONNECTED);
        _writer.Put(connected);
        
        _server?.SendOne(connId, _writer.CopyData());
    }
    
    private void SendClientsDisconnected(ulong roomId, int disconnected)
    {
        if (!_roomToHost.TryGetValue(roomId, out var connId))
            return;
        
        _writer.Reset();
        _writer.Put((byte)SERVER_PACKET_TYPE.SERVER_CLIENT_DISCONNECTED);
        _writer.Put(disconnected);
        
        _server?.SendOne(connId, _writer.CopyData());
    }
    
    private void SendSingleCode(int connId, SERVER_PACKET_TYPE type)
    {
        _writer.Reset();
        _writer.Put((byte)type);
        
        _server?.SendOne(connId, _writer.CopyData());
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        
        if (_server?.Active == false)
            return;
        
        _server?.Stop();
        onClosed?.Invoke();
    }
}