using LiteNetLib;

namespace PurrLay;

public class UdpServer : INetLogger
{
    private readonly NetManager _server;
    private readonly EventBasedNetListener _serverListener;

    static readonly Dictionary<NetPeer, int> _localConnToGlobal = new();
    static readonly Dictionary<int, NetPeer> _globalConnToLocal = new();

    public UdpServer(int port)
    {
        NetDebug.Logger = this;
        _serverListener = new EventBasedNetListener();

        _server = new NetManager(_serverListener)
        {
            UnconnectedMessagesEnabled = true,
            PingInterval = 900,
            UnsyncedEvents = true
        };

        _serverListener.ConnectionRequestEvent += OnServerConnectionRequest;
        _serverListener.PeerConnectedEvent += OnServerConnected;
        _serverListener.PeerDisconnectedEvent += OnServerDisconnected;
        _serverListener.NetworkReceiveEvent += OnServerData;

        _server.Start(port);
    }

    private static void OnServerConnectionRequest(ConnectionRequest request)
    {
        request.AcceptIfKey("PurrNet");
    }

    private static void OnServerConnected(NetPeer conn)
    {
        var global = Transport.ReserveConnId(true);
        _localConnToGlobal[conn] = global;
        _globalConnToLocal[global] = conn;
    }

    private static void OnServerDisconnected(NetPeer connId, DisconnectInfo disconnectinfo)
    {
        try
        {
            if (_localConnToGlobal.Remove(connId, out var global))
            {
                _globalConnToLocal.Remove(global);
                Transport.OnClientLeft(new PlayerInfo(global, true));
            }
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error handling disconnect: {e.Message}\n{e.StackTrace}");
        }
    }

    private static void OnServerData(NetPeer connId, NetPacketReader reader, byte channel, DeliveryMethod deliverymethod)
    {
        try
        {
            if (_localConnToGlobal.TryGetValue(connId, out var globalId))
                Transport.OnServerReceivedData(new PlayerInfo(globalId, true), reader.GetRemainingBytesSegment());
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error handling data: {e.Message}\n{e.StackTrace}");
        }
    }

    public void WriteNet(NetLogLevel level, string str, params object[] args)
    {
        Console.WriteLine($"{level}: {str}", args);
    }

    public void KickClient(int playerConnId)
    {
        if (_globalConnToLocal.Remove(playerConnId, out var peer))
        {
            _localConnToGlobal.Remove(peer);
            _server.DisconnectPeer(peer);
        }
    }

    public void SendOne(int valueConnId, ReadOnlySpan<byte> segment, DeliveryMethod method)
    {
        if (_globalConnToLocal.TryGetValue(valueConnId, out var peer))
            peer.Send(segment, method);
    }
}
