using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;

namespace PoliceChaser.Audio
{
    internal enum Waveform
    {
        Pulse,
        Triangle,
        Saw,
        Noise
    }

    internal struct Note
    {
        public int Midi;      // -1 = rest, 36/38/42 = drums
        public float Beats;
    }

    internal sealed class Voice
    {
        public Waveform Wave;
        public float Gain;
        public float Duty = 0.5f;
        public List<Note> Notes = new List<Note>();
    }

    internal sealed class Song
    {
        public float Bpm;
        public List<Voice> Voices = new List<Voice>();
    }

    /// <summary>
    /// A small software chip synth. Everything the game plays - the title theme, the chase
    /// music, the game over and high score themes, the engine, siren and effects - is generated
    /// as PCM at runtime, so no audio files (and no copyrighted music) are shipped.
    /// </summary>
    public sealed class ChipAudio : IDisposable
    {
        public const int SampleRate = 22050;

        private readonly Dictionary<string, SoundEffect> _music = new Dictionary<string, SoundEffect>();
        private readonly List<SoundEffect> _owned = new List<SoundEffect>();

        private SoundEffectInstance _engineInstance;
        private SoundEffectInstance _sirenInstance;
        private SoundEffectInstance _musicInstance;
        private string _currentMusic;

        public SoundEffect Engine { get; }
        public SoundEffect Siren { get; }
        public SoundEffect Skid { get; }
        public SoundEffect Crash { get; }
        public SoundEffect Screech { get; }
        public SoundEffect Squelch { get; }
        public SoundEffect Gunshot { get; }
        public SoundEffect Ram { get; }
        public SoundEffect Beep { get; }
        public SoundEffect Select { get; }
        public SoundEffect Checkpoint { get; }
        public SoundEffect Busted { get; }
        public SoundEffect Turbo { get; }
        public SoundEffect Wrong { get; }
        public SoundEffect Countdown { get; }
        public SoundEffect SirenShort { get; }

        public const string TrackTitle = "title";
        public const string TrackRace = "race";
        public const string TrackGameOver = "gameover";
        public const string TrackHighScore = "hiscore";
        public const string TrackBusted = "busted";

        /// <summary>
        /// Peak-linear gains for every sound that can overlap. A traffic hit layers crash,
        /// screech and squelch, so those three are budgeted to stay under 1.0 together while
        /// the music, engine and siren sit underneath the effects.
        /// </summary>
        public static class Mix
        {
            public const float Music = 0.45f;
            public const float EngineMin = 0.12f;
            public const float EngineMax = 0.28f;
            public const float Siren = 0.12f;
            public const float Crash = 0.50f;
            public const float Screech = 0.28f;
            public const float Squelch = 0.16f;
            public const float Gunshot = 0.45f;
            public const float Ram = 0.60f;
            public const float Skid = 0.40f;
            public const float Checkpoint = 0.40f;
            public const float Warning = 0.40f;
        }

        public float MusicVolume { get; set; } = Mix.Music;

        public ChipAudio()
        {
            Engine = Make(EngineLoop(), 0.5f);
            Siren = Make(SirenLoop(), 0.28f);
            SirenShort = Make(SirenLoop(), 0.5f);
            Skid = Make(SkidSound(), 0.7f);
            Crash = Make(CrashSound(), 1f);
            Screech = Make(ScreechSound(), 1f);
            Squelch = Make(SquelchSound(), 1f);
            Gunshot = Make(GunshotSound(), 1f);
            Ram = Make(RamSound(), 0.95f);
            Beep = Make(SimpleTone(0.07f, 940f, 940f, Waveform.Pulse, 0.35f, 0.5f), 0.55f);
            Select = Make(SimpleTone(0.05f, 1400f, 1600f, Waveform.Pulse, 0.2f, 0.5f), 0.5f);
            Checkpoint = Make(Arpeggio(new[] { 72, 76, 79, 84 }, 0.075f), 0.6f);
            Busted = Make(BustedSting(), 0.8f);
            Turbo = Make(TurboSound(), 0.75f);
            Wrong = Make(SimpleTone(0.16f, 150f, 90f, Waveform.Saw, 0.4f, 0.8f), 0.6f);
            Countdown = Make(SimpleTone(0.12f, 660f, 660f, Waveform.Pulse, 0.5f, 0.6f), 0.6f);
        }

