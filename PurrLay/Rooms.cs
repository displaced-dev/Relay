namespace PurrLay;

public class Room
{
    public string name;
    public string hostSecret;
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
            createdAt = DateTime.UtcNow
        };
    }
}