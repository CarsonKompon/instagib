using System;
using Sandbox;

namespace Instagib;

[Category( "Instagib - Player" )]
public class BotController : Component
{
    enum BotState
    {
        Attacking,
        Retreating
    }


    [RequireComponent] Player Player { get; set; }
    [RequireComponent] NavMeshAgent Agent { get; set; }

    BotState State = BotState.Attacking;
    TimeSince timeSinceLastNavUpdate = 100;
    TimeSince timesinceLastStateChange = 0;
    Vector3 targetPosition = Vector3.Zero;

    protected override void OnStart()
    {
        Agent.UpdatePosition = false;
        Agent.UpdateRotation = false;
    }

    protected override void OnUpdate()
    {
        if ( !Networking.IsHost ) return;

        if ( Random.Shared.Float() < 0.01f ) Player?.Jump();
        if ( Random.Shared.Float() < 0.0025f ) Player?.PrimaryFire();
        if ( Random.Shared.Float() < 0.0025f ) Player?.SecondaryFire();

        // if ( timesinceLastStateChange > 1f )
        // {
        //     if ( Random.Shared.Float() < 0.2f )
        //     {
        //         State = (BotState)(((int)State + 1) % Enum.GetNames( typeof( BotState ) ).Length);
        //     }
        //     timesinceLastStateChange = 0f;
        // }
    }

    protected override void OnFixedUpdate()
    {
        if ( !Networking.IsHost ) return;

        if ( timeSinceLastNavUpdate > 10f || Transform.Position.Distance( targetPosition ) < 200f )
        {
            var fallback = (Transform.Position + Vector3.Random.WithZ( 0 ) * 350f);
            switch ( State )
            {
                case BotState.Attacking:
                    var randomPlayer = Scene.GetAllComponents<Player>().OrderBy( x => Random.Shared.Float() ).FirstOrDefault();
                    if ( randomPlayer is not null )
                    {
                        targetPosition = Scene.NavMesh.GetRandomPoint( randomPlayer.Transform.Position, 350f ) ?? fallback;
                    }
                    break;
                case BotState.Retreating:
                    targetPosition = Scene.NavMesh.GetRandomPoint( Transform.Position, 1000f ) ?? fallback;
                    break;
            }

            Agent.MoveTo( targetPosition );
            timeSinceLastNavUpdate = 0;

            Player.BuildWishVelocity( Agent.WishVelocity.Normal, false );
            Player.Direction = Player.Direction.LerpTo( Rotation.LookAt( Agent.WishVelocity.WithZ( Random.Shared.Float( -90f, 80 ) ), Vector3.Up ), 10 * Time.Delta );

            // var players = Scene.GetAllComponents<Player>();
            // var nearest = players.OrderBy( x => x.Transform.Position.DistanceSquared( Transform.Position ) ).FirstOrDefault();
            // if ( nearest is not null )
            // {
            //     Player.Direction = Rotation.Slerp( Player.Direction, Rotation.LookAt( (nearest.Transform.Position + Vector3.Random * 2f) - Transform.Position ), 10 * Time.Delta );
            // }

            Player.LookAngles = Player.Direction;
        }
    }
}