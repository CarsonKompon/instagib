using Sandbox;

namespace Instagib;

public sealed class ViewModel : Component
{
	public static ViewModel Instance { get; private set; }

	[Property] GameObject Body { get; set; }

	Vector3 BasePosition;
	Vector3 Offset;
	Rotation BaseRotation;
	Angles CurrentRotation;
	float upwardLook = 0;

	protected override void OnAwake()
	{
		BasePosition = LocalPosition;
		BaseRotation = LocalRotation;
		Instance = this;
	}

	protected override void OnUpdate()
	{
		Body.Enabled = Player.Local.IsValid();

		upwardLook += Input.AnalogLook.pitch / 4f;
		CurrentRotation = new Angles( upwardLook, Player.Local?.headRoll ?? 0, 0 );
		LocalRotation = BaseRotation * CurrentRotation;

		LocalPosition = BasePosition + Offset;

		Offset = Offset.LerpTo( 0, 10 * Time.Delta );
		upwardLook = upwardLook.LerpTo( 0, 10 * Time.Delta );
	}

	public void Recoil( float amount = 5f )
	{
		Offset += Vector3.Backward * amount;
	}
}
