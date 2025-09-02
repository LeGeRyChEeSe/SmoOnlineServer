namespace Server.Discord;

/// <summary>
/// Service pour accéder au serveur principal depuis les modules Discord
/// </summary>
public class ServerService
{
    public global::Server.Server? MainServer { get; set; }
    public HashSet<int>? ShineBag { get; set; }
    
    public ServerService()
    {
        // Le serveur et les données seront injectés plus tard depuis Program.cs
    }
    
    public void SetServer(global::Server.Server server)
    {
        MainServer = server;
    }
    
    public void SetShineBag(HashSet<int> shineBag)
    {
        ShineBag = shineBag;
    }
}