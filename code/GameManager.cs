using Sandbox;
using Sandbox.Network;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Instagib;


[Category( "Instagib" )]
public sealed class GameManager : Component, Component.INetworkListener
{
    public static GameManager Instance { get; private set; }

    [Property] GameObject ClientPrefab { get; set; }
	[Property] GameObject PlayerPrefab { get; set; }
	[Property] List<GameObject> SpawnPoints { get; set; }

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
		}
	}

	public void OnActive( Connection channel )
	{
		if ( PlayerPrefab is null )
			return;

		var client = ClientPrefab.Clone(global::Transform.Zero, name: channel.DisplayName);
        client.NetworkSpawn(channel);
	}

    [Broadcast]
	public void SpawnPlayer( Client client )
    {
        if(!Networking.IsHost) return;
        if(Scene.GetAllComponents<Player>().Any(x => x.GameObject.Name == client.GameObject.Id.ToString())) return;
        var startLocation = FindSpawnLocation().WithScale( 1 );
		var player = PlayerPrefab.Clone( startLocation, name: client.GameObject.Id.ToString() );
        var playerScript = player.Components.Get<Player>();
        playerScript.Client = client;
        if(client.IsBot)
        {
            player.Components.Get<HumanController>().Destroy();
            player.Components.Create<BotController>();
        }
		player.NetworkSpawn( client.Network.OwnerConnection );
    }

    [ConCmd("bot_add")]
    public static void AddBot()
    {
        if(!Networking.IsHost) return;
        var clientObj = Instance.ClientPrefab.Clone(global::Transform.Zero, name: GetRandomBotName());
        clientObj.NetworkSpawn(null);
        var client = clientObj.Components.Get<Client>();
        client.IsBot = true;
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

        return names[Random.Shared.Next(0, names.Length)];
    }
}
