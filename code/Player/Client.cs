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
    [Sync] public int Kills { get; set; } = 0;
    [Sync] public int Deaths { get; set; } = 0;
    [Sync] public RealTimeSince Playtime { get; set; } = 0;
    [Sync] public bool IsBot { get; set; } = false;
    [Sync] public string ColorString { get; set; }
    public Player Player => Scene.GetAllComponents<Player>().FirstOrDefault( x => x.Client.GameObject.Id == GameObject.Id );


    // Private Variables
    internal TimeSince TimeSinceLastDeath = 5;
    private TimeSince TimeSinceLastLookAt = 0;
    private Rotation deadCamRotation = Rotation.Identity;

    protected override void OnStart()
    {
        TimeSinceLastDeath = 5;
        ColorString = new ColorHsv( Random.Shared.Float( 0, 360 ), 0.8f, 1 ).ToColor().Hex;
    }

    protected override void OnFixedUpdate()
    {
        if ( Network.IsProxy ) return;

        if ( !Player.IsValid() )
        {
            if ( TimeSinceLastDeath >= 5f )
            {
                if ( IsBot || Input.Pressed( "Jump" ) )
                {
                    GameManager.Instance.SpawnPlayer( this );
                    TimeSinceLastDeath = 0;
                }
            }

            if ( Network.IsOwner )
            {
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
                    var nearestPlayer = Scene.GetAllComponents<Player>().Where( x =>
                    {
                        var tr = Scene.Trace.Ray( Scene.Camera.Transform.Position, x.Transform.Position ).Run();
                        return tr.GameObject.IsValid() && tr.GameObject.Tags.Has( "player" );
                    } ).OrderBy( x => x.Transform.Position.Distance( Scene.Camera.Transform.Position ) ).FirstOrDefault();
                    Vector3 lookAt = Vector3.Zero;
                    if ( nearestPlayer is not null && Scene.Camera.Transform.Position.Distance( nearestPlayer.Transform.Position ) < 700 )
                    {
                        lookAt = nearestPlayer.Transform.Position;
                        TimeSinceLastLookAt = 0f;
                    }
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
        }
    }
}