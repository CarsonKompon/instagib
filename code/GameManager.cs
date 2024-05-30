using Sandbox;
using Sandbox.Network;
using Sandbox.Services;
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
    [Property] public MapInstance MapInstance { get; set; }
    [Property] List<GameObject> SpawnPoints { get; set; }

    [Sync] public Gamemode Gamemode { get; set; } = Gamemode.Deathmatch;
    [Sync] public bool InGame { get; set; } = false;
    [Sync] public TimeUntil MapVoteTimer { get; set; } = 60;
    [Sync] public TimeUntil Timer { get; set; } = 60;
    [Sync] public int FragLimit { get; set; } = 30;
    [Sync] public NetDictionary<Guid, string> MapVotes { get; set; } = new();

    [Sync] public bool ServerLoading { get; set; } = true;
    public bool ClientLoading { get; set; } = true;

    [Group( "Prefabs" ), Property] public GameObject BeamPrefab { get; set; }
    [Group( "Prefabs" ), Property] public GameObject ReticlePrefab { get; set; }

    [Group( "Particles" ), Property] public GameObject LaserDustParticle { get; set; }
    [Group( "Particles" ), Property] public GameObject BounceParticle { get; set; }

    [Group( "Gibs" ), Property] public List<GameObject> CitizenGibs { get; set; }

    protected override void OnAwake()
    {
        Instance = this;
    }

    protected override void OnStart()
    {
        MapInstance.OnMapLoaded += OnMapLoaded;
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
            StartGame();
        }
    }

    public void OnActive( Connection channel )
    {
        if ( PlayerPrefab is null )
            return;

        var client = ClientPrefab.Clone( global::Transform.Zero, name: channel.DisplayName );
        client.NetworkSpawn( channel );
    }

    protected override void OnFixedUpdate()
    {
        if ( !Networking.IsHost ) return;

        if ( InGame )
        {
            foreach ( var client in Scene.GetAllComponents<Client>() )
            {
                if ( client.Kills >= FragLimit )
                {
                    EndGame();
                    return;
                }
            }

            if ( Timer <= 0 )
            {
                EndGame();
            }
        }
        else if ( !InGame && !ServerLoading && MapVoteTimer <= 0 )
        {
            StartGame();
        }
    }

    public void StartGame()
    {
        if ( !Networking.IsHost ) return;
        if ( InGame ) return;

        Log.Info( "STARTING GAME!" );

        var map = MapInstance.MapName;
        if ( MapVotes.Count() > 0 )
        {
            // Choose randomly with weight of int
            var votes = GetVotedMaps();
            var totalVotes = votes.Values.Sum();
            var random = Random.Shared.Next( 0, totalVotes );
            var current = 0;
            foreach ( var vote in votes )
            {
                current += vote.Value;
                if ( random < current )
                {
                    map = vote.Key;
                    break;
                }
            }
            MapVotes.Clear();
        }
        foreach ( var player in Scene.GetAllComponents<Client>() )
        {
            player.Kills = 0;
            player.Deaths = 0;
            player.Shots = 0;
        }


        ServerLoading = true;
        InGame = true;
        Timer = 1000f;
        ChangeMap( map );
    }

    [Broadcast]
    public void EndGame()
    {
        var winner = Scene.GetAllComponents<Client>().OrderByDescending( x => x.Kills ).FirstOrDefault();
        if ( Client.Local.GameObject.Id == winner.GameObject.Id )
        {
            if ( !InstagibPreferences.Stats.TotalWins.ContainsKey( Gamemode ) )
            {
                InstagibPreferences.Stats.TotalWins.Add( Gamemode, 0 );
            }
            var stat = "wins_dm";
            if ( Gamemode == Gamemode.TeamDeathmatch ) stat = "wins_tdm";
            Stats.Increment( stat, 1 );
        }
        else
        {
            InstagibPreferences.Stats.TotalLosses[Gamemode]++;
            var stat = "losses_dm";
            if ( Gamemode == Gamemode.TeamDeathmatch ) stat = "losses_tdm";
            Stats.Increment( stat, 1 );
        }
        Stats.SetValue( "accuracy", InstagibPreferences.Stats.OverallAccuracy );

        if ( !Networking.IsHost ) return;
        if ( !InGame ) return;

        Log.Info( "ENDING GAME!" );

        InGame = false;
        foreach ( var player in Scene.GetAllComponents<Player>() )
        {
            player.Kill();
        }

        MapVotes.Clear();
        MapVoteTimer = 20f;
    }

    [Broadcast]
    void ChangeMap( string map )
    {
        InstagibPreferences.Save();

        Log.Info( "CHANGING MAP (LOADING!!)" );
        ClientLoading = true;

        if ( MapInstance.MapName != map )
        {
            MapInstance.UnloadMap();
            MapInstance.MapName = map;
        }
        else
        {
            OnMapLoaded();
        }
    }

    public void OnMapLoaded()
    {
        Log.Info( "MAP LOADED!" );
        ClientLoading = false;
        if ( Networking.IsHost )
        {
            ServerLoading = false;

            Gamemode = InstagibPreferences.Settings.Gamemode;
            Timer = InstagibPreferences.Settings.TimeLimit * 60f;
            FragLimit = InstagibPreferences.Settings.FragLimit;
        }
    }

    [Broadcast]
    public void SpawnPlayer( Guid clientId )
    {
        if ( !Networking.IsHost ) return;

        var client = Scene.GetAllComponents<Client>().FirstOrDefault( x => x.GameObject.Id == clientId );
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

    [Broadcast]
    public void VoteMap( Guid clientId, string mapName )
    {
        if ( !Networking.IsHost ) return;

        if ( MapVotes.ContainsKey( clientId ) )
        {
            MapVotes[clientId] = mapName;
        }
        else
        {
            MapVotes.Add( clientId, mapName );
        }
    }

    public Dictionary<string, int> GetVotedMaps()
    {
        var votes = new Dictionary<string, int>();
        foreach ( var vote in MapVotes.Values )
        {
            if ( votes.ContainsKey( vote ) )
            {
                votes[vote]++;
            }
            else
            {
                votes.Add( vote, 1 );
            }
        }
        return votes;
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
            var renderer = gib.Components.GetInChildrenOrSelf<ModelRenderer>();
            renderer.Tint = color;
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
