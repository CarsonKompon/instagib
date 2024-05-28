using Sandbox;

namespace Instagib;

public class Client : Component
{
	// Static Variables
	public static Client Local
	{
		get
		{
			if(!_local.IsValid())
			{
				_local = Game.ActiveScene.GetAllComponents<Client>().FirstOrDefault(x => !x.Network.IsOwner);
			}
			return _local;
		}
	}
	private static Client _local;

    // Public Variables
    [Sync] public int Kills { get; set; } = 0;
    [Sync] public int Deaths { get; set; } = 0;
    [Sync] public RealTimeSince Playtime { get; set; } = 0;
    [Sync] public bool IsBot { get; set; } = false;
    [Sync] public Color Color { get; set; } = Color.White;
    public Player Player => Scene.GetAllComponents<Player>().FirstOrDefault(x => x.Client.GameObject.Id == GameObject.Id);


    // Private Variables
    internal TimeSince TimeSinceLastDeath = 5;

    protected override void OnStart()
    {
        TimeSinceLastDeath = 5;
        if(!Network.IsProxy)
        {
            Color = Color.Random;
        }
    }

	protected override void OnFixedUpdate()
	{
        if(Network.IsProxy) return;

        if(TimeSinceLastDeath >= 5f && !Player.IsValid())
        {
            if(IsBot || Input.Pressed("Jump"))
            {
                GameManager.Instance.SpawnPlayer(this);
                TimeSinceLastDeath = 0;
            }
        }
	}
}