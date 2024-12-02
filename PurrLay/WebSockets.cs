using System.Security.Authentication;
using System.Text;
using JamesFrowen.SimpleWeb;
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
    
    readonly TcpConfig _tcpConfig = new (noDelay: true, sendTimeout: 5000, receiveTimeout: 20000);
    
    private readonly Dictionary<int, ulong> _clientToRoom = new();
    private readonly Dictionary<ulong, List<int>> _roomToClients = new();
    private readonly Dictionary<ulong, int> _roomToHost = new();
    
    public event Action? onClosed;
    
    public int port { get; private set; }

    private Thread? _thread;
    
    public WebSockets(int port)
    {
        this.port = port;
        _thread = new Thread(Start);
        _thread.Start();
    }

    private void Start()
    {
#if DEBUG
        var sslConfig = new SslConfig(false, null, null, SslProtocols.None);
#else
        var sslConfig = new SslConfig(true, Program.certPath, Program.keyPath, SslProtocols.Tls12);
#endif

        _server = new SimpleWebServer(int.MaxValue, _tcpConfig, ushort.MaxValue, 5000, sslConfig);
        _server.Start((ushort)port);
        _server.onDisconnect += OnClientDisconnectedFromServer;
        _server.onData += OnServerReceivedData;

        while (true)
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
    
    public enum HOST_PACKET_TYPE : byte
    {
        SEND_ALL = 0,
        SEND_ONE = 1,
        SEND_MANY = 2,
        SEND_ALL_EXCEPT = 3,
        SEND_ALL_EXCEPT_MANY = 4
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
                case HOST_PACKET_TYPE.SEND_ALL:
                {
                    if (_roomToClients.TryGetValue(roomId, out var list))
                        _server?.SendAll(list, subData);
                    break;
                }
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
                case HOST_PACKET_TYPE.SEND_MANY:
                {
                    int count = subData.Array[subData.Offset + 0] | 
                                 subData.Array[subData.Offset + 1] << 8 | 
                                 subData.Array[subData.Offset + 2] << 16 | 
                                 subData.Array[subData.Offset + 3] << 24;
                    
                    List<int> targets = [count];
                    
                    for (int i = 0; i < count; i++)
                    {
                        int target = subData.Array[subData.Offset + 4 + i * sizeof(int)] | 
                                     subData.Array[subData.Offset + 5 + i * sizeof(int)] << 8 | 
                                     subData.Array[subData.Offset + 6 + i * sizeof(int)] << 16 | 
                                     subData.Array[subData.Offset + 7 + i * sizeof(int)] << 24;
                        
                        if (_clientToRoom.TryGetValue(target, out var room) && room == roomId)
                            targets.Add(target);
                    }
                    
                    _server?.SendAll(targets, new ArraySegment<byte>(
                        subData.Array, subData.Offset + 4 + count * sizeof(int), subData.Count - 4 - count * sizeof(int)));
                    break;
                }
                case HOST_PACKET_TYPE.SEND_ALL_EXCEPT:
                {
                    const int metdataLength = sizeof(int);
                    
                    int target = subData.Array[subData.Offset + 0] | 
                                 subData.Array[subData.Offset + 1] << 8 | 
                                 subData.Array[subData.Offset + 2] << 16 | 
                                 subData.Array[subData.Offset + 3] << 24;
                    
                    if (_roomToClients.TryGetValue(roomId, out var list))
                    {
                        List<int> allExcept = [..list];
                        
                        allExcept.Remove(target);
                        
                        _server?.SendAll(allExcept, new ArraySegment<byte>(
                            subData.Array, subData.Offset + metdataLength, subData.Count - metdataLength));
                    }
                    break;
                }
                case HOST_PACKET_TYPE.SEND_ALL_EXCEPT_MANY:
                {
                    int count = subData.Array[subData.Offset + 0] | 
                                 subData.Array[subData.Offset + 1] << 8 | 
                                 subData.Array[subData.Offset + 2] << 16 | 
                                 subData.Array[subData.Offset + 3] << 24;
                    
                    List<int> targets = [count];
                    
                    for (int i = 0; i < count; i++)
                    {
                        int target = subData.Array[subData.Offset + 4 + i * sizeof(int)] | 
                                     subData.Array[subData.Offset + 5 + i * sizeof(int)] << 8 | 
                                     subData.Array[subData.Offset + 6 + i * sizeof(int)] << 16 | 
                                     subData.Array[subData.Offset + 7 + i * sizeof(int)] << 24;
                        
                        targets.Add(target);
                    }
                    
                    if (_roomToClients.TryGetValue(roomId, out var list))
                    {
                        List<int> allExcept = [..list];
                        
                        foreach (var target in targets)
                            allExcept.Remove(target);
                        
                        _server?.SendAll(allExcept, new ArraySegment<byte>(
                            subData.Array, subData.Offset + 4 + count * sizeof(int), subData.Count - 4 - count * sizeof(int)));
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
                
                Buffer.BlockCopy(data.Array, data.Offset, buffer, 1, data.Count);
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
                isHost = true;
            }
            else if (room.hostSecret == auth.clientSecret)
            {
                _clientToRoom.Add(connId, room.roomId);
                _roomToHost.Add(room.roomId, connId);
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

    private void SendClientsConnected(ulong roomId, List<int> connected)
    {
        if (!_roomToHost.TryGetValue(roomId, out var connId))
            return;
        
        var buffer = new byte[connected.Count * sizeof(int) + 1];
        buffer[0] = (byte)SERVER_PACKET_TYPE.SERVER_CLIENT_CONNECTED;
        
        for (int i = 0; i < connected.Count; i++)
        {
            buffer[i * sizeof(int) + 1] = (byte)connected[i];
            buffer[i * sizeof(int) + 2] = (byte)(connected[i] >> 8);
            buffer[i * sizeof(int) + 3] = (byte)(connected[i] >> 16);
            buffer[i * sizeof(int) + 4] = (byte)(connected[i] >> 24);
        }
        
        _server?.SendOne(connId, new ArraySegment<byte>(buffer));
    }
    
    private void SendClientsConnected(ulong roomId, int connected)
    {
        if (!_roomToHost.TryGetValue(roomId, out var connId))
            return;
        
        var buffer = new byte[sizeof(int) + 1];
        buffer[0] = (byte)SERVER_PACKET_TYPE.SERVER_CLIENT_CONNECTED;
        
        buffer[sizeof(int) + 1] = (byte)connected;
        buffer[sizeof(int) + 2] = (byte)(connected >> 8);
        buffer[sizeof(int) + 3] = (byte)(connected >> 16);
        buffer[sizeof(int) + 4] = (byte)(connected >> 24);
        
        _server?.SendOne(connId, new ArraySegment<byte>(buffer));
    }
    
    private void SendClientsDisconnected(ulong roomId, List<int> disconnected)
    {
        if (!_roomToHost.TryGetValue(roomId, out var connId))
            return;
        
        var buffer = new byte[disconnected.Count * sizeof(int) + 1];
        buffer[0] = (byte)SERVER_PACKET_TYPE.SERVER_CLIENT_DISCONNECTED;
        
        for (int i = 0; i < disconnected.Count; i++)
        {
            buffer[i * sizeof(int) + 1] = (byte)disconnected[i];
            buffer[i * sizeof(int) + 2] = (byte)(disconnected[i] >> 8);
            buffer[i * sizeof(int) + 3] = (byte)(disconnected[i] >> 16);
            buffer[i * sizeof(int) + 4] = (byte)(disconnected[i] >> 24);
        }
        
        _server?.SendOne(connId, new ArraySegment<byte>(buffer));
    }
    
    
    private void SendClientsDisconnected(ulong roomId, int disconnected)
    {
        if (!_roomToHost.TryGetValue(roomId, out var connId))
            return;
        
        var buffer = new byte[sizeof(int) + 1];
        buffer[0] = (byte)SERVER_PACKET_TYPE.SERVER_CLIENT_DISCONNECTED;
        
        buffer[sizeof(int) + 1] = (byte)disconnected;
        buffer[sizeof(int) + 2] = (byte)(disconnected >> 8);
        buffer[sizeof(int) + 3] = (byte)(disconnected >> 16);
        buffer[sizeof(int) + 4] = (byte)(disconnected >> 24);
        
        _server?.SendOne(connId, new ArraySegment<byte>(buffer));
    }
    
    private void SendSingleCode(int connId, SERVER_PACKET_TYPE type)
    {
        _server?.SendOne(connId, new ArraySegment<byte>([(byte)type]));
    }

    public void Dispose()
    {
        if (_server?.Active == false)
            return;
        
        _server?.Stop();
        onClosed?.Invoke();
    }
}