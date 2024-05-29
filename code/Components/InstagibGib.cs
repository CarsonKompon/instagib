using System;
using Sandbox;

namespace Instagib;

public sealed class InstagibGib : Component
{
	[RequireComponent] Rigidbody Rigidbody { get; set; }
	[RequireComponent] ParticleEmitter Emitter { get; set; }

	ModelRenderer Renderer;
	TimeSince timeSinceStart = 0;

	protected override void OnStart()
	{
		Renderer = Components.GetInDescendantsOrSelf<ModelRenderer>();
		Rigidbody.AngularVelocity = Vector3.Random * 10;
		Rigidbody.Velocity = Vector3.Random * 950;
		Rigidbody.Velocity = Rigidbody.Velocity.WithZ( MathF.Abs( Rigidbody.Velocity.z ) );
		timeSinceStart = 0;
	}

	protected override void OnFixedUpdate()
	{
		if ( timeSinceStart > 3 )
		{
			Emitter.Enabled = false;
			Renderer.Tint = Renderer.Tint.WithAlpha( 1 - (timeSinceStart - 3) / 2 );

			if ( timeSinceStart > 5 )
			{
				GameObject.Destroy();
				return;
			}
		}
	}
}
