using System;
using System.Runtime.InteropServices;
using Sandbox;
using Sandbox.Citizen;

namespace Parkour;

public sealed class Player : Component
{
	// Properties
	[RequireComponent] CharacterController CharacterController { get; set; }
	[RequireComponent] CitizenAnimationHelper AnimationHelper { get; set; }
	SkinnedModelRenderer BodyRenderer { get; set; }

	[Group("References"), Property] GameObject Body { get; set; }
	[Group("References"), Property] GameObject Head { get; set; }

	[Group("Movement"), Property] Vector3 Gravity { get; set; } = new Vector3(0, 0, -800);
	[Group("Movement"), Property] float GroundControl { get; set; } = 4.0f;
	[Group("Movement"), Property] float AirControl { get; set; } = 1.0f;
	[Group("Movement"), Property] float Speed { get; set; } = 160f;
	[Group("Movement"), Property] float JumpForce { get; set; } = 400f;
	[Group("Movement"), Property] float DashTimer { get; set; } = 1.0f;

	[Group("Prefabs"), Property] GameObject BeamPrefab { get; set; }

	// Public Member Variables
	public bool CanDash => timeSinceLastDash >= DashTimer;
	public bool CanFire => timeSinceLastFire >= 0.6f;
	public Vector3 WishVelocity = Vector3.Zero;
	public Angles Direction = Angles.Zero;

	// Private Member Variables
	private float headRoll = 0f;
	private TimeSince timeSinceLastFire = 0;
	private TimeSince timeSinceLastDash = 0;

	protected override void OnAwake()
	{
		BodyRenderer = Body.Components.Get<SkinnedModelRenderer>();
	}

	protected override void OnUpdate()
	{
		UpdateCamera();
		UpdateAnimations();

		if(Input.Pressed("Jump")) Jump();
		if(Input.Down("Attack1")) PrimaryFire();
		
	}

	protected override void OnFixedUpdate()
	{
		BuildWishVelocity();
		Move();
		RotateBody();

		BodyRenderer.RenderType = Network.IsProxy ? ModelRenderer.ShadowRenderType.On : ModelRenderer.ShadowRenderType.ShadowsOnly;
	}

	void BuildWishVelocity()
	{
		WishVelocity = 0f;
		
		var _rot = Direction.ToRotation();
		WishVelocity += _rot.Forward * Input.AnalogMove.x;
		WishVelocity += _rot.Left * Input.AnalogMove.y;
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

		var _headRot = Direction;
		var _shake = CharacterController.Velocity.Length / Speed;
		_headRot.pitch += MathF.Sin(Time.Now * 10f) * _shake;
		_headRot.yaw += MathF.Sin(Time.Now * 9.5f) * _shake;
		headRoll = headRoll.LerpTo((Input.AnalogMove.y - _input.yaw/8f) * -1f, Time.Delta * 10f);
		_headRot.roll = headRoll;

		if(Scene.Camera is not null)
		{
			Scene.Camera.Transform.Position = Head.Transform.Position;
			Scene.Camera.Transform.Rotation = _headRot;
		}
	}

	void Jump()
	{
		if(!CharacterController.IsOnGround) return;

		CharacterController.Punch(Vector3.Up * JumpForce);
		BroadcastJump();
	}

	void PrimaryFire()
	{
		if(!CanFire) return;

		var tr = Scene.Trace.Ray(Head.Transform.Position, Head.Transform.Position + Direction.Forward * 1000f)
			.WithoutTags("trigger")
			.Run();

		CharacterController.Punch(Direction.Forward * -(250f * ((CharacterController.IsOnGround || !tr.Hit) ? 1f : 2f)));

		BroadcastBeam(tr.StartPosition + tr.Direction * 15f, tr.Hit ? tr.HitPosition : tr.EndPosition);
		timeSinceLastFire = 0;
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

	[Broadcast]
	void BroadcastJump()
	{
		AnimationHelper?.TriggerJump();
	}

	[Broadcast]
	void BroadcastBeam(Vector3 startPos, Vector3 endPos)
	{
		var midpos = startPos + (endPos - startPos) / 2f;
		var beamObj = BeamPrefab.Clone(midpos);
		if(beamObj.Components.TryGet<Beam>(out var beam))
		{
			beam.StartPosition = startPos;
			beam.EndPosition = endPos;
		}
	}
}
