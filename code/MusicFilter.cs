using Sandbox.Audio;
using System;

namespace Instagib;

public class MusicFilter : AudioProcessor
{
	[Property, Range( 0, 1 )] float Amount { get; set; } = 0f;

	private PerChannel<float> previousSample = new PerChannel<float>();

	protected override void ProcessSingleChannel( AudioChannel c, Span<float> input )
	{
		float num = this.Amount.Remap( 0.0f, 1f, 1f, 0.0f, true );
		for ( int index = 0; index < input.Length; ++index )
		{
			input[index] = (float)(num * input[index] + (1f - Amount) * previousSample.Get( c ));
			previousSample.Set( c, input[index] );
		}
	}
}
