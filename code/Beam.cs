using Sandbox;

public sealed class Beam : Component
{
	public Vector3 StartPosition { get; set; }
	public Vector3 EndPosition { get; set; }
	public Color Color { get; set; }

	TimeSince timeSinceStart = 0;
	float lifetime = 0.5f;

	List<ModelRenderer> renderers = new();

	protected override void OnStart()
	{
		timeSinceStart = 0;
		OnFixedUpdate();
		renderers = Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndChildren ).ToList();
		foreach ( var renderer in renderers )
		{
			renderer.Enabled = true;
		}
	}

	protected override void OnFixedUpdate()
	{
		foreach ( var renderer in renderers )
		{
			if ( !renderer.IsValid() ) continue;
			renderer.Tint = Color.WithAlpha( 0.8f );
		}

		if ( timeSinceStart > lifetime )
		{
			GameObject.Destroy();
			return;
		}

		var distance = StartPosition.Distance( EndPosition );
		var rotation = Rotation.LookAt( StartPosition - EndPosition );

		var size = (lifetime - timeSinceStart) / lifetime;
		Transform.Position = StartPosition + (EndPosition - StartPosition) / 2f;
		Transform.Rotation = rotation;
		Transform.Scale = new Vector3( distance / 50f, size / 10f, size / 10f );
	}
}
