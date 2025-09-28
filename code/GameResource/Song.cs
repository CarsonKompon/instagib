namespace Instagib;

[AssetType( Name = "Instagib Song", Extension = "song", Category = "Instagib" )]
public class Song : GameResource
{
	public string Name { get; set; } = "New Song";
	public SoundEvent Sound { get; set; }
	public float Length { get; set; } = 100f;

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "music_note", width, height );
	}
}
