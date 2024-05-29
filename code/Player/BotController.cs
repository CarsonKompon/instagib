using System;
using Sandbox;

namespace Instagib;

[Category( "Instagib - Player" )]
public class BotController : Component
{
    [RequireComponent] Player Player { get; set; }
    [RequireComponent] NavMeshAgent Agent { get; set; }

    TimeSince timeSinceLastNavUpdate = 100;
    Vector3 targetPosition = Vector3.Zero;

    protected override void OnStart()
    {
        Agent.UpdatePosition = false;
        Agent.UpdateRotation = false;
    }

    protected override void OnUpdate()
    {
        if ( Random.Shared.Float() < 0.01f ) Player?.Jump();
        if ( Random.Shared.Float() < 0.0025f ) Player?.PrimaryFire();
        if ( Random.Shared.Float() < 0.0025f ) Player?.SecondaryFire();
    }

    protected override void OnFixedUpdate()
    {
        if ( timeSinceLastNavUpdate > 10f || Transform.Position.Distance( targetPosition ) < 200f )
        {
            var randomPlayer = Scene.GetAllComponents<Player>().OrderBy( x => Random.Shared.Float() ).FirstOrDefault();
            if ( randomPlayer is not null )
            {
                targetPosition = randomPlayer.Transform.Position + Vector3.Random.WithZ( 0 ) * 350f;
                Agent.MoveTo( targetPosition );
                timeSinceLastNavUpdate = 0;
            }
        }

        Player.BuildWishVelocity( Agent.WishVelocity.Normal, false );
        Player.Direction = Player.Direction.LerpTo( Rotation.LookAt( Agent.WishVelocity.WithZ( Random.Shared.Float( -90f, 80 ) ), Vector3.Up ), 10 * Time.Delta );

    }
}