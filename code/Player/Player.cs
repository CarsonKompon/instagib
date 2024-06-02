using System;
using System.Runtime.InteropServices;
using Sandbox;
using Sandbox.Citizen;

namespace Instagib;

[Category( "Instagib - Player" )]
public sealed class Player : Component
{
	// Static Variables
	public static Player Local
	{
		get
		{
			if ( !_local.IsValid() )
			{
				_local = Game.ActiveScene.GetAllComponents<Player>().FirstOrDefault( x => x.Network.IsOwner );
			}
			return _local;
		}
	}
	private static Player _local;

	// Properties
	[RequireComponent] CharacterController CharacterController { get; set; }
	[RequireComponent] CitizenAnimationHelper AnimationHelper { get; set; }
	SkinnedModelRenderer BodyRenderer { get; set; }

	[Group( "References" ), Property] GameObject Body { get; set; }
	[Group( "References" ), Property] GameObject Head { get; set; }
	[Group( "References" ), Property] GameObject Shadow { get; set; }

	[Group( "Movement" ), Property] Vector3 Gravity { get; set; } = new Vector3( 0, 0, -800 );
	[Group( "Movement" ), Property] float GroundControl { get; set; } = 8.0f;
	[Group( "Movement" ), Property] float AirControl { get; set; } = 0.8f;
	[Group( "Movement" ), Property] float Speed { get; set; } = 590f;
	[Group( "Movement" ), Property] float JumpForce { get; set; } = 400f;

	[Group( "Combat" ), Property] float BounceRange { get; set; } = 150f;
	[Group( "Combat" ), Property] float BounceTimer { get; set; } = 0.8f;
	[Group( "Combat" ), Property] float DashTimer { get; set; } = 1f;

	// Public Member Variables
	public bool CanDash => timeSinceLastDash >= DashTimer;
	public bool CanFire => timeSinceLastFire >= 0.6f;
	public bool CanBounce { get; private set; } = false;
	public bool IsInvulnerable => timeSinceSpawned < 3f;
	[Sync] public Angles LookAngles { get; set; } = Angles.Zero;
	[Sync] public Vector3 WishVelocity { get; set; } = Vector3.Zero;
	public Angles Direction = Angles.Zero;
	public Client Client => Scene.GetAllComponents<Client>().FirstOrDefault( x => x.GameObject.Id.ToString() == GameObject.Name );
	[Sync] public int KillStreak { get; set; } = 0;
	[Sync] public string ColorString { get; set; }
	Color Color => Color.Parse( ColorString ) ?? Color.White;

	// Private Member Variables
	private Angles visualDirection = Angles.Zero;
	private float headRoll = 0f;
	private TimeSince timeSinceLastFire = 0;
	private TimeSince timeSinceLastBounce = 0;
	private TimeSince timeSinceLastDash = 0;
	private TimeSince timeSinceLastKill = 0;
	[Sync] private TimeSince timeSinceSpawned { get; set; } = 0;
	private SpriteRenderer bounceIndicator = null;

	protected override void OnAwake()
	{
		BodyRenderer = Body.Components.Get<SkinnedModelRenderer>();
	}

	public void Init( Client client )
	{
		ColorString = client.ColorString;
	}

	protected override void OnStart()
	{
		if ( Network.IsOwner )
		{
			var indicatorObj = GameManager.Instance.ReticlePrefab.Clone();
			indicatorObj.NetworkMode = NetworkMode.Never;
			if ( indicatorObj.Components.TryGet<SpriteRenderer>( out bounceIndicator ) )
			{
				bounceIndicator.Enabled = false;
			}
		}

		timeSinceSpawned = 0f;
	}

	protected override void OnDestroy()
	{
		if ( Network.IsOwner && bounceIndicator.IsValid() )
		{
			bounceIndicator.GameObject.Destroy();
		}
	}

	protected override void OnUpdate()
	{
		if ( Network.IsOwner )
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

		if ( !Network.IsProxy && timeSinceLastKill > 5f )
		{
			KillStreak = 0;
			timeSinceLastKill = 0;
		}
		if ( IsInvulnerable )
		{
			BodyRenderer.Tint = Color.WithAlpha( 0.2f );
		}
		else
		{
			BodyRenderer.Tint = Color;
		}
		BodyRenderer.RenderType = Network.IsOwner ? ModelRenderer.ShadowRenderType.ShadowsOnly : ModelRenderer.ShadowRenderType.On;

		if ( Transform.Position.z < -1000 )
		{
			Kill();
		}
	}

