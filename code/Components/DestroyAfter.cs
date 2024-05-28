using Sandbox;

public sealed class DestroyAfter : Component
{
	[Property] float Seconds { get; set; } = 1f;

	TimeSince timeSinceStart = 0;

	protected override void OnFixedUpdate()
	{
		if(timeSinceStart > Seconds)
		{
			GameObject.Destroy();
		}
	}
}
