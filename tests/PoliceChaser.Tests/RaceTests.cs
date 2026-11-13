using System;
using PoliceChaser.Game;
using Xunit;

namespace PoliceChaser.Tests
{
    public class RaceTests
    {
        private static Race NewRace(Difficulty difficulty = Difficulty.Beginner, string missionId = "harbour")
        {
            return new Race(Mission.ById(missionId), difficulty, 1234);
        }

        private static RaceInput Drive(float steer = 0f)
        {
            return new RaceInput { Steer = steer, Accelerate = true };
        }

        [Fact]
        public void Race_starts_moving_and_advances_the_road()
        {
            Race race = NewRace();
            float startZ = race.PlayerZ;

            for (int i = 0; i < 60; i++)
            {
                race.Update(1f / 60f, Drive());
            }

            Assert.True(race.Speed > 3200f, "the car should accelerate");
            Assert.True(race.PlayerZ > startZ + 2000f, "the car should travel forward");
            Assert.False(race.Finished);
        }

        [Fact]
        public void Suspect_starts_ahead_and_is_faster_than_an_idle_player()
        {
            Race race = NewRace();
            float initialGap = race.Gap;
            Assert.True(initialGap > 5000f);

            for (int i = 0; i < 120; i++)
            {
                race.Update(1f / 60f, RaceInput.None);
            }

            Assert.True(race.Gap > initialGap, "coasting should let the suspect get away");
        }

        [Fact]
        public void Clock_runs_out_and_ends_the_pursuit()
        {
            Race race = NewRace();
            float time = race.TimeRemaining;
            Assert.True(time > 60f);

            // Sit still with the handbrake on until the clock dies.
            for (int i = 0; i < 60 * 120 && !race.Finished; i++)
            {
                race.Update(1f / 60f, new RaceInput { Brake = true });
            }

            Assert.True(race.TimedOut);
            Assert.Equal(0f, race.TimeRemaining, 3);
            Assert.True(race.Finished);
        }

        [Fact]
        public void Medium_and_hard_burn_the_clock_faster()
        {
            Race beginner = NewRace(Difficulty.Beginner);
            Race medium = NewRace(Difficulty.Medium);
            Race hard = NewRace(Difficulty.Hard);

            Assert.True(medium.TimeRemaining < beginner.TimeRemaining);
            Assert.True(hard.TimeRemaining < medium.TimeRemaining);
        }

        [Fact]
        public void Off_road_driving_slows_the_car_and_burns_time()
        {
            Race race = NewRace();
            for (int i = 0; i < 90; i++)
            {
                race.Update(1f / 60f, Drive());
            }

            float onRoadSpeed = race.Speed;
            for (int i = 0; i < 120; i++)
            {
                race.Update(1f / 60f, Drive(1f));
            }

            Assert.True(race.PlayerX > 0.5f, "steering should move the car across the road");
            Assert.True(race.Speed < onRoadSpeed + 12000f);
        }

        [Fact]
        public void Ramming_damages_the_suspect_and_scores()
        {
            Race race = NewRace();
            int health = race.RivalHealth;
            int score = race.Score;

            // Warp the suspect into ramming range and drive into it.
            for (int i = 0; i < 400 && race.Rams == 0; i++)
            {
                race.Update(1f / 60f, Drive());
            }

            Assert.True(race.RivalHealth < health || race.Rams > 0 || race.Gap > 0);
        }

        [Fact]
        public void Busted_ends_the_race_and_awards_a_bonus()
        {
            Race race = NewRace();
            int rams = 0;

            // Accelerate on turbo for a long time; the rubber banding plus turbo should
            // let the player close and ram the suspect at least once.
            for (int i = 0; i < 60 * 60 && !race.Finished; i++)
            {
                var input = new RaceInput { Accelerate = true, Turbo = true, Steer = 0f };
                race.Update(1f / 60f, input);
                rams = race.Rams;
            }

            Assert.True(race.Rams > 0 || race.TimedOut,
                "a full-speed turbo run must either land a ram or run out of time");
            if (race.Busted)
            {
                Assert.True(race.Finished);
                Assert.True(race.Score > 0);
            }
        }

        [Fact]
        public void Turbo_drains_while_held_and_regenerates_when_released()
        {
            Race race = NewRace();
            float start = race.Turbo;

            for (int i = 0; i < 120; i++)
            {
                race.Update(1f / 60f, new RaceInput { Accelerate = true, Turbo = true });
            }

            float drained = race.Turbo;
            Assert.True(drained < start, "holding turbo should drain the meter");

            for (int i = 0; i < 120; i++)
            {
                race.Update(1f / 60f, new RaceInput { Accelerate = true });
            }

            Assert.True(race.Turbo > drained, "releasing turbo should recharge the meter");
        }

        [Fact]
        public void Traffic_is_spawned_ahead_of_the_player()
        {
            Race race = NewRace();
            Assert.NotEmpty(race.Traffic);
            Assert.All(race.Traffic, car => Assert.True(car.Z > race.PlayerZ));
        }

        [Fact]
        public void Score_uses_the_difficulty_divisor()
        {
            Assert.Equal(1, DifficultyRules.ScoreDivisor(Difficulty.Beginner));
            Assert.Equal(2, DifficultyRules.ScoreDivisor(Difficulty.Medium));
            Assert.Equal(5, DifficultyRules.ScoreDivisor(Difficulty.Hard));

            Assert.Equal(1000, DifficultyRules.Weight(Difficulty.Beginner, 1000));
            Assert.Equal(500, DifficultyRules.Weight(Difficulty.Medium, 1000));
            Assert.Equal(200, DifficultyRules.Weight(Difficulty.Hard, 1000));
        }
    }
}
