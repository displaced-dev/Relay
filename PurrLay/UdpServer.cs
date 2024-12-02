using LiteNetLib;

namespace PurrLay;

public class UdpServer : INetLogger
{
    private readonly NetManager _server;
    private readonly EventBasedNetListener _serverListener;

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

    private void OnServerConnected(NetPeer peer)
    {
        // notify host
        
    }

    private void OnServerDisconnected(NetPeer peer, DisconnectInfo disconnectinfo)
    {
        throw new NotImplementedException();
    }

    private void OnServerData(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliverymethod)
    {
        throw new NotImplementedException();
    }

    public void WriteNet(NetLogLevel level, string str, params object[] args)
    {
        Console.WriteLine($"{level}: {str}", args);
    }
}