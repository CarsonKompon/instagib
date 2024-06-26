using Sandbox;

namespace Instagib;

public partial class GameManager
{
    [ConCmd( "bot_add" )]
    public static void AddBot()
    {
        if ( !Networking.IsHost ) return;
        var clientObj = Instance.ClientPrefab.Clone( global::Transform.Zero, name: GetRandomBotName() );
        clientObj.NetworkSpawn( null );
        var client = clientObj.Components.Get<Client>();
        client.IsBot = true;
    }

    [ConCmd( "kill" )]
    public static void KillLocalPlayer()
    {
        if ( Player.Local.IsValid() )
        {
            Player.Local.Kill();
        }
    }

    [ConCmd( "mapvote" )]
    public static void EndCurrentGame()
    {
        if ( !Networking.IsHost ) return;
        Instance.EndGame();
    }

    [ConCmd( "rtv" )]
    public static void VoteToChangeMap()
    {
        if ( !Networking.IsHost ) return;
        Instance.RockTheVote( Client.Local.GameObject.Id );
    }
}