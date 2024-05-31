using System;
using Sandbox;

namespace Instagib;

public class Client : Component
{
    // Static Variables
    public static Client Local
    {
        get
        {
            if ( !_local.IsValid() )
            {
                _local = Game.ActiveScene.GetAllComponents<Client>().FirstOrDefault( x => x.Network.IsOwner );
            }
            return _local;
        }
    }
    private static Client _local;

    // Public Variables
    [HostSync] public int Kills { get; set; } = 0;
    [HostSync] public int Deaths { get; set; } = 0;
    [Sync] public int Shots { get; set; } = 0;
    [Sync] public RealTimeSince Playtime { get; set; } = 0;
    [HostSync] public bool IsBot { get; set; } = false;
    [Sync] public string ColorString { get; set; }
    [Sync] public string Killstreak { get; set; } = "";
    [Sync] public int Team { get; set; } = 0;
    public float Aim => (float)Kills / (float)Shots;
    public Player Player => Scene.GetAllComponents<Player>().FirstOrDefault( x => x.GameObject.Name == GameObject.Id.ToString() );
    public string Name => IsBot ? GameObject.Name : Network.OwnerConnection.DisplayName;


    // Private Variables
    internal TimeSince TimeSinceLastDeath = 5;
    private TimeSince TimeSinceLastLookAt = 0;
    private TimeSince TimeSinceKillstreak = 100f;
    private Rotation deadCamRotation = Rotation.Identity;

    protected override void OnStart()
    {
        TimeSinceLastDeath = 5;
        if ( GameManager.Instance.IsTeamGamemode )
        {
            ColorString = Color.Lerp( Color.Gray, Color.Black, 0.5f ).Hex;
        }
        else
        {
            ColorString = new ColorHsv( Random.Shared.Float( 0, 360 ), 0.8f, 1 ).ToColor().Hex;
        }
    }

    protected override void OnFixedUpdate()
    {
        if ( Network.IsProxy ) return;

        if ( GameManager.Instance.InGame && !GameManager.Instance.ClientLoading && !GameManager.Instance.ServerLoading && !Player.IsValid() )
        {
            if ( GameManager.Instance.IsTeamGamemode && Team == 0 )
            {
                if ( IsBot )
                {
                    JoinRandomTeam();
                }
            }
            else
            {
                if ( TimeSinceLastDeath >= 5f )
                {
                    if ( IsBot || Input.Pressed( "Jump" ) )
                    {
                        GameManager.Instance.SpawnPlayer( GameObject.Id );
                        TimeSinceLastDeath = 0;
                    }
                }

                if ( Network.IsOwner )
                {
                    UpdateDeathCam();
                }
            }
        }

        if ( !string.IsNullOrEmpty( Killstreak ) && TimeSinceKillstreak > 8f )
        {
            Killstreak = "";
        }
    }

    public async void SetKillstreak( int streak )
    {
        TimeSinceKillstreak = 0;
        Killstreak = "";
        await Task.DelayRealtime( 100 );
        switch ( streak )
        {
            case 1: Killstreak = "DOUBLE KILL"; break;
            case 2: Killstreak = "DOUBLE KILL"; break;
            case 3: Killstreak = "TRIPLE KILL"; break;
            case 4: Killstreak = "MEGA KILL"; break;
            case 5: Killstreak = "HYPER KILL"; break;
            case 6: Killstreak = "ULTRA KILL"; break;
            default: Killstreak = "UNSTOPPABLE"; break;
        }
    }

    void UpdateDeathCam()
    {
        Scene.Camera.FieldOfView = Scene.Camera.FieldOfView.LerpTo( 100, 10 * Time.Delta );
        if ( Deaths == 0 )
        {
            var spawnPoints = Scene.GetAllComponents<SpawnPoint>();
            Vector3 center = Vector3.Zero;
            foreach ( var spawnPoint in spawnPoints )
            {
                center += spawnPoint.Transform.Position;
            }
            center /= spawnPoints.Count();
            Vector3 camPos = center + new Vector3(
                MathF.Sin( Time.Now * 0.1f ) * 600,
                MathF.Cos( Time.Now * 0.1f ) * 600,
                600
            );
            center += Vector3.Up * 80f;
            var rotation = Rotation.LookAt( center - camPos, Vector3.Up );
            Scene.Camera.Transform.Position = Scene.Camera.Transform.Position.LerpTo( camPos, 10 * Time.Delta );
            Scene.Camera.Transform.Rotation = Rotation.Slerp( Scene.Camera.Transform.Rotation, rotation, 10 * Time.Delta );
        }
        else
        {
            var nearestPlayer = Scene.GetAllComponents<Player>().OrderBy( x => -x.Transform.Position.Distance( Scene.Camera.Transform.Position ) ).FirstOrDefault();
            Vector3 lookAt = Vector3.Zero;
            if ( nearestPlayer is not null )
            {
                lookAt = nearestPlayer.Transform.Position;
                TimeSinceLastLookAt = 0f;
            }
            // else if ( Scene.GetAllComponents<InstagibGib>().OrderBy( x => x.Transform.Position.Distance( Scene.Camera.Transform.Position ) ).FirstOrDefault() is InstagibGib gib && Scene.Camera.Transform.Position.Distance( gib.Transform.Position ) < 700 )
            // {
            //     lookAt = gib.Transform.Position;
            //     TimeSinceLastLookAt = 0f;
            // }
            else if ( TimeSinceLastLookAt > 3f )
            {
                lookAt = Scene.Camera.Transform.Position + new Vector3(
                    MathF.Sin( Time.Now * 0.1f ) * 600,
                    MathF.Cos( Time.Now * 0.1f ) * 600,
                    0
                );
            }

            if ( lookAt != Vector3.Zero )
            {
                deadCamRotation = Rotation.LookAt( lookAt - Scene.Camera.Transform.Position, Vector3.Up );
            }
            Scene.Camera.Transform.Rotation = Rotation.Slerp( Scene.Camera.Transform.Rotation, deadCamRotation, (lookAt == Vector3.Zero ? 4 : 10) * Time.Delta );
        }
    }

    void JoinRandomTeam()
    {
        Dictionary<int, int> teamCounts = new();
        teamCounts[1] = 0;
        teamCounts[2] = 0;
        foreach ( var client in Scene.GetAllComponents<Client>() )
        {
            if ( client.Team != 0 )
            {
                if ( !teamCounts.ContainsKey( client.Team ) )
                {
                    teamCounts[client.Team] = 0;
                }
                teamCounts[client.Team]++;
            }
        }

        int team = 0;
        if ( teamCounts.Count > 0 )
        {
            team = teamCounts.OrderBy( x => x.Value ).FirstOrDefault().Key;
        }
        else
        {
            team = Random.Shared.Int( 1, 2 );
        }

        if ( team > 0 ) JoinTeam( team );
    }

    public void JoinTeam( int team )
    {
        Team = team;
        ColorString = GameManager.GetTeamColor( team ).Hex;
    }

    [Broadcast]
    public void ResetData()
    {
        Team = 0;
        Kills = 0;
        Deaths = 0;
        Shots = 0;
    }
}