        // ------------------------------------------------------------------ playback

        public void StartRaceEngine()
        {
            if (Engine == null || Siren == null)
            {
                return;
            }

            if (_engineInstance == null)
            {
                _engineInstance = Engine.CreateInstance();
                _engineInstance.IsLooped = true;
                _engineInstance.Volume = 0.0f;
            }

            if (_engineInstance.State != SoundState.Playing)
            {
                _engineInstance.Play();
            }

            if (_sirenInstance == null)
            {
                _sirenInstance = Siren.CreateInstance();
                _sirenInstance.IsLooped = true;
                _sirenInstance.Volume = 0.0f;
            }

            if (_sirenInstance.State != SoundState.Playing)
            {
                _sirenInstance.Play();
            }
        }

        public void UpdateRaceAudio(float speedRatio, bool offRoad, bool paused, bool turbo)
        {
            if (_engineInstance != null)
            {
                float target = paused ? 0f
                    : Mix.EngineMin + (Mix.EngineMax - Mix.EngineMin) * Math.Min(1f, speedRatio);
                _engineInstance.Volume = Lerp(_engineInstance.Volume, target, 0.15f);
                _engineInstance.Pitch = Math.Clamp(-0.45f + speedRatio * 0.7f + (turbo ? 0.12f : 0f), -0.6f, 0.85f);
            }

            if (_sirenInstance != null)
            {
                float target = paused ? 0f : Mix.Siren;
                _sirenInstance.Volume = Lerp(_sirenInstance.Volume, target, 0.05f);
            }
        }

        public void StopRaceAudio()
        {
            _engineInstance?.Stop();
            _sirenInstance?.Stop();
        }

        public void PauseRaceAudio(bool paused)
        {
            if (paused)
            {
                _engineInstance?.Pause();
                _sirenInstance?.Pause();
            }
            else
            {
                if (_engineInstance?.State == SoundState.Paused)
                {
                    _engineInstance.Resume();
                }

                if (_sirenInstance?.State == SoundState.Paused)
                {
                    _sirenInstance.Resume();
                }
            }
        }

        public void PlayMusic(string name, bool loop = true)
        {
            if (_currentMusic == name && _musicInstance != null)
            {
                return;
            }

            StopMusic();

            SoundEffect effect = GetMusic(name);
            if (effect == null)
            {
                return;
            }

            _musicInstance = effect.CreateInstance();
            _musicInstance.IsLooped = loop;
            _musicInstance.Volume = MusicVolume;
            _musicInstance.Play();
            _currentMusic = name;
        }

        public void StopMusic()
        {
            if (_musicInstance != null)
            {
                try
                {
                    _musicInstance.Stop();
                }
                catch (Exception)
                {
                    // The instance can be disposed by the platform audio thread; ignore.
                }

                _musicInstance.Dispose();
                _musicInstance = null;
            }

            _currentMusic = null;
        }

