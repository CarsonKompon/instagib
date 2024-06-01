using System;
using Sandbox;

namespace Instagib;

[GameResource( "Instagib Song", "song", "Describes a song that can be played in the game.", Icon = "music_note" )]
public class Song : GameResource
{
    public string Name { get; set; } = "New Song";
    public SoundEvent Sound { get; set; }
    public float Length { get; set; } = 100f;
}