	public void BuildWishVelocity( Vector2 input, bool respectDirection = true )
	{
		WishVelocity = 0f;

		var _rot = Direction.ToRotation();
		if ( respectDirection )
		{
			WishVelocity += _rot.Forward * input.x;
			WishVelocity += _rot.Left * input.y;
		}
		else
		{
			WishVelocity = input;
		}
		WishVelocity = WishVelocity.WithZ( 0 );

		if ( !WishVelocity.IsNearZeroLength ) WishVelocity = WishVelocity.Normal;

		WishVelocity *= Speed;
	}

	void Move()
	{
		if ( CharacterController.IsOnGround )
		{
			// Apply friction/acceleration
			CharacterController.Velocity = CharacterController.Velocity.WithZ( 0 );
			CharacterController.Accelerate( WishVelocity );
			CharacterController.ApplyFriction( GroundControl );
		}
		else
		{
			// Apply air control/gravity
			CharacterController.Velocity += Gravity * Time.Delta * 0.5f;
			CharacterController.Accelerate( WishVelocity );
			CharacterController.ApplyFriction( AirControl );
		}

		CharacterController.Move();

		if ( CharacterController.IsOnGround )
		{
			CharacterController.Velocity = CharacterController.Velocity.WithZ( 0 );
		}
		else
		{
			CharacterController.Velocity += Gravity * Time.Delta * 0.5f;
		}
	}

	void UpdateCamera()
	{
		var _input = Input.AnalogLook * (InstagibPreferences.Settings.Sensitivity * (Input.Down( "Zoom" ) ? InstagibPreferences.Settings.ZoomedSensitivity : 1f));
		Direction.pitch += _input.pitch / 20f;
		Direction.yaw += _input.yaw / 20f;
		Direction.roll = 0f;
		Direction.pitch = Direction.pitch.Clamp( -89.9f, 89.9f );
		LookAngles = Direction;

		visualDirection = Direction;
		float _shake = 0;
		if ( !Input.Down( "Zoom" ) )
		{
			_shake = CharacterController.Velocity.Length / Speed;
			visualDirection.pitch += MathF.Sin( Time.Now * 10f ) * _shake;
			visualDirection.yaw += MathF.Sin( Time.Now * 9.5f ) * _shake;
		}

		headRoll = headRoll.LerpTo( (Input.AnalogMove.y - _input.yaw / 8f) * -1f, 1f - MathF.Pow( 0.5f, Time.Delta * 10f ) );
		visualDirection.roll = headRoll;

		if ( Scene.Camera is not null )
		{
			Scene.Camera.Transform.Position = Head.Transform.Position;
			Scene.Camera.Transform.Rotation = visualDirection;
			Scene.Camera.FieldOfView = Scene.Camera.FieldOfView.LerpTo( Input.Down( "Zoom" ) ? InstagibPreferences.Settings.ZoomedFieldOfView : InstagibPreferences.Settings.FieldOfView, 10 * Time.Delta );
		}
	}

	void UpdateBounceReticle()
	{
		if ( Network.IsProxy ) return;

		var dir = (Client?.IsBot ?? true) ? Direction : visualDirection;
		var tr = Scene.Trace.Ray( Head.Transform.Position, Head.Transform.Position + dir.Forward * BounceRange )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger" )
			.Run();

		CanBounce = tr.Hit;

		if ( Network.IsOwner && bounceIndicator.IsValid() )
		{
			bounceIndicator.Enabled = tr.Hit;
			if ( tr.Hit )
			{
				bounceIndicator.Color = Color;
				bounceIndicator.Transform.Position = tr.HitPosition - visualDirection.Forward * 20f;
			}
		}
	}

	public void Jump()
	{
		if ( !CharacterController.IsOnGround ) return;

		CharacterController.Punch( Vector3.Up * JumpForce );
		BroadcastJump();
	}