        public void Play(SoundEffect effect, float volume = 1f, float pitch = 0f)
        {
            if (effect == null)
            {
                return;
            }

            try
            {
                effect.Play(volume, pitch, 0f);
            }
            catch (Exception)
            {
                // Audio may not be available yet on some platforms; never crash the game.
            }
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * Math.Max(0f, Math.Min(1f, t));

        // ------------------------------------------------------------------ music

        private SoundEffect GetMusic(string name)
        {
            if (_music.TryGetValue(name, out SoundEffect cached))
            {
                return cached;
            }

            Song song = BuildSong(name);
            if (song == null)
            {
                return null;
            }

            byte[] pcm = Render(song, name != TrackBusted);

            try
            {
                var effect = new SoundEffect(pcm, SampleRate, AudioChannels.Mono);
                _music[name] = effect;
                _owned.Add(effect);
                return effect;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static Song BuildSong(string name)
        {
            switch (name)
            {
                case TrackTitle:
                    return TitleTheme();
                case TrackRace:
                    return RaceTheme();
                case TrackGameOver:
                    return GameOverTheme();
                case TrackHighScore:
                    return HighScoreTheme();
                case TrackBusted:
                    return BustedTheme();
                default:
                    return null;
            }
        }

        private static Song TitleTheme()
        {
            var song = new Song { Bpm = 138f };

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Pulse,
                Gain = 0.30f,
                Duty = 0.25f,
                Notes = Seq("A4:1 C5:1 E5:1 A5:1 G5:1 E5:1 C5:1 E5:1 F5:1 E5:1 D5:1 C5:1 B4:2 A4:1 R:1")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Triangle,
                Gain = 0.34f,
                Notes = Seq("A2:2 A2:2 F2:2 F2:2 C3:2 C3:2 G2:2 G2:2")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Pulse,
                Gain = 0.11f,
                Duty = 0.125f,
                Notes = Seq("A3:0.5 E4:0.5 A4:0.5 E4:0.5 A3:0.5 E4:0.5 A4:0.5 E4:0.5 " +
                             "F3:0.5 C4:0.5 F4:0.5 C4:0.5 F3:0.5 C4:0.5 F4:0.5 C4:0.5 " +
                             "C3:0.5 G3:0.5 C4:0.5 G3:0.5 C3:0.5 G3:0.5 C4:0.5 G3:0.5 " +
                             "G3:0.5 D4:0.5 G4:0.5 D4:0.5 G3:0.5 D4:0.5 G4:0.5 D4:0.5")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Noise,
                Gain = 0.16f,
                Notes = Seq("K:1 H:0.5 S:0.5 H:0.5 K:0.5 H:1 K:1 H:0.5 S:0.5 H:0.5 K:0.5 H:1 " +
                             "K:1 H:0.5 S:0.5 H:0.5 K:0.5 H:1 K:1 H:0.5 S:0.5 H:0.5 K:0.5 H:1")
            });

            return song;
        }

        private static Song RaceTheme()
        {
            var song = new Song { Bpm = 158f };

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Pulse,
                Gain = 0.20f,
                Duty = 0.3f,
                Notes = Seq("E5:0.5 R:0.5 E5:0.5 G5:0.5 E5:0.5 D5:0.5 B4:1 " +
                             "E5:0.5 R:0.5 E5:0.5 A5:0.5 G5:0.5 E5:0.5 D5:1 " +
                             "C5:0.5 R:0.5 C5:0.5 E5:0.5 G5:0.5 A5:0.5 G5:1 " +
                             "F5:0.5 E5:0.5 D5:0.5 B4:0.5 E5:1 R:1")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Triangle,
                Gain = 0.32f,
                Notes = Seq("E2:0.5 E2:0.5 E3:0.5 E2:0.5 E2:0.5 E2:0.5 E3:0.5 E2:0.5 " +
                             "A2:0.5 A2:0.5 A3:0.5 A2:0.5 A2:0.5 A2:0.5 A3:0.5 A2:0.5 " +
                             "C3:0.5 C3:0.5 C4:0.5 C3:0.5 C3:0.5 C3:0.5 C4:0.5 C3:0.5 " +
                             "B2:0.5 B2:0.5 B3:0.5 B2:0.5 B2:0.5 B2:0.5 B3:0.5 B2:0.5")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Pulse,
                Gain = 0.08f,
                Duty = 0.125f,
                Notes = Seq("B4:0.25 E5:0.25 B4:0.25 E5:0.25 B4:0.25 E5:0.25 B4:0.25 E5:0.25 " +
                             "A4:0.25 C5:0.25 A4:0.25 C5:0.25 A4:0.25 C5:0.25 A4:0.25 C5:0.25 " +
                             "G4:0.25 C5:0.25 G4:0.25 C5:0.25 G4:0.25 C5:0.25 G4:0.25 C5:0.25 " +
                             "F4:0.25 B4:0.25 F4:0.25 B4:0.25 F4:0.25 B4:0.25 F4:0.25 B4:0.25")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Noise,
                Gain = 0.17f,
                Notes = Seq("K:0.25 H:0.25 S:0.25 H:0.25 K:0.25 K:0.25 S:0.25 H:0.25 " +
                             "K:0.25 H:0.25 S:0.25 H:0.25 K:0.25 H:0.25 S:0.25 H:0.25 " +
                             "K:0.25 H:0.25 S:0.25 H:0.25 K:0.25 K:0.25 S:0.25 H:0.25 " +
                             "K:0.25 H:0.25 S:0.25 H:0.25 K:0.25 S:0.25 S:0.25 H:0.25")
            });

