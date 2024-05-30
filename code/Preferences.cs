using System.Text.Json.Serialization;
using Sandbox;

namespace Instagib;

public static class InstagibPreferences
{

    public static InstagibSettings Settings
    {
        get
        {
            if ( _settings is null )
            {
                var file = "/settings.json";
                _settings = FileSystem.Data.ReadJson( file, new InstagibSettings() );
            }
            return _settings;
        }
    }
    static InstagibSettings _settings;

    public static InstagibStats Stats
    {
        get
        {
            if ( _stats is null )
            {
                var file = "/stats.json";
                _stats = FileSystem.Data.ReadJson( file, new InstagibStats() );
            }
            return _stats;
        }
    }
    static InstagibStats _stats;

    public static ChatSettings Chat
    {
        get
        {
            if ( _chatSettings is null )
            {
                var file = "/settings/chat.json";
                _chatSettings = FileSystem.Data.ReadJson( file, new ChatSettings() );
            }
            return _chatSettings;
        }
    }
    static ChatSettings _chatSettings;

    public static void Save()
    {
        FileSystem.Data.WriteJson( "/settings.json", Settings );
        FileSystem.Data.WriteJson( "/stats.json", Stats );
    }

}

public class InstagibStats
{
    public int TotalKills { get; set; } = 0;
    public int TotalDeaths { get; set; } = 0;
    [JsonIgnore] public float OverallAccuracy => (float)TotalKills / (float)TotalShotsFired;
    public int TotalShotsFired { get; set; } = 0;
    public int TotalBounces { get; set; } = 0;

    public Dictionary<Gamemode, int> TotalWins { get; set; } = new();
    public Dictionary<Gamemode, int> TotalLosses { get; set; } = new();

    public Dictionary<int, int> Killstreaks { get; set; } = new();
}

public class InstagibSettings
{
    public float VolumeMaster { get; set; } = 60f;
    public float VolumeMusic { get; set; } = 40f;
    public float VolumeSFX { get; set; } = 60f;

    public Gamemode Gamemode { get; set; } = Gamemode.Deathmatch;
    public int BotCount { get; set; } = 0;
    public int MaxPlayers { get; set; } = 8;
    public int FragLimit { get; set; } = 30;
    public int TimeLimit { get; set; } = 10;

    public float FieldOfView { get; set; } = 100f;
    public float ZoomedFieldOfView { get; set; } = 40f;
    public float Sensitivity { get; set; } = 8f;
    public float ZoomedSensitivity { get; set; } = 0.4f;
}

public class ChatSettings
{
    public bool ShowAvatars { get; set; } = true;
    public int FontSize { get; set; } = 16;
    public bool ChatSounds { get; set; } = true;
}