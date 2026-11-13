using System;
using PoliceChaser.Game;
using Xunit;

namespace PoliceChaser.Tests
{
    public class HighScoreTests
    {
        private static ScoreEntry Entry(string name, int score, Difficulty difficulty = Difficulty.Beginner,
            int stage = 0, string outcome = "")
        {
            return new ScoreEntry
            {
                Name = name,
                Score = score,
                Difficulty = difficulty,
                MissionId = "harbour",
                MissionTitle = "HARBOUR PURSUIT",
                Stage = stage,
                Outcome = outcome,
                Ticks = DateTime.UtcNow.Ticks
            };
        }

        [Fact]
        public void Table_keeps_only_the_top_ten()
        {
            var table = new HighScoreTable();
            for (int i = 1; i <= 14; i++)
            {
                table.Add(Entry("P" + i, i * 100));
            }

            Assert.Equal(10, table.Entries.Count);
            Assert.Equal(1400, table.Entries[0].Score);
            Assert.Equal(500, table.Entries[9].Score);
        }

        [Fact]
        public void Table_is_sorted_high_to_low()
        {
            var table = new HighScoreTable();
            table.Add(Entry("AAA", 300));
            table.Add(Entry("BBB", 900));
            table.Add(Entry("CCC", 100));

            Assert.Equal("BBB", table.Entries[0].Name);
            Assert.Equal("AAA", table.Entries[1].Name);
            Assert.Equal("CCC", table.Entries[2].Name);
        }

        [Fact]
        public void A_low_score_does_not_make_a_full_table()
        {
            var table = new HighScoreTable();
            for (int i = 0; i < 10; i++)
            {
                table.Add(Entry("AAA", 1000 + i));
            }

            Assert.False(table.Qualifies(500));
            Assert.True(table.Qualifies(2000));
        }

        [Fact]
        public void Scores_survive_a_restart()
        {
            var file = new MemoryHighScoreFile();
            var first = new HighScoreStore(file);
            first.Submit(Entry("PC1", 4321, Difficulty.Hard, 7, "RAMMED A CAR"));
            first.Submit(Entry("PC2", 8765, Difficulty.Medium, 4, "RAN OUT OF TIME"));

            Assert.False(string.IsNullOrWhiteSpace(file.Contents));

            // Simulate quitting and launching again: fresh store, same file.
            var second = new HighScoreStore(file);
            Assert.Equal(2, second.Table.Entries.Count);
            Assert.Equal(8765, second.BestScore());
            Assert.Equal("PC1", second.Table.Entries[1].Name);
            Assert.Equal(Difficulty.Hard, second.Table.Entries[1].Difficulty);
            Assert.Equal(7, second.Table.Entries[1].Stage);
            Assert.Equal("RAMMED A CAR", second.Table.Entries[1].Outcome);
            Assert.Equal("STAGE 7 - RAMMED A CAR", second.Table.Entries[1].RunSummary);
        }

        [Fact]
        public void Corrupt_file_starts_a_clean_table_instead_of_crashing()
        {
            var file = new MemoryHighScoreFile { Contents = "{ this is not json" };
            var store = new HighScoreStore(file);

            Assert.Empty(store.Table.Entries);
            store.Submit(Entry("AAA", 10));
            Assert.Single(new HighScoreStore(file).Table.Entries);
        }

        [Fact]
        public void Submit_returns_the_rank_of_a_qualifying_score()
        {
            var store = new HighScoreStore(new MemoryHighScoreFile());
            store.Submit(Entry("AAA", 500));
            int rank = store.Submit(Entry("BBB", 900));

            Assert.Equal(1, rank);
            Assert.Equal(900, store.BestScore());
        }

        [Fact]
        public void Stage_column_reads_all_for_a_cleared_campaign()
        {
            Assert.Equal("7", Entry("AAA", 100, Difficulty.Hard, 7).StageLabel);
            Assert.Equal("ALL", Entry("AAA", 100, Difficulty.Hard, 9, "ALL STAGES CLEARED").StageLabel);
            Assert.Equal("-", Entry("AAA", 100).StageLabel);
        }

        [Fact]
        public void Difficulty_labels_are_present_on_entries()
        {
            var entry = Entry("AAA", 100, Difficulty.Hard);
            Assert.Equal("HARD", entry.DifficultyLabel);
            Assert.Equal("HARBOUR PURSUIT", entry.MissionLabel);
        }
    }
}
