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
    [Sync] public int Teams { get; set; } = 2;
    [Sync] public NetDictionary<Guid, string> MapVotes { get; set; } = new();
    [Sync] public NetList<Guid> RockTheVotes { get; set; } = new();
    [Sync] public NetDictionary<int, int> TeamScores { get; set; } = new();

    [Sync] public bool ServerLoading { get; set; } = true;
    public bool ClientLoading { get; set; } = false;
    public bool IsTeamGamemode => (int)Gamemode > 0;
    public Dictionary<int, int> TeamCounts
    {
        get
        {
            var counts = new Dictionary<int, int>();
            for ( var i = 1; i <= Teams; i++ )
            {
                counts.Add( i, 0 );
            }
            foreach ( var client in Scene.GetAllComponents<Client>() )
            {
                if ( !counts.ContainsKey( client.Team ) )
                {
                    counts.Add( client.Team, 1 );
                }
                else
                {
                    counts[client.Team]++;
                }
            }
            return counts;
        }
    }

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
            for ( int i = 0; i < MathF.Min( InstagibPreferences.Settings.BotCount, InstagibPreferences.Settings.MaxPlayers - 1 ); i++ )
            {
                AddBot();
            }
            StartGame();
        }
        Gamemode = InstagibPreferences.Settings.Gamemode;
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
        foreach ( var client in Scene.GetAllComponents<Client>() )
        {
            client.Team = 0;
            client.Kills = 0;
            client.Deaths = 0;
            client.Shots = 0;
        }

        RockTheVotes.Clear();
        ServerLoading = true;
        InGame = true;
        Timer = 1000f;
        ChangeMap( map );
    }

    [Broadcast]
    public void EndGame()
    {
        MapVotes.Clear();
        MapVoteTimer = 20f;

        var winner = Scene.GetAllComponents<Client>().OrderByDescending( x => x.Kills ).FirstOrDefault();
        if ( Client.Local.GameObject.Id == winner.GameObject.Id )
        {
            if ( !InstagibPreferences.Stats.TotalWins.ContainsKey( Gamemode ) )
            {
                InstagibPreferences.Stats.TotalWins.Add( Gamemode, 1 );
            }
            else
            {
                InstagibPreferences.Stats.TotalWins[Gamemode]++;
            }
            var stat = "wins_dm";
            if ( Gamemode == Gamemode.TeamDeathmatch ) stat = "wins_tdm";
            Stats.Increment( stat, 1 );
        }
        else
        {
            if ( !InstagibPreferences.Stats.TotalLosses.ContainsKey( Gamemode ) )
            {
                InstagibPreferences.Stats.TotalLosses.Add( Gamemode, 1 );
            }
            else
            {
                InstagibPreferences.Stats.TotalLosses[Gamemode]++;
            }
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

    [Broadcast]
    public void RockTheVote( Guid clientId )
    {
        if ( !Networking.IsHost ) return;
        if ( !InGame ) return;
        if ( RockTheVotes.Contains( clientId ) ) return;

        RockTheVotes.Add( clientId );

        var clients = Scene.GetAllComponents<Client>();
        for ( var i = 0; i < RockTheVotes.Count; i++ )
        {
            if ( !clients.Any( x => x.GameObject.Id == RockTheVotes[i] ) )
            {
                RockTheVotes.RemoveAt( i );
                i--;
            }
        }

        var required = MathF.Ceiling( clients.Where( x => !x.IsBot ).Count() * 0.8f );
        var client = clients.FirstOrDefault( x => x.GameObject.Id == clientId );
        var name = (client is null) ? "Someone" : client.GameObject.Name;
        if ( RockTheVotes.Count >= required )
        {
            EndGame();
            Chatbox.Instance.AddMessage( "🎲", $"{name} has rocked the vote! ({required}/{required} votes reached)" );
        }
        else
        {
            Chatbox.Instance.AddMessage( "🎲", $"{name} has rocked the vote! ({RockTheVotes.Count}/{required} votes needed)" );
        }
    }

    public void OnMapLoaded()
    {
        Log.Info( "MAP LOADED!" );
        ClientLoading = false;
        if ( Networking.IsHost )
        {
            ServerLoading = false;

            Timer = InstagibPreferences.Settings.TimeLimit * 60f;
            FragLimit = InstagibPreferences.Settings.FragLimit;
        }

        Scene.NavMesh.Generate( Scene.PhysicsWorld );
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
            gib.NetworkMode = NetworkMode.Never;
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

    public static Color GetTeamColor( int team )
    {
        switch ( team )
        {
            case 1: return Color.Red;
            case 2: return Color.Blue;
            case 3: return Color.Green;
            case 4: return Color.Yellow;
            case 5: return Color.Magenta;
            case 6: return Color.Orange;
            case 7: return Color.Cyan;
            case 8: return Color.White;
            default: return Color.Black;
        }
    }
}
