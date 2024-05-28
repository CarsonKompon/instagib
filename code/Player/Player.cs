using System;
using System.Runtime.InteropServices;
using Sandbox;
using Sandbox.Citizen;

namespace Instagib;

[Category("Instagib - Player")]
public sealed class Player : Component
{
	// Static Variables
	public static Player Local
	{
		get
		{
			if(!_local.IsValid())
			{
				_local = Game.ActiveScene.GetAllComponents<Player>().FirstOrDefault(x => !x.Network.IsOwner);
			}
			return _local;
		}
	}
	private static Player _local;

	// Properties
	[RequireComponent] CharacterController CharacterController { get; set; }
	[RequireComponent] CitizenAnimationHelper AnimationHelper { get; set; }
	SkinnedModelRenderer BodyRenderer { get; set; }

	[Group("References"), Property] GameObject Body { get; set; }
	[Group("References"), Property] GameObject Head { get; set; }
	[Group("References"), Property] GameObject Shadow { get; set; }

	[Group("Movement"), Property] Vector3 Gravity { get; set; } = new Vector3(0, 0, -800);
	[Group("Movement"), Property] float GroundControl { get; set; } = 8.0f;
	[Group("Movement"), Property] float AirControl { get; set; } = 0.8f;
	[Group("Movement"), Property] float Speed { get; set; } = 590f;
	[Group("Movement"), Property] float JumpForce { get; set; } = 400f;

	[Group("Combat"), Property] float BounceRange { get; set; } = 150f;
	[Group("Combat"), Property] float BounceTimer { get; set; } = 0.8f;
	[Group("Combat"), Property] float DashTimer { get; set; } = 1f;

	[Group("Prefabs"), Property] GameObject BeamPrefab { get; set; }
	[Group("Prefabs"), Property] GameObject ReticlePrefab { get; set; }

	[Group("Particles"), Property] GameObject LaserDustParticle { get; set; }
	[Group("Particles"), Property] GameObject BounceParticle { get; set; }

	// Public Member Variables
	public bool CanDash => timeSinceLastDash >= DashTimer;
	public bool CanFire => timeSinceLastFire >= 0.6f;
	public bool CanBounce { get; private set; } = false;
	public Vector3 WishVelocity = Vector3.Zero;
	public Angles Direction = Angles.Zero;
	public Client Client { get; set; }

	// Private Member Variables
	private Angles visualDirection = Angles.Zero;
	private float headRoll = 0f;
	private TimeSince timeSinceLastFire = 0;
	private TimeSince timeSinceLastBounce = 0;
	private TimeSince timeSinceLastDash = 0;
	private GameObject bounceReticle = null;

	protected override void OnAwake()
	{
		BodyRenderer = Body.Components.Get<SkinnedModelRenderer>();
	}

	protected override void OnStart()
	{
		if(Network.IsOwner)
		{
			bounceReticle = ReticlePrefab.Clone();
			bounceReticle.Enabled = false;
		}
	}

	protected override void OnUpdate()
	{
		if(Network.IsOwner)
		{
			UpdateCamera();
		}

		UpdateBounceReticle();
		UpdateAnimations();		
	}

	protected override void OnFixedUpdate()
	{
		Move();
		RotateBody();
		UpdateShadow();

		BodyRenderer.RenderType = Network.IsOwner ? ModelRenderer.ShadowRenderType.ShadowsOnly : ModelRenderer.ShadowRenderType.On;
	}

	public void BuildWishVelocity(Vector2 input, bool respectDirection = true)
	{
		WishVelocity = 0f;
		
		var _rot = Direction.ToRotation();
		if(respectDirection)
		{
			WishVelocity += _rot.Forward * input.x;
			WishVelocity += _rot.Left * input.y;
		}
		else
		{
			WishVelocity = input;
		}
		WishVelocity = WishVelocity.WithZ(0);

		if(!WishVelocity.IsNearZeroLength) WishVelocity = WishVelocity.Normal;

		WishVelocity *= Speed;
	}

	void Move()
	{
		if(CharacterController.IsOnGround)
		{
			// Apply friction/acceleration
			CharacterController.Velocity = CharacterController.Velocity.WithZ(0);
			CharacterController.Accelerate(WishVelocity);
			CharacterController.ApplyFriction(GroundControl);
		}
		else
		{
			// Apply air control/gravity
			CharacterController.Velocity += Gravity * Time.Delta * 0.5f;
			CharacterController.Accelerate(WishVelocity);
			CharacterController.ApplyFriction(AirControl);
		}

		CharacterController.Move();

		if(CharacterController.IsOnGround)
		{
			CharacterController.Velocity = CharacterController.Velocity.WithZ(0);
		}
		else
		{
			CharacterController.Velocity += Gravity * Time.Delta * 0.5f;
		}
	}

	void UpdateCamera()
	{
		var _input = Input.AnalogLook * Preferences.Sensitivity;
		Direction.pitch += _input.pitch / 20f;
		Direction.yaw += _input.yaw / 20f;
		Direction.roll = 0f;
		Direction.pitch = Direction.pitch.Clamp(-89.9f, 89.9f);

		visualDirection = Direction;
		var _shake = CharacterController.Velocity.Length / Speed;
		visualDirection.pitch += MathF.Sin(Time.Now * 10f) * _shake;
		visualDirection.yaw += MathF.Sin(Time.Now * 9.5f) * _shake;
		headRoll = headRoll.LerpTo((Input.AnalogMove.y - _input.yaw/8f) * -1f, Time.Delta * 10f);
		visualDirection.roll = headRoll;

		if(Scene.Camera is not null)
		{
			Scene.Camera.Transform.Position = Head.Transform.Position;
			Scene.Camera.Transform.Rotation = visualDirection;
		}
	}

