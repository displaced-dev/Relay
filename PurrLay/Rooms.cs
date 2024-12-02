namespace PurrLay;

public class Room
{
    public string name;
    public string hostSecret;
    public string clientSecret;
    public DateTime createdAt;
}

public static class Lobby
{
    static readonly Dictionary<string, Room> _room = new();
    
    public static void CreateRoom(string name, out string hostSecret)
    {
        if (_room.ContainsKey(name))
            throw new Exception("Room already exists");
        
        hostSecret = Guid.NewGuid().ToString().Replace("-", "");
        
        _room[name] = new Room
        {
            name = name,
            hostSecret = hostSecret,
            clientSecret = Guid.NewGuid().ToString().Replace("-", ""),
            createdAt = DateTime.UtcNow
        };
    }

    public static bool TryGetRoom(string name, out Room? room)
    {
        return _room.TryGetValue(name, out room);
    }
}