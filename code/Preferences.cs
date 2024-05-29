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
        var file = "/settings.json";
        FileSystem.Data.WriteJson( file, Settings );
    }

}

public class InstagibSettings
{
    public float VolumeMusic { get; set; } = 40f;
    public float VolumeSFX { get; set; } = 60f;

    public float FieldOfView { get; set; } = 100f;
    public float ZoomedFieldOfView { get; set; } = 40f;
    public float ZoomedSensitivity { get; set; } = 0.4f;
}

public class ChatSettings
{
    public bool ShowAvatars { get; set; } = true;
    public int FontSize { get; set; } = 16;
    public bool ChatSounds { get; set; } = true;
}