	void UpdateBounceReticle()
	{
		var dir = Client.IsBot ? Direction : visualDirection;
		var tr = Scene.Trace.Ray(Head.Transform.Position, Head.Transform.Position + dir.Forward * BounceRange)
			.IgnoreGameObjectHierarchy(GameObject)
			.WithoutTags("trigger")
			.Run();

		CanBounce = tr.Hit;
		
		if(Network.IsOwner)
		{
			bounceReticle.Enabled = tr.Hit;
			if(tr.Hit)
			{
				bounceReticle.Transform.Position = tr.HitPosition - visualDirection.Forward * 20f;
			}
		}
	}

	public void Jump()
	{
		if(!CharacterController.IsOnGround) return;

		CharacterController.Punch(Vector3.Up * JumpForce);
		BroadcastJump();
	}

	public void PrimaryFire()
	{
		if(!CanFire) return;

		var dir = Client.IsBot ? Direction : visualDirection;
		var tr = Scene.Trace.Ray(Head.Transform.Position, Head.Transform.Position + dir.Forward * 5000f)
			.IgnoreGameObjectHierarchy(GameObject)
			.WithoutTags("trigger")
			.Run();
		var endPos = tr.Hit ? tr.HitPosition : tr.EndPosition;
		var distance = tr.StartPosition.Distance(endPos);

		if(tr.Hit && tr.GameObject is not null && tr.GameObject.Components.TryGet<Player>(out var hitPlayer))
		{
			if(hitPlayer.GameObject.Id == GameObject.Id) return;
			Client.Kills++;
			hitPlayer?.Kill(Client.GameObject.Name);
		}

		CharacterController.Punch(dir.Forward * -(400f * (1f-(distance / 5000f))));

		BroadcastBeam(tr.StartPosition + tr.Direction * 15f, endPos, Client.Color);
		timeSinceLastFire = 0;
	}

	public void SecondaryFire()
	{
		if(!CanBounce) return;
		if(timeSinceLastBounce < BounceTimer) return;

		var dir = Client.IsBot ? Direction : visualDirection;
		var tr = Scene.Trace.Ray(Head.Transform.Position, Head.Transform.Position + dir.Forward * BounceRange)
			.IgnoreGameObjectHierarchy(GameObject)
			.WithoutTags("trigger")
			.Run();

		if(tr.Hit)
		{
			var force = -dir.Forward * 500f;
			force = force.WithZ(force.z * 1.5f);
			CharacterController.Punch(force);
			timeSinceLastBounce = 0;
			BroadcastBounce(tr.HitPosition, -dir.Forward);
		}
	}

	[Broadcast]
	public void Kill(string killer = null)
	{
		Sound.Play("snd-kill", Transform.Position);
		if(!Network.IsProxy)
		{
			var killMsg = (killer is null) ? $"{Client.GameObject.Name} was killed" : $"{Client.GameObject.Name} was killed by {killer}";
			Chatbox.Instance.AddMessage("☠️", killMsg, "kill-feed");
			Client.TimeSinceLastDeath = 0;
			Client.Deaths++;
			GameObject.Destroy();
		}
	}
	
	void RotateBody()
	{
		if(Body is null) return;

		var targetAngle = new Angles(0, Direction.yaw, 0);
		float rotateDifference = Body.Transform.Rotation.Distance(targetAngle);

		if(rotateDifference > 40f || CharacterController.Velocity.Length > 10f)
		{
			Body.Transform.Rotation = Angles.Lerp(Body.Transform.Rotation, targetAngle, Time.Delta * 10f);
		}
	}

	void UpdateAnimations()
	{
		AnimationHelper.IsGrounded = CharacterController.IsOnGround;
		AnimationHelper.WithVelocity(CharacterController.Velocity);
		AnimationHelper.WithWishVelocity(WishVelocity);
		AnimationHelper.WithLook(Direction.Forward);
	}

	void UpdateShadow()
	{
		var floorTr = Scene.Trace.Ray(Transform.Position, Transform.Position + Vector3.Down * 5000f)
			.WithoutTags("trigger")
			.Run();
		
		Shadow.Transform.Position = floorTr.Hit ? (floorTr.HitPosition + Vector3.Up) : floorTr.EndPosition;
		Shadow.Transform.Rotation = Rotation.LookAt(floorTr.Normal) * Rotation.From( new Angles( 0, 90, 90 ) );
	}

	[Broadcast]
	void BroadcastJump()
	{
		AnimationHelper?.TriggerJump();
	}

	[Broadcast]
	void BroadcastBeam(Vector3 startPos, Vector3 endPos, Color color)
	{
		var midpos = startPos + (endPos - startPos) / 2f;
		var beamObj = BeamPrefab.Clone(midpos);
		if(beamObj.Components.TryGet<Beam>(out var beam))
		{
			beam.StartPosition = startPos;
			beam.EndPosition = endPos;
		}
		if(beamObj.Components.TryGet<ModelRenderer>(out var modelRenderer))
		{
			modelRenderer.Tint = color;
		}

		LaserDustParticle.Clone(new Transform(endPos, Rotation.LookAt(startPos - endPos) * Rotation.From(new Angles(0, 0, 90)), Vector3.One));

		Sound.Play("snd-fire", startPos);
	}

	[Broadcast]
	void BroadcastBounce(Vector3 position, Vector3 normal)
	{
		Sound.Play("snd-bounce", position);

		var transform = new Transform(position, Rotation.LookAt(normal) * Rotation.From( new Angles( 0, 0, 90 ) ), Vector3.One);
		BounceParticle.Clone(transform);
	}
}
