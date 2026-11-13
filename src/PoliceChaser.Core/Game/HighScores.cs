using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PoliceChaser.Game
{
    public sealed class ScoreEntry
    {
        public string Name { get; set; } = "AAA";
        public int Score { get; set; }
        public Difficulty Difficulty { get; set; }
        public string MissionId { get; set; } = "";
        public string MissionTitle { get; set; } = "";
        public int Rams { get; set; }

        /// <summary>1-based stage the run ended on, 0 for entries saved before this existed.</summary>
        public int Stage { get; set; }

        /// <summary>How the run ended: "RAMMED A CAR", "SHOT BY THE SUSPECT", etc.</summary>
        public string Outcome { get; set; } = "";

        public long Ticks { get; set; }

        [JsonIgnore]
        public string DifficultyLabel => DifficultyRules.Name(Difficulty);

        /// <summary>"STAGE 7 - RAMMED A CAR", or empty for a legacy entry with no stage.</summary>
        [JsonIgnore]
        public string RunSummary
        {
            get
            {
                if (Stage <= 0)
                {
                    return Outcome ?? "";
                }

                return "STAGE " + Stage + " - " +
                    (string.IsNullOrEmpty(Outcome) ? "PURSUIT FAILED" : Outcome);
            }
        }

        /// <summary>"ALL" for a cleared campaign, the stage number, or "-" for a legacy entry.</summary>
        [JsonIgnore]
        public string StageLabel
        {
            get
            {
                if (Outcome == "ALL STAGES CLEARED")
                {
                    return "ALL";
                }

                return Stage > 0 ? Stage.ToString() : "-";
            }
        }

        [JsonIgnore]
        public string MissionLabel =>
            string.IsNullOrEmpty(MissionTitle) ? "---" : MissionTitle;
    }

    public sealed class HighScoreTable
    {
        public const int MaxEntries = 10;

        public List<ScoreEntry> Entries { get; set; } = new List<ScoreEntry>();

        public void Sort()
        {
            Entries.Sort((a, b) =>
            {
                int byScore = b.Score.CompareTo(a.Score);
                if (byScore != 0)
                {
                    return byScore;
                }

                return a.Ticks.CompareTo(b.Ticks);
            });
        }

        public bool Qualifies(int score)
        {
            if (Entries.Count < MaxEntries)
            {
                return true;
            }

            int lowest = int.MaxValue;
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Score < lowest)
                {
                    lowest = Entries[i].Score;
                }
            }

            return score > lowest;
        }

        /// <summary>Adds a score and returns its 1-based rank, or 0 if it did not make the table.</summary>
        public int Add(ScoreEntry entry)
        {
            if (entry == null)
            {
                return 0;
            }

            if (entry.Ticks == 0)
            {
                entry.Ticks = DateTime.UtcNow.Ticks;
            }

            Entries.Add(entry);
            Sort();

            int rank = Entries.IndexOf(entry) + 1;
            if (Entries.Count > MaxEntries)
            {
                Entries.RemoveRange(MaxEntries, Entries.Count - MaxEntries);
            }

            return rank <= MaxEntries ? rank : 0;
        }

        public ScoreEntry Best()
        {
            return Entries.Count > 0 ? Entries[0] : null;
        }
    }

    /// <summary>Where the high score table physically lives. Abstracted so tests can use memory.</summary>
    public interface IHighScoreFile
    {
        string Read();
        void Write(string contents);
    }

    public sealed class HighScoreStore
    {
        public const string FileName = "policechaser.scores.json";

        private readonly IHighScoreFile _file;
        private readonly HighScoreTable _table = new HighScoreTable();
        private bool _loaded;

        public HighScoreStore(IHighScoreFile file)
        {
            _file = file;
        }

        public HighScoreTable Table
        {
            get
            {
                Load();
                return _table;
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

                HighScoreFileModel model = JsonSerializer.Deserialize(text, HighScoreJsonContext.Default.HighScoreFileModel);
                if (model?.Entries != null)
                {
                    _table.Entries.Clear();
                    for (int i = 0; i < model.Entries.Count; i++)
                    {
                        if (model.Entries[i] != null)
                        {
                            _table.Entries.Add(model.Entries[i]);
                        }
                    }

                    _table.Sort();
                    if (_table.Entries.Count > HighScoreTable.MaxEntries)
                    {
                        _table.Entries.RemoveRange(HighScoreTable.MaxEntries,
                            _table.Entries.Count - HighScoreTable.MaxEntries);
                    }
                }
            }
            catch (Exception)
            {
                // A corrupt table must never take the game down; start a fresh one.
                _table.Entries.Clear();
            }
        }

        public void Save()
        {
            Load();
            _table.Sort();

            var model = new HighScoreFileModel
            {
                Version = 1,
                Entries = new List<ScoreEntry>(_table.Entries)
            };

            string json = JsonSerializer.Serialize(model, HighScoreJsonContext.Default.HighScoreFileModel);
            _file.Write(json);
        }

        /// <summary>Adds an entry, saving immediately so a quit never loses a score.</summary>
        public int Submit(ScoreEntry entry)
        {
            Load();
            int rank = _table.Add(entry);
            Save();
            return rank;
        }

        public int BestScore()
        {
            Load();
            ScoreEntry best = _table.Best();
            return best == null ? 0 : best.Score;
        }
    }

    public sealed class HighScoreFileModel
    {
        public int Version { get; set; }
        public List<ScoreEntry> Entries { get; set; } = new List<ScoreEntry>();
    }

    /// <summary>Source generated serialisation keeps AOT/trimming iOS builds working.</summary>
    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(HighScoreFileModel))]
    [JsonSerializable(typeof(ScoreEntry))]
    public partial class HighScoreJsonContext : JsonSerializerContext
    {
    }

    /// <summary>File based store rooted in the platform's local application data folder.</summary>
    public sealed class LocalHighScoreFile : IHighScoreFile
    {
        private readonly string _path;

        public LocalHighScoreFile(string rootDirectory)
        {
            Directory.CreateDirectory(rootDirectory);
            _path = Path.Combine(rootDirectory, HighScoreStore.FileName);
        }

        public string FilePath => _path;

        public static string DefaultRoot
        {
            get
            {
                string root = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData,
                    Environment.SpecialFolderOption.Create);
                return System.IO.Path.Combine(root, "PoliceChaser");
            }
        }

        public string Read()
        {
            return File.Exists(_path) ? File.ReadAllText(_path) : null;
        }

        public void Write(string contents)
        {
            string temp = _path + ".tmp";
            File.WriteAllText(temp, contents);
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            File.Move(temp, _path);
        }
    }

    public sealed class MemoryHighScoreFile : IHighScoreFile
    {
        public string Contents { get; set; }

        public string Read() => Contents;

        public void Write(string contents) => Contents = contents;
    }
}