	public void PrimaryFire()
	{
		if ( !CanFire ) return;

		var dir = (Client?.IsBot ?? true) ? Direction : visualDirection;
		var tr = Scene.Trace.Ray( Head.Transform.Position, Head.Transform.Position + dir.Forward * 5000f )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger" )
			.Run();
		var endPos = tr.Hit ? tr.HitPosition : tr.EndPosition;
		var distance = tr.StartPosition.Distance( endPos );

		if ( Network.IsOwner )
		{
			InstagibPreferences.Stats.TotalShotsFired++;
		}

		if ( tr.Hit && tr.GameObject is not null && tr.GameObject.Components.TryGet<Player>( out var hitPlayer ) )
		{
			if ( hitPlayer.GameObject.Id != GameObject.Id && !hitPlayer.IsInvulnerable )
			{
				hitPlayer?.Kill( Client.GameObject.Id );
			}
		}

		Client.Shots++;
		CharacterController.Punch( dir.Forward * -(400f * (1f - (distance / 5000f))) );

		BroadcastBeam( tr.StartPosition + Vector3.Down * 15f, endPos );
		timeSinceLastFire = 0;
		timeSinceSpawned = 100f;
	}

	public void SecondaryFire()
	{
		if ( !CanBounce ) return;
		if ( timeSinceLastBounce < BounceTimer ) return;

		var dir = Client.IsBot ? Direction : visualDirection;
		var tr = Scene.Trace.Ray( Head.Transform.Position, Head.Transform.Position + dir.Forward * BounceRange )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "trigger" )
			.Run();

