using Sandbox;
using Sandbox.Network;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Instagib;

public enum Gamemode
{
    Deathmatch,
    TeamDeathmatch,
    Freezetag
}

[Category( "Instagib" )]
public partial class GameManager : Component, Component.INetworkListener
{
    public static GameManager Instance { get; private set; }

    [Property] GameObject ClientPrefab { get; set; }
    [Property] GameObject PlayerPrefab { get; set; }
    [Property] List<GameObject> SpawnPoints { get; set; }

    [Sync] public TimeUntil Timer { get; set; } = 60;
    [Sync] public int FragLimit { get; set; } = 30;

    [Group( "Prefabs" ), Property] public GameObject BeamPrefab { get; set; }
    [Group( "Prefabs" ), Property] public GameObject ReticlePrefab { get; set; }

    [Group( "Particles" ), Property] public GameObject LaserDustParticle { get; set; }
    [Group( "Particles" ), Property] public GameObject BounceParticle { get; set; }

    [Group( "Gibs" ), Property] public List<GameObject> CitizenGibs { get; set; }

    protected override void OnAwake()
    {
        Instance = this;
    }

    protected override async Task OnLoad()
    {
        if ( Scene.IsEditor )
            return;

        if ( !GameNetworkSystem.IsActive )
        {
            LoadingScreen.Title = "Creating Lobby";
            await Task.DelayRealtimeSeconds( 0.1f );
            GameNetworkSystem.CreateLobby();
            Timer = InstagibPreferences.Settings.TimeLimit * 60f;
            FragLimit = InstagibPreferences.Settings.FragLimit;
        }
    }

    public void OnActive( Connection channel )
    {
        if ( PlayerPrefab is null )
            return;

        var client = ClientPrefab.Clone( global::Transform.Zero, name: channel.DisplayName );
        client.NetworkSpawn( channel );
    }

    [Broadcast]
    public void SpawnPlayer( Client client )
    {
        if ( !Networking.IsHost ) return;
        if ( Scene.GetAllComponents<Player>().Any( x => x.GameObject.Name == client.GameObject.Id.ToString() ) ) return;
        var startLocation = FindSpawnLocation().WithScale( 1 );
        var player = PlayerPrefab.Clone( startLocation, name: client.GameObject.Id.ToString() );
        var playerScript = player.Components.Get<Player>();
        playerScript.Init( client );
        if ( client.IsBot )
        {
            player.Components.Get<HumanController>().Destroy();
            player.Components.Create<BotController>();
        }
        player.NetworkSpawn( client.Network.OwnerConnection );
    }

    Transform FindSpawnLocation()
    {
        //
        // If they have spawn point set then use those
        //
        if ( SpawnPoints is not null && SpawnPoints.Count > 0 )
        {
            return Random.Shared.FromList( SpawnPoints, default ).Transform.World;
        }

        //
        // If we have any SpawnPoint components in the scene, then use those
        //
        var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();
        if ( spawnPoints.Length > 0 )
        {
            return Random.Shared.FromArray( spawnPoints ).Transform.World;
        }

        //
        // Failing that, spawn where we are
        //
        return Transform.World;
    }

    public void SpawnGibs( Vector3 position, Color color = default )
    {
        foreach ( var gibPrefab in CitizenGibs )
        {
            var gib = gibPrefab.Clone( position + Vector3.Random * 5f );
            var outline = gib.Components.GetOrCreate<HighlightOutline>();
            outline.Color = Color.Transparent;
            outline.InsideColor = color;
            outline.ObscuredColor = Color.Transparent;
        }
    }

    static string GetRandomBotName()
    {
        //Simpsons characters separated with newlines
        var names = new string[]
        {
            "Homer",
            "Marge",
            "Bart",
            "Lisa",
            "Maggie",
            "Abe",
            "Moe",
            "Barney",
            "Ned",
            "Milhouse",
            "Krusty",
            "Lenny",
            "Carl",
            "Mr. Burns",
            "Smithers",
            "Skinner",
            "Wiggum",
            "Ralph",
            "Hibbert",
            "Comic Book Guy",
            "Apu",
            "Bob",
            "Itchy",
            "Scratchy",
            "Kang",
            "Kodos",
            "Patty",
            "Selma",
            "Troy",
            "Hutz",
            "Fat Tony",
            "Snake",
            "Gil",
            "Jasper",
            "Disco Stu",
            "Hans Moleman"
        };

        return names[Random.Shared.Next( 0, names.Length )];
    }
}
