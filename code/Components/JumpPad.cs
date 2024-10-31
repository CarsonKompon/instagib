using Sandbox;

namespace Instagib;

public sealed class JumpPad : Component, Component.ITriggerListener
{
    [Property] public float Force { get; set; } = 1200;
    Collider Collider { get; set; }

    protected override void OnStart()
    {
        Collider = Components.Get<Collider>();
        if ( !Collider.IsValid() )
        {
            var boxCollider = AddComponent<BoxCollider>();
            boxCollider.Scale = 64f;
            boxCollider.IsTrigger = true;
            Collider = boxCollider;
        }
    }

    public void OnTriggerEnter( Collider other )
    {
        if ( other.Components.TryGet<Player>( out var player ) )
        {
            if ( !player.IsProxy )
            {
                player.CharacterController.Velocity = player.CharacterController.Velocity.WithZ( 0 );
                player.CharacterController.Punch( Vector3.Up * Force );
            }
        }
    }
}