		if ( tr.Hit )
		{
			var force = -dir.Forward * 500f;
			force = force.WithZ( force.z * 1.5f );
			CharacterController.Punch( force );
			timeSinceLastBounce = 0;
			BroadcastBounce( tr.HitPosition, -dir.Forward );

			if ( Network.IsOwner )
			{
				InstagibPreferences.Stats.TotalBounces++;
			}
		}
	}

	[Broadcast]
	public void Kill( Guid killer = default )
	{
		var killerClient = Scene.GetAllComponents<Client>().FirstOrDefault( x => x.GameObject.Id == killer );
		if ( killerClient.IsValid() && GameManager.Instance.IsTeamGamemode && killerClient.Team == Client.Team )
		{
			return;
		}

		Sound.Play( "snd-flesh-explode", Transform.Position );
		GameManager.Instance.SpawnGibs( Transform.Position + Vector3.Up * 32f, Color );
		if ( Network.IsOwner )
		{
			InstagibPreferences.Stats.TotalDeaths++;
			InstagibPreferences.Save();
			Sandbox.Services.Stats.Increment( "deaths", 1 );
		}
		if ( !Network.IsProxy )
		{
			if ( killer != Guid.Empty )
			{
				if ( killerClient is not null )
				{
					var killMsg = (killer == Guid.Empty) ? $"{Client.GameObject.Name} was killed" : $"{Client.Name} was killed by {killerClient.Name}";
					Chatbox.Instance.AddMessage( "☠️", killMsg, "kill-feed" );
				}
			}
			Client.TimeSinceLastDeath = 0;
			GameObject.Destroy();

			var player = Scene.GetAllComponents<Player>().FirstOrDefault( x => x.GameObject.Name == killer.ToString() );
			if ( player is not null )
			{
				player.OnKill();
			}
		}

		if ( Networking.IsHost )
		{
			Client.Deaths++;
			if ( killerClient.IsValid() && GameManager.Instance.IsTeamGamemode )
			{
				GameManager.Instance.AddTeamPoints( killerClient.Team, 1 );
			}
		}
	}

	[Broadcast]
	public void OnKill()
	{
		timeSinceLastKill = 0;
		if ( !Network.IsProxy )
		{
			KillStreak++;
			if ( KillStreak > 1 )
			{
				Client.SetKillstreak( KillStreak );
			}
			Sandbox.Services.Stats.Increment( "kills", 1 );
		}
		if ( Network.IsOwner )
		{
			var sound = Sound.Play( "snd-kill" );
			sound.Pitch = 1f + ((KillStreak - 1) * (1f / 12f));
			InstagibPreferences.Stats.TotalKills++;
			if ( KillStreak > 1 )
			{
				InstagibHud.Instance.SetKillstreak( KillStreak );
				if ( !InstagibPreferences.Stats.Killstreaks.ContainsKey( KillStreak ) )
				{
					InstagibPreferences.Stats.Killstreaks.Add( KillStreak, 1 );
				}
				else
				{
					InstagibPreferences.Stats.Killstreaks[KillStreak]++;
				}
			}
			InstagibPreferences.Save();
		}
		if ( Networking.IsHost )
		{
			Client.Kills++;
		}
	}

	void RotateBody()
	{
		if ( Body is null ) return;

		var targetAngle = new Angles( 0, LookAngles.yaw, 0 );
		float rotateDifference = Body.Transform.Rotation.Distance( targetAngle );

		if ( rotateDifference > 40f || CharacterController.Velocity.Length > 10f )
		{
			Body.Transform.Rotation = Angles.Lerp( Body.Transform.Rotation, targetAngle, Time.Delta * 10f );
		}
	}

	void UpdateAnimations()
	{
		AnimationHelper.IsGrounded = CharacterController.IsOnGround;
		AnimationHelper.WithVelocity( CharacterController.Velocity );
		AnimationHelper.WithWishVelocity( WishVelocity );
		AnimationHelper.WithLook( LookAngles.Forward );
	}

	void UpdateShadow()
	{
		var floorTr = Scene.Trace.Ray( Transform.Position, Transform.Position + Vector3.Down * 5000f )
			.WithoutTags( "trigger" )
			.Run();

		Shadow.Transform.Position = floorTr.Hit ? (floorTr.HitPosition + Vector3.Up) : floorTr.EndPosition;
		Shadow.Transform.Rotation = Rotation.LookAt( floorTr.Normal ) * Rotation.From( new Angles( 0, 90, 90 ) );
	}

	[Broadcast]
	void BroadcastJump()
	{
		AnimationHelper?.TriggerJump();
	}

	[Broadcast]
	void BroadcastBeam( Vector3 startPos, Vector3 endPos )
	{
		var midpos = startPos + (endPos - startPos) / 2f;
		var beamObj = GameManager.Instance.BeamPrefab.Clone( midpos );
		beamObj.NetworkMode = NetworkMode.Never;
		if ( beamObj.Components.TryGet<Beam>( out var beam ) )
		{
			beam.StartPosition = startPos;
			beam.EndPosition = endPos;
			beam.Color = Color;
		}
		if ( beamObj.Components.TryGet<ModelRenderer>( out var modelRenderer ) )
		{
			modelRenderer.Tint = Color;
		}

		var particleTransform = new Transform( endPos, Rotation.LookAt( startPos - endPos ) * Rotation.From( new Angles( 0, 0, 90 ) ), Vector3.One );
		var particle = GameManager.Instance.LaserDustParticle.Clone( particleTransform );
		particle.NetworkMode = NetworkMode.Never;

		var decalRot = Rotation.LookAt( startPos - endPos, Vector3.Random ) * Rotation.From( new Angles( 180, 0, 0 ) );
		var decalTransform = new Transform( endPos + decalRot.Backward * 4f, decalRot, Vector3.One );
		var decalObj = GameManager.Instance.BeamDecal.Clone( decalTransform );
		decalObj.NetworkMode = NetworkMode.Never;
		var decal = decalObj.Components.Get<DecalRenderer>();
		decal.TintColor = Color;

		Sound.Play( "snd-fire", startPos );
	}

	[Broadcast]
	void BroadcastBounce( Vector3 position, Vector3 normal )
	{
		Sound.Play( "snd-bounce", position );

		var transform = new Transform( position, Rotation.LookAt( normal ) * Rotation.From( new Angles( 0, 0, 90 ) ), Vector3.One );
		var particleObj = GameManager.Instance.BounceParticle.Clone( transform );
		particleObj.NetworkMode = NetworkMode.Never;

		var particle = particleObj.Components.Get<ParticleEffect>();
		particle.ApplyColor = true;
		particle.Tint = Color;

		var decalRot = Rotation.LookAt( normal, Vector3.Random ) * Rotation.From( new Angles( 180, 0, 0 ) );
		var decalTransform = new Transform( position + decalRot.Backward * 4f, decalRot, Vector3.One );
		var decalObj = GameManager.Instance.BounceDecal.Clone( decalTransform );
		decalObj.NetworkMode = NetworkMode.Never;
		var decal = decalObj.Components.Get<DecalRenderer>();
		decal.TintColor = Color;
		decal.Size = decal.Size * 3f;
	}
}
