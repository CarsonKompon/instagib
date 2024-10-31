using System;
using Sandbox;

namespace Instagib;

public sealed class PlayerDresser : Component, Component.INetworkSpawn
{
	[Property]
	bool IsNetworked { get; set; } = true;
	[Property]
	public SkinnedModelRenderer BodyRenderer { get; set; }

	public Color Color { get; set; }

	protected override void OnStart()
	{
		if ( IsNetworked && Network.OwnerId == Guid.Empty )
		{
			IsNetworked = false;
			Enabled = false;
			return;
		}

		if ( IsNetworked ) return;

		ApplyClothing( Connection.Local );
	}

	public void OnNetworkSpawn( Connection owner )
	{
		if ( !IsNetworked ) return;
		if ( owner is null || !owner.IsActive ) return;

		ApplyClothing( owner );
	}

	void ApplyClothing( Connection owner )
	{
		var material = BodyRenderer.MaterialOverride;
		Color = new ColorHsv( Random.Shared.Float( 0, 360 ), 0.8f, 1 ).ToColor();

		if ( IsNetworked )
		{
			var client = Scene.GetAllComponents<Client>().FirstOrDefault( x => x.Network.OwnerId == owner.Id );
			if ( client.IsValid() )
			{
				Color = Color.Parse( client.ColorString ) ?? Color;
			}
		}

		var clothing = new ClothingContainer();
		clothing.Deserialize( owner.GetUserData( "avatar" ) );
		clothing.Apply( BodyRenderer );

		foreach ( var renderer in BodyRenderer.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		{
			renderer.ClearMaterialOverrides();
			renderer.MaterialOverride = material;
			renderer.Tint = Color;
		}
	}
}