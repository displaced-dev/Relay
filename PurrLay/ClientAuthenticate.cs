namespace PurrLay;

public struct ClientAuthenticate(string roomName, string clientSecret)
{
    public readonly string roomName = roomName;
    public readonly string clientSecret = clientSecret;
}