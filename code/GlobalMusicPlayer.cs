using System;
using Sandbox;
using Sandbox.Audio;

namespace Instagib;

public static class GlobalMusicPlayer
{
    public static Song CurrentSong = null;
    static List<Song> Queue = new List<Song>();
    static List<Song> Played = new List<Song>();

    static TimeSince TimeSinceLastSong = 0;
    static Mixer MusicMixer = null;
    static SoundHandle LastHandle = null;

    public static bool IsPaused
    {
        get => _isPaused;
        set
        {
            _isPaused = value;
            if (LastHandle.IsValid())
            {
                if (value)
                {
                    LastHandle.Pitch = 0;
                    LastHandle.Volume = 0;
                }
                else if (!value)
                {
                    LastHandle.Pitch = 1;
                    LastHandle.Volume = 1;
                }
            }
        }
    }
    static bool _isPaused = false;

    public static float CurrentSongProgress => (float)(TimeSinceLastSong) / (CurrentSong?.Length ?? 100f);

    public static void Start()
    {
        MusicMixer ??= Mixer.FindMixerByName("Music");

        Queue = new();
        Played = new();

        var songs = ResourceLibrary.GetAll<Song>().OrderBy(x => Random.Shared.Float());
        foreach (var song in songs)
        {
            Queue.Add(song);
        }

        Sound.StopAll(1f);
        PlayNext();
    }

    public static void PlayNext()
    {
        MusicMixer ??= Mixer.FindMixerByName("Music");

        if (Queue.Count == 0)
        {
            Queue = Played.OrderBy(x => Random.Shared.Float()).ToList();
            Played.Clear();
        }

        if (Queue.Count == 0)
        {
            return;
        }

        var song = Queue[0];
        Queue.RemoveAt(0);
        Played.Add(song);
        CurrentSong = song;

        LastHandle?.Stop(1f);
        var sound = Sound.Play(song.Sound);
        sound.TargetMixer = MusicMixer;
        LastHandle = sound;

        TimeSinceLastSong = 0f;
        IsPaused = false;
    }

    public static void CheckForNextSong()
    {
        if (IsPaused)
        {
            TimeSinceLastSong -= Time.Delta;
        }
        if (TimeSinceLastSong < 2f) return;

        MusicMixer ??= Mixer.FindMixerByName("Music");

        if (CurrentSongProgress > 1f || CurrentSong is null)
        {
            PlayNext();
        }
        else if (CurrentSong == null || (Queue.Count == 0 && Played.Count == 0))
        {
            Start();
        }
    }

}