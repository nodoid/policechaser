using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoliceChaser.Game
{
    /// <summary>Player audio preferences, 0..1 in tenths so the bars and buttons line up.</summary>
    public sealed class GameSettings
    {
        public float Music { get; set; } = 1f;

        public float Effects { get; set; } = 1f;

        public static float Clamp(float value)
        {
            return MathF.Round(Math.Clamp(value, 0f, 1f) * 10f) / 10f;
        }
    }

    /// <summary>Where preferences live. Abstracted so tests and smoke runs never touch the disk.</summary>
    public interface ISettingsFile
    {
        string Read();

        void Write(string contents);
    }

    public sealed class SettingsStore
    {
        public const string FileName = "policechaser.settings.json";

        private readonly ISettingsFile _file;
        private GameSettings _value = new GameSettings();
        private bool _loaded;

        public SettingsStore(ISettingsFile file)
        {
            _file = file;
        }

        public GameSettings Value
        {
            get
            {
                Load();
                return _value;
            }
        }

        public void Load()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;

            try
            {
                string text = _file.Read();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return;
                }

                GameSettings loaded = JsonSerializer.Deserialize(text, SettingsJsonContext.Default.GameSettings);
                if (loaded == null)
                {
                    return;
                }

                loaded.Music = GameSettings.Clamp(loaded.Music);
                loaded.Effects = GameSettings.Clamp(loaded.Effects);
                _value = loaded;
            }
            catch (Exception)
            {
                // A corrupt settings file falls back to the defaults instead of breaking audio.
                _value = new GameSettings();
            }
        }

        public void Save()
        {
            Load();

            try
            {
                _file.Write(JsonSerializer.Serialize(_value, SettingsJsonContext.Default.GameSettings));
            }
            catch (Exception)
            {
                // Preferences are a convenience; never let a full disk take the game down.
            }
        }

        public void SetMusic(float value)
        {
            Value.Music = GameSettings.Clamp(value);
            Save();
        }

        public void SetEffects(float value)
        {
            Value.Effects = GameSettings.Clamp(value);
            Save();
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(GameSettings))]
    public partial class SettingsJsonContext : JsonSerializerContext
    {
    }

    public sealed class LocalSettingsFile : ISettingsFile
    {
        private readonly string _path;

        public LocalSettingsFile(string rootDirectory)
        {
            Directory.CreateDirectory(rootDirectory);
            _path = Path.Combine(rootDirectory, SettingsStore.FileName);
        }

        public string Read()
        {
            return File.Exists(_path) ? File.ReadAllText(_path) : null;
        }

        public void Write(string contents)
        {
            File.WriteAllText(_path, contents);
        }
    }

    public sealed class MemorySettingsFile : ISettingsFile
    {
        public string Contents { get; set; }

        public string Read()
        {
            return Contents;
        }

        public void Write(string contents)
        {
            Contents = contents;
        }
    }
}
