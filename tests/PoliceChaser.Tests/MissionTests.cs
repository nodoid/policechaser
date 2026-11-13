using System;
using System.Collections.Generic;
using PoliceChaser.Game;
using Xunit;

namespace PoliceChaser.Tests
{
    public class MissionTests
    {
        [Fact]
        public void Campaign_has_six_missions()
        {
            Assert.Equal(6, Mission.All.Count);
        }

        [Fact]
        public void Campaign_contains_multiple_new_missions()
        {
            Assert.True(Mission.NewMissionCount >= 3);

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (Mission mission in Mission.All)
            {
                Assert.True(ids.Add(mission.Id), "mission ids must be unique");
                Assert.False(string.IsNullOrWhiteSpace(mission.Title));
                Assert.False(string.IsNullOrWhiteSpace(mission.Tagline));
                Assert.True(mission.RequiredRams >= 3);
                Assert.True(mission.BaseTimeSeconds >= 60f);
            }
        }

        [Fact]
        public void New_missions_are_the_last_three()
        {
            Assert.False(Mission.All[0].IsNewMission);
            Assert.False(Mission.All[1].IsNewMission);
            Assert.False(Mission.All[2].IsNewMission);
            Assert.True(Mission.All[3].IsNewMission);
            Assert.True(Mission.All[4].IsNewMission);
            Assert.True(Mission.All[5].IsNewMission);
        }

        [Fact]
        public void Missions_get_harder_down_the_list()
        {
            for (int i = 1; i < Mission.All.Count; i++)
            {
                Assert.True(Mission.All[i].SuspectBaseSpeed >= Mission.All[i - 1].SuspectBaseSpeed);
                Assert.True(Mission.All[i].RequiredRams >= Mission.All[i - 1].RequiredRams);
            }
        }

        [Fact]
        public void Mission_lookup_by_id_works()
        {
            Assert.NotNull(Mission.ById("neon"));
            Assert.Equal("NEON GRID", Mission.ById("neon").Title);
            Assert.Null(Mission.ById("does-not-exist"));
        }

        [Fact]
        public void Difficulty_rules_are_progressive()
        {
            Assert.True(DifficultyRules.SuspectSpeedScale(Difficulty.Hard) >
                        DifficultyRules.SuspectSpeedScale(Difficulty.Medium));
            Assert.True(DifficultyRules.TimeDrain(Difficulty.Hard) >
                        DifficultyRules.TimeDrain(Difficulty.Beginner));
            Assert.True(DifficultyRules.TrafficDensity(Difficulty.Hard) >
                        DifficultyRules.TrafficDensity(Difficulty.Medium));
            Assert.Equal(0f, DifficultyRules.CatchUpAssist(Difficulty.Hard));
        }
    }
}
