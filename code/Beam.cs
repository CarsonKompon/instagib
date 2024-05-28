using Sandbox;

public sealed class Beam : Component
{
	[RequireComponent] ModelRenderer Renderer { get; set; }
	
	public Vector3 StartPosition { get; set; }
	public Vector3 EndPosition { get; set; }

	TimeSince timeSinceStart = 0;
	float lifetime = 0.5f;

	protected override void OnStart()
	{
		timeSinceStart = 0;
		OnFixedUpdate();
		Renderer.Enabled = true;
	}

	protected override void OnFixedUpdate()
	{
		if(timeSinceStart > lifetime)
		{
			GameObject.Destroy();
			return;
		}

		var distance = StartPosition.Distance(EndPosition);
		var rotation = Rotation.LookAt(StartPosition - EndPosition);

		var size = (lifetime - timeSinceStart) / lifetime;
		Transform.Position = StartPosition + (EndPosition - StartPosition) / 2f;
		Transform.Rotation = rotation;
		Transform.Scale = new Vector3(distance/50f, size/10f, size/10f);
	}
}