            return song;
        }

        private static Song GameOverTheme()
        {
            var song = new Song { Bpm = 92f };

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Pulse,
                Gain = 0.30f,
                Duty = 0.5f,
                Notes = Seq("E4:1 D4:0.5 C4:1.5 B3:1 A3:1.5 R:1.5 " +
                             "G3:1 F3:0.5 E3:1.5 D3:1 C3:2 R:2")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Triangle,
                Gain = 0.32f,
                Notes = Seq("A2:4 F2:2 E2:2 D2:2 G2:2 C2:4")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Noise,
                Gain = 0.10f,
                Notes = Seq("H:1 H:1 H:1 H:1 K:1 H:1 S:1 H:1")
            });

            return song;
        }

        private static Song HighScoreTheme()
        {
            var song = new Song { Bpm = 146f };

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Pulse,
                Gain = 0.30f,
                Duty = 0.25f,
                Notes = Seq("C5:0.5 E5:0.5 G5:0.5 C6:1 G5:0.5 E5:0.5 G5:1 " +
                             "A5:0.5 G5:0.5 F5:0.5 E5:0.5 D5:1 E5:1 " +
                             "F5:0.5 A5:0.5 C6:0.5 F6:1 C6:0.5 A5:0.5 C6:1 " +
                             "G5:0.5 F5:0.5 E5:0.5 D5:0.5 C5:2 R:1")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Triangle,
                Gain = 0.32f,
                Notes = Seq("C3:2 G2:2 A2:2 F2:2 F2:2 C3:2 G2:2 C3:2")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Pulse,
                Gain = 0.10f,
                Duty = 0.125f,
                Notes = Seq("C4:0.25 G4:0.25 C5:0.25 G4:0.25 C4:0.25 G4:0.25 C5:0.25 G4:0.25 " +
                             "A3:0.25 E4:0.25 A4:0.25 E4:0.25 A3:0.25 E4:0.25 A4:0.25 E4:0.25 " +
                             "F3:0.25 C4:0.25 F4:0.25 C4:0.25 F3:0.25 C4:0.25 F4:0.25 C4:0.25 " +
                             "G3:0.25 D4:0.25 G4:0.25 D4:0.25 G3:0.25 D4:0.25 G4:0.25 D4:0.25")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Noise,
                Gain = 0.15f,
                Notes = Seq("K:0.5 H:0.5 S:0.5 H:0.5 K:0.5 H:0.5 S:0.5 H:0.5 " +
                             "K:0.5 H:0.5 S:0.5 H:0.5 K:0.5 H:0.5 S:0.5 H:0.5 " +
                             "K:0.5 H:0.5 S:0.5 H:0.5 K:0.5 H:0.5 S:0.5 H:0.5 " +
                             "K:0.5 H:0.5 S:0.5 H:0.5 K:0.5 S:0.5 S:0.5 H:0.5")
            });

            return song;
        }

        private static Song BustedTheme()
        {
            var song = new Song { Bpm = 150f };

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Pulse,
                Gain = 0.30f,
                Duty = 0.35f,
                Notes = Seq("C5:0.25 E5:0.25 G5:0.25 C6:1 R:0.5")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Triangle,
                Gain = 0.34f,
                Notes = Seq("C3:0.5 G3:0.5 C4:1")
            });

            song.Voices.Add(new Voice
            {
                Wave = Waveform.Noise,
                Gain = 0.18f,
                Notes = Seq("S:0.25 S:0.25 K:1")
            });

            return song;
        }

        private static List<Note> Seq(string spec)
        {
            var notes = new List<Note>();
            string[] tokens = spec.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < tokens.Length; i++)
            {
                string token = tokens[i];
                int colon = token.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                string name = token.Substring(0, colon);
                float beats = 1f;
                float.TryParse(token.Substring(colon + 1), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out beats);

                if (name == "R")
                {
                    notes.Add(new Note { Midi = -1, Beats = beats });
                }
                else if (name == "K")
                {
                    notes.Add(new Note { Midi = 36, Beats = beats });
                }
                else if (name == "S")
                {
                    notes.Add(new Note { Midi = 38, Beats = beats });
                }
                else if (name == "H")
                {
                    notes.Add(new Note { Midi = 42, Beats = beats });
                }
                else
                {
                    notes.Add(new Note { Midi = ParseMidi(name), Beats = beats });
                }
            }

            return notes;
        }

        private static int ParseMidi(string name)
        {
            int semitone;
            switch (char.ToUpperInvariant(name[0]))
            {
                case 'C': semitone = 0; break;
                case 'D': semitone = 2; break;
                case 'E': semitone = 4; break;
                case 'F': semitone = 5; break;
                case 'G': semitone = 7; break;
                case 'A': semitone = 9; break;
                case 'B': semitone = 11; break;
                default: return 60;
            }

            int index = 1;
            if (index < name.Length && name[index] == '#')
            {
                semitone++;
                index++;
            }
            else if (index < name.Length && name[index] == 'b')
            {
                semitone--;
                index++;
            }

            int octave = 4;
            if (index < name.Length && char.IsDigit(name[index]))
            {
                octave = name[index] - '0';
            }

            return (octave + 1) * 12 + semitone;
        }

        private static byte[] Render(Song song, bool loop)
        {
            double secondsPerBeat = 60.0 / song.Bpm;
            double beats = 0;
            for (int v = 0; v < song.Voices.Count; v++)
            {
                double total = 0;
                for (int n = 0; n < song.Voices[v].Notes.Count; n++)
                {
                    total += song.Voices[v].Notes[n].Beats;
                }

                beats = Math.Max(beats, total);
            }

            int samples = (int)(beats * secondsPerBeat * SampleRate) + SampleRate / 20;
            var mix = new float[samples];

            for (int v = 0; v < song.Voices.Count; v++)
            {
                Voice voice = song.Voices[v];
                double time = 0;
                uint noiseState = (uint)(0x1234567 + v * 7919);

                for (int n = 0; n < voice.Notes.Count; n++)
                {
                    Note note = voice.Notes[n];
                    double duration = note.Beats * secondsPerBeat;
                    int start = (int)(time * SampleRate);
                    int length = (int)(duration * SampleRate);

                    if (note.Midi >= 36 && note.Midi <= 46)
                    {
                        AddDrum(mix, start, length, note.Midi, voice.Gain, ref noiseState);
                    }
                    else if (note.Midi > 0)
                    {
                        double freq = 440.0 * Math.Pow(2.0, (note.Midi - 69) / 12.0);
                        AddTone(mix, start, length, freq, voice.Wave, voice.Gain, voice.Duty, ref noiseState);
                    }

                    time += duration;
                }
            }

            // Optional short crossfade so looping tracks join without a click.
            int fade = loop ? SampleRate / 180 : 0;
            if (fade > 0 && samples > fade * 3)
            {
                for (int i = 0; i < fade; i++)
                {
                    float t = i / (float)fade;
                    mix[i] *= t;
                    mix[samples - fade + i] *= 1f - t;
                }
            }

            var pcm = new byte[samples * 2];
            for (int i = 0; i < samples; i++)
            {
                float value = mix[i];
                value = value / (1f + MathF.Abs(value)); // soft clip
                short sample = (short)(Math.Clamp(value * 26000f, -32767f, 32767f));
                pcm[i * 2] = (byte)(sample & 0xFF);
                pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return pcm;
        }

        private static void AddTone(float[] mix, int start, int length, double freq, Waveform wave, float gain, float duty, ref uint noiseState)
        {
            if (start < 0 || start >= mix.Length || length <= 0)
            {
                return;
            }

            int attack = Math.Min(length / 4, SampleRate / 200);
            int release = Math.Min(length / 2, SampleRate / 22);
            double phase = 0;
            double step = freq / SampleRate;
            float filtered = 0f;

            int end = Math.Min(mix.Length, start + length);
            for (int i = start; i < end; i++)
            {
                double p = phase - Math.Floor(phase);
                float sample;

                switch (wave)
                {
                    case Waveform.Triangle:
                        sample = (float)(4.0 * Math.Abs(p - 0.5) - 1.0);
                        break;
                    case Waveform.Saw:
                        sample = (float)(2.0 * p - 1.0);
                        break;
                    case Waveform.Noise:
                        sample = NextNoise(ref noiseState);
                        break;
                    default:
                        sample = p < duty ? 1f : -1f;
                        break;
                }

                float env = 1f;
                int local = i - start;
                if (local < attack)
                {
                    env = local / (float)attack;
                }
                else if (local > length - release && release > 0)
                {
                    env = Math.Max(0f, (length - local) / (float)release);
                }

                env *= 0.75f + 0.25f * (1f - local / (float)length);

                // Slight low-pass to take the edge off the aliasing.
                filtered = filtered * 0.55f + sample * 0.45f;
                mix[i] += filtered * env * gain;
                phase += step;
            }
        }

        private static void AddDrum(float[] mix, int start, int length, int midi, float gain, ref uint noiseState)
        {
            if (start < 0 || start >= mix.Length || length <= 0)
            {
                return;
            }

            int end = Math.Min(mix.Length, start + length);

            if (midi == 36)
            {
                // Kick: pitch-swept sine.
                double phase = 0;
                for (int i = start; i < end; i++)
                {
                    double local = (i - start) / (double)SampleRate;
                    double freq = 130 * Math.Exp(-local * 22) + 42;
                    phase += freq / SampleRate;
                    float env = (float)Math.Exp(-local * 14);
                    mix[i] += (float)Math.Sin(phase * Math.PI * 2) * env * gain * 1.5f;
                }
            }
            else if (midi == 38)
            {
                for (int i = start; i < end; i++)
                {
                    double local = (i - start) / (double)SampleRate;
                    float env = (float)Math.Exp(-local * 26);
                    mix[i] += NextNoise(ref noiseState) * env * gain * 1.2f;
                }
            }
            else
            {
                for (int i = start; i < end; i++)
                {
                    double local = (i - start) / (double)SampleRate;
                    float env = (float)Math.Exp(-local * 70);
                    mix[i] += NextNoise(ref noiseState) * env * gain * 0.55f;
                }
            }
        }

        private static float NextNoise(ref uint state)
        {
            uint bit = ((state >> 0) ^ (state >> 2)) & 1u;
            state = (state >> 1) | (bit << 30);
            return ((state & 0xFFFF) / 32768f) - 1f;
        }

        // ------------------------------------------------------------------ effects

        private static float[] EngineLoop()
        {
            int samples = SampleRate / 2;
            var buffer = new float[samples];
            uint noise = 0x9E3779B9;

            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)SampleRate;
                double phase = t * 52.0 % 1.0;
                float saw = (float)(2.0 * phase - 1.0);
                float sub = (float)Math.Sin(t * 2 * Math.PI * 26);
                float rumble = NextNoise(ref noise) * 0.15f;
                float pulse = (t * 4 % 1.0) < 0.5f ? 0.12f : -0.12f;
                buffer[i] = saw * 0.34f + sub * 0.35f + rumble + pulse;
            }

            return buffer;
        }

        private static float[] SirenLoop()
        {
            int samples = SampleRate * 2;
            var buffer = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)SampleRate;
                double tone = t < 0.5 ? 700 : t < 1.0 ? 950 : t < 1.5 ? 700 : 950;
                double phase = (t * tone) % 1.0;
                float sample = phase < 0.5 ? 1f : -1f;
                float wobble = (float)(0.85 + 0.15 * Math.Sin(t * 2 * Math.PI * 6));
                buffer[i] = sample * wobble * 0.5f;
            }

            return buffer;
        }

        private static float[] SkidSound()
        {
            int samples = (int)(SampleRate * 0.7);
            var buffer = new float[samples];
            uint noise = 0x51ED270B;
            float filtered = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float n = NextNoise(ref noise);
                filtered = filtered * 0.72f + n * 0.28f;
                float env = MathF.Min(1f, t * 12f) * (1f - t);
                buffer[i] = filtered * env * 1.6f;
            }

            return buffer;
        }

        private static float[] CrashSound()
        {
            int samples = (int)(SampleRate * 0.55);
            var buffer = new float[samples];
            uint noise = 0x1234ABCD;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float n = NextNoise(ref noise);
                float low = (float)Math.Sin(i / (double)SampleRate * 2 * Math.PI * 90) * 0.6f;
                buffer[i] = (n * 0.9f + low) * MathF.Exp(-t * 7f);
            }

            return buffer;
        }

        private static float[] ScreechSound()
        {
            int samples = (int)(SampleRate * 0.5);
            var buffer = new float[samples];
            uint noise = 0x0BADF00D;
            float filtered = 0f;
            double phase = 0;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float n = NextNoise(ref noise);
                filtered = filtered * 0.6f + n * 0.4f;
                double freq = 2100 - 900 * t;
                phase += freq / SampleRate;
                float squeal = (float)Math.Sin(phase) * 0.35f;
                float env = MathF.Min(1f, t * 18f) * (1f - t) * (1f - t);
                buffer[i] = (filtered * 1.3f + squeal) * env;
            }

            return buffer;
        }

        private static float[] SquelchSound()
        {
            int samples = (int)(SampleRate * 0.16);
            var buffer = new float[samples];
            uint noise = 0x5EED5EED;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float n = NextNoise(ref noise);
                float click = i < 24 ? (1f - i / 24f) * 0.8f : 0f;
                buffer[i] = (n * 0.7f + click) * MathF.Exp(-t * 9f);
            }

            return buffer;
        }

        private static float[] GunshotSound()
        {
            int samples = (int)(SampleRate * 0.22);
            var buffer = new float[samples];
            uint noise = 0x1BADB002;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float n = NextNoise(ref noise);
                float crack = i < 40 ? (1f - i / 40f) * (n > 0 ? 0.9f : -0.9f) : 0f;
                float thump = (float)Math.Sin(i / (double)SampleRate * 2 * Math.PI * 95) * 0.5f;
                buffer[i] = (n * 0.6f + thump + crack) * MathF.Exp(-t * 10f);
            }

            return buffer;
        }

        private static float[] RamSound()
        {
            int samples = (int)(SampleRate * 0.4);
            var buffer = new float[samples];
            uint noise = 0x77AA33CC;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float n = NextNoise(ref noise);
                float metal = (float)Math.Sin(i / (double)SampleRate * 2 * Math.PI * 420) * 0.5f;
                float metal2 = (float)Math.Sin(i / (double)SampleRate * 2 * Math.PI * 733) * 0.3f;
                buffer[i] = (n * 0.7f + metal + metal2) * MathF.Exp(-t * 12f);
            }

            return buffer;
        }

        private static float[] TurboSound()
        {
            int samples = (int)(SampleRate * 0.55);
            var buffer = new float[samples];
            uint noise = 0x31415926;
            float filtered = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float n = NextNoise(ref noise);
                filtered = filtered * 0.85f + n * 0.15f;
                float sweep = (float)Math.Sin(i / (double)SampleRate * 2 * Math.PI * (300 + 1200 * t));
                buffer[i] = (sweep * 0.45f + filtered * 0.8f) * MathF.Min(1f, t * 4f) * (1f - t * 0.6f);
            }

            return buffer;
        }

        private static float[] BustedSting()
        {
            var song = BustedTheme();
            return PcmToFloat(Render(song, false));
        }

        private static float[] SimpleTone(double seconds, float startFreq, float endFreq, Waveform wave, float duty, float gain)
        {
            int samples = (int)(seconds * SampleRate);
            var buffer = new float[samples];
            uint noise = 0x2468ACE0;
            double phase = 0;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                double freq = startFreq + (endFreq - startFreq) * t;
                phase += freq / SampleRate;
                double p = phase - Math.Floor(phase);
                float sample = wave == Waveform.Saw
                    ? (float)(2 * p - 1)
                    : wave == Waveform.Triangle
                        ? (float)(4 * Math.Abs(p - 0.5) - 1)
                        : p < duty ? 1f : -1f;
                float env = MathF.Min(1f, (1f - t) * 4f) * MathF.Min(1f, t * 40f);
                buffer[i] = sample * env * gain;
            }

            if (noise == 0)
            {
                return buffer;
            }

            return buffer;
        }

        private static float[] Arpeggio(int[] midiNotes, float noteSeconds)
        {
            int perNote = (int)(noteSeconds * SampleRate);
            var buffer = new float[perNote * midiNotes.Length];
            double phase = 0;

            for (int n = 0; n < midiNotes.Length; n++)
            {
                double freq = 440.0 * Math.Pow(2.0, (midiNotes[n] - 69) / 12.0);
                for (int i = 0; i < perNote; i++)
                {
                    float t = i / (float)perNote;
                    phase += freq / SampleRate;
                    double p = phase - Math.Floor(phase);
                    float sample = p < 0.5 ? 1f : -1f;
                    float env = MathF.Min(1f, t * 20f) * (1f - t * 0.85f);
                    buffer[n * perNote + i] = sample * env * 0.5f;
                }
            }

            return buffer;
        }

        private static float[] PcmToFloat(byte[] pcm)
        {
            var result = new float[pcm.Length / 2];
            for (int i = 0; i < result.Length; i++)
            {
                short sample = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
                result[i] = sample / 32768f;
            }

            return result;
        }

        private SoundEffect Make(float[] buffer, float volume)
        {
            try
            {
                Normalize(buffer);
                byte[] pcm = FloatToPcm(buffer, volume);
                var effect = new SoundEffect(pcm, SampleRate, AudioChannels.Mono);
                _owned.Add(effect);
                return effect;
            }
            catch (Exception)
            {
                // No audio device (headless CI, a VM without sound): the game stays silent.
                return null;
            }
        }

        /// <summary>Scales an effect so its loudest peak is 1.0 before the mix gain is applied.</summary>
        private static void Normalize(float[] buffer)
        {
            float peak = 0f;
            for (int i = 0; i < buffer.Length; i++)
            {
                peak = MathF.Max(peak, MathF.Abs(buffer[i]));
            }

            if (peak <= 0.0001f)
            {
                return;
            }

            float scale = 1f / peak;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] *= scale;
            }
        }

        private static byte[] FloatToPcm(float[] buffer, float volume)
        {
            byte[] pcm = new byte[buffer.Length * 2];
            for (int i = 0; i < buffer.Length; i++)
            {
                float value = buffer[i] * volume;
                value = value / (1f + MathF.Abs(value));
                short sample = (short)Math.Clamp(value * 30000f, -32767f, 32767f);
                pcm[i * 2] = (byte)(sample & 0xFF);
                pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
            }

            return pcm;
        }

        public void Dispose()
        {
            StopMusic();
            _engineInstance?.Dispose();
            _sirenInstance?.Dispose();

            for (int i = 0; i < _owned.Count; i++)
            {
                _owned[i]?.Dispose();
            }

            _owned.Clear();
            _music.Clear();
        }
    }
}
