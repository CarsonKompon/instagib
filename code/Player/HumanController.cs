using Sandbox;

namespace Instagib;

[Category("Instagib - Player")]
public class HumanController : Component
{
    [RequireComponent] Player Player { get; set; }

    protected override void OnUpdate()
    {
        if(Input.Pressed("Jump")) Player?.Jump();
		if(Input.Down("Attack1")) Player?.PrimaryFire();
		if(Input.Down("Attack2")) Player?.SecondaryFire();
    }

	protected override void OnFixedUpdate()
	{
		Player?.BuildWishVelocity(Input.AnalogMove);
	}
}