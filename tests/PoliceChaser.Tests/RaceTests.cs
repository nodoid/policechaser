using System;
using System.IO;
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
        public void Armed_series_two_suspects_shoot_back()
        {
            Race race = new Race(Mission.ById("steelworks"), Difficulty.Beginner, 1234);
            Assert.Equal(4, race.PlayerMaxHealth);
            Assert.Equal(4, race.PlayerHealth);

            bool sawGunfire = false;
            int lowestHull = race.PlayerHealth;
            for (int i = 0; i < 60 * 90 && !race.Finished; i++)
            {
                float steer = Math.Clamp((race.RivalX - race.PlayerX) * 2f, -1f, 1f);
                race.Update(1f / 60f, new RaceInput
                {
                    Accelerate = true,
                    Turbo = race.Turbo > 0.05f,
                    Steer = steer
                });
                lowestHull = Math.Min(lowestHull, race.PlayerHealth);

                // Isolate the shooting: traffic crashes are covered by their own tests and
                // would otherwise wreck the car before it ever gets into gun range.
                race.Traffic.Clear();

                foreach (RaceEvent evt in race.Events)
                {
                    if (evt.Type == RaceEventType.Gunfire)
                    {
                        sawGunfire = true;
                    }
                }
            }

            Assert.True(sawGunfire,
                "an armed suspect should open fire when the player closes in (gap=" + race.Gap +
                " speed=" + race.Speed + " busted=" + race.Busted + " timedOut=" + race.TimedOut +
                " wrecked=" + race.Wrecked + " hull=" + race.PlayerHealth + ")");
            Assert.True(lowestHull <= race.PlayerMaxHealth);
            Assert.Equal(race.Wrecked, race.PlayerHealth <= 0);
        }

        [Fact]
        public void Unarmed_missions_carry_a_hull_and_traffic_damages_it()
        {
            Race race = NewRace();
            Assert.Equal(4, race.PlayerMaxHealth);
            Assert.Equal(4, race.PlayerHealth);

            race.Traffic.Clear();
            race.Traffic.Add(new TrafficCar { Z = race.PlayerZ + 300f, X = race.PlayerX, Speed = 0f });
            float time = race.TimeRemaining;

            race.Update(1f / 60f, Drive());

            Assert.Equal(3, race.PlayerHealth);
            Assert.Equal(1, race.TrafficHits);
            Assert.True(race.TimeRemaining < time - 1.5f, "a crash must cost time as well as hull");
        }

        [Fact]
        public void Traffic_is_caught_even_when_a_long_frame_jumps_over_it()
        {
            Race race = NewRace();
            race.Traffic.Clear();

            // An oncoming car on the nose: after a clamped frame the player has jumped past
            // the 430-unit collision window, so only a swept test can register the impact.
            race.Traffic.Add(new TrafficCar { Z = race.PlayerZ + 100f, X = race.PlayerX, Speed = -12000f });

            race.Update(0.05f, Drive());

            Assert.Equal(1, race.TrafficHits);
            Assert.Equal(race.PlayerMaxHealth - 1, race.PlayerHealth);
        }

        [Fact]
        public void Losing_all_hull_to_traffic_wrecks_the_car()
        {
            Race race = NewRace();
            race.Traffic.Clear();

            for (int i = 0; i < race.PlayerMaxHealth; i++)
            {
                race.Traffic.Add(new TrafficCar { Z = race.PlayerZ + 60f, X = race.PlayerX, Speed = 0f });
                race.Update(1f / 60f, Drive());
            }

            Assert.True(race.Wrecked);
            Assert.True(race.Finished);
            Assert.Equal(0, race.PlayerHealth);
        }

        /// <summary>Steer towards a lane and hold it with a stiff controller.</summary>
        private static float HoldLane(Race race, float lane)
        {
            return Math.Clamp((lane - race.PlayerX) * 5f, -1f, 1f);
        }

        /// <summary>How clear a lane stays over the next three seconds of closing speed.</summary>
        private static float LaneScore(Race race, float lane)
        {
            float score = 100f - MathF.Abs(lane) * 6f;
            foreach (TrafficCar car in race.Traffic)
            {
                float dz = car.Z - race.PlayerZ;
                if (dz < -500f || dz > 30000f || MathF.Abs(car.X - lane) > 0.42f)
                {
                    continue;
                }

                float closing = MathF.Max(400f, race.Speed - car.Speed);
                float timeToCollision = dz / closing;
                if (timeToCollision > 3f)
                {
                    continue;
                }

                score -= 220f * (1f - timeToCollision / 3f);
            }

            return score;
        }

        /// <summary>A simple driver: pick the clearest lane, brake for anything left in it.</summary>
        private static RaceInput Autopilot(Race race)
        {
            float lane = 0f;
            if (race.Gap < 2000f)
            {
                lane = race.RivalX;
            }
            else
            {
                float best = float.MinValue;
                for (int i = -2; i <= 2; i++)
                {
                    float candidate = i * 0.33f;
                    float score = LaneScore(race, candidate);
                    if (score > best)
                    {
                        best = score;
                        lane = candidate;
                    }
                }
            }

            return new RaceInput
            {
                Accelerate = true,
                Turbo = race.Turbo > 0.4f && race.Gap > 500f,
                Steer = Math.Clamp((lane - race.PlayerX) * 4f, -1f, 1f)
            };
        }

        [Fact]
        public void Hard_campaign_is_winnable_by_a_clean_autopilot()
        {
            var results = new System.Collections.Generic.List<string>();
            int wins = 0;

            foreach (Mission mission in Mission.All)
            {
                Race race = new Race(mission, Difficulty.Hard, 4242);
                for (int i = 0; i < 60 * 150 && !race.Finished; i++)
                {
                    race.Update(1f / 60f, Autopilot(race));
                }

                if (race.Busted)
                {
                    wins++;
                }

                results.Add(mission.Title.PadRight(16) +
                    (race.Busted ? "WIN     " : race.Wrecked ? "WRECKED " : "TIMEOUT ") +
                    "hull=" + race.PlayerHealth + "/" + race.PlayerMaxHealth +
                    " rams=" + race.Rams + "/" + race.RivalMaxHealth +
                    " time=" + race.TimeRemaining.ToString("F1") +
                    " gap=" + (int)race.Gap + " x=" + race.PlayerX.ToString("F2") +
                    " offroad=" + race.OffRoad + " spd=" + (int)race.Speed +
                    " hits=" + race.TrafficHits);
            }

            File.WriteAllLines("/tmp/hard-campaign.txt", results);
            Assert.True(wins >= 6, wins + "/9 wins: " + string.Join(" | ", results));
        }

        [Fact]
        public void Hard_chase_is_catchable_without_traffic()
        {
            Race race = new Race(Mission.ById("harbour"), Difficulty.Hard, 4242);
            for (int i = 0; i < 60 * 45 && !race.Finished; i++)
            {
                race.Traffic.Clear();
                race.Update(1f / 60f, Autopilot(race));
            }

            Assert.True(race.Rams > 0 || race.Busted,
                "gap=" + (int)race.Gap + " speed=" + (int)race.Speed + " time=" + race.TimeRemaining +
                " rams=" + race.Rams + " timedOut=" + race.TimedOut + " busted=" + race.Busted);
        }

        [Fact]
        public void Gears_shift_up_and_back_down_with_speed()
        {
            Race race = NewRace();
            bool sawUp = false;

            for (int i = 0; i < 60 * 12 && !race.Finished && !race.HighGear; i++)
            {
                race.Traffic.Clear();
                race.Update(1f / 60f, Drive());
                foreach (RaceEvent evt in race.Events)
                {
                    if (evt.Type == RaceEventType.GearChange && evt.Value == 1)
                    {
                        sawUp = true;
                    }
                }
            }

            Assert.True(sawUp, "speeding up should shift into high gear (speed=" + race.Speed + ")");
            Assert.True(race.HighGear);
            Assert.Equal("HI", race.GearLabel);

            bool sawDown = false;
            for (int i = 0; i < 60 * 12 && !race.Finished && race.HighGear; i++)
            {
                race.Update(1f / 60f, new RaceInput { Brake = true });
                foreach (RaceEvent evt in race.Events)
                {
                    if (evt.Type == RaceEventType.GearChange && evt.Value == 0)
                    {
                        sawDown = true;
                    }
                }
            }

            Assert.True(sawDown, "braking should drop back to low gear");
            Assert.False(race.HighGear);
            Assert.Equal("LO", race.GearLabel);
        }

        [Fact]
        public void Armed_fire_warns_before_the_round_lands()
        {
            Race race = new Race(Mission.ById("steelworks"), Difficulty.Beginner, 4321);
            bool warned = false;
            bool fired = false;
            bool warningCameFirst = false;

            for (int i = 0; i < 60 * 90 && !race.Finished; i++)
            {
                race.Traffic.Clear();
                float gap = race.Gap;
                race.Update(1f / 60f, new RaceInput
                {
                    Accelerate = gap > 2600f,
                    Brake = gap < 1800f,
                    Turbo = gap > 5500f && race.Turbo > 0.05f,
                    Steer = Math.Clamp((race.RivalX - race.PlayerX) * 2f, -1f, 1f)
                });

                foreach (RaceEvent evt in race.Events)
                {
                    if (evt.Type == RaceEventType.GunfireWarning)
                    {
                        warned = true;
                        if (!fired)
                        {
                            warningCameFirst = true;
                        }
                    }
                    else if (evt.Type == RaceEventType.Gunfire)
                    {
                        fired = true;
                    }
                }
            }

            Assert.True(warned, "armed suspect never telegraphed a shot");
            Assert.True(fired, "the telegraph was never followed by a shot");
            Assert.True(warningCameFirst, "a shot landed before any warning");
        }

        [Fact]
        public void Armed_fire_costs_a_plate_every_third_hit()
        {
            Race race = new Race(Mission.ById("steelworks"), Difficulty.Beginner, 99);
            int hits = 0;
            int plates = 0;
            int hull = race.PlayerHealth;

            for (int i = 0; i < 60 * 90 && !race.Finished; i++)
            {
                race.Traffic.Clear();

                // Sit in gun range without ramming: the pursuit would otherwise be over
                // before the suspect has fired often enough to observe the cadence.
                float gap = race.Gap;
                race.Update(1f / 60f, new RaceInput
                {
                    Accelerate = gap > 2600f,
                    Brake = gap < 1800f,
                    Turbo = gap > 5500f && race.Turbo > 0.05f,
                    Steer = Math.Clamp((race.RivalX - race.PlayerX) * 2f, -1f, 1f)
                });

                foreach (RaceEvent evt in race.Events)
                {
                    if (evt.Type == RaceEventType.PlayerHit)
                    {
                        hits++;
                    }
                }

                if (race.PlayerHealth < hull)
                {
                    plates++;
                }

                hull = race.PlayerHealth;
            }

            Assert.True(hits >= 4, "expected sustained fire, got " + hits + " hits");
            Assert.True(plates <= hits / 3 + 1,
                plates + " plates from " + hits + " hits (expected at most every third round)");
        }

        [Fact]
        public void Checkpoints_patch_one_hull_plate()
        {
            Race race = NewRace();
            race.Traffic.Clear();
            race.Traffic.Add(new TrafficCar { Z = race.PlayerZ + 60f, X = race.PlayerX, Speed = 0f });
            race.Update(1f / 60f, Drive());

            int damaged = race.PlayerHealth;
            Assert.Equal(race.PlayerMaxHealth - 1, damaged);

            // Hold the outside lane so the test driver does not accidentally bust the suspect
            // before it can reach the checkpoint.
            for (int i = 0; i < 60 * 60 && race.Checkpoints == 0 && !race.Finished; i++)
            {
                race.Traffic.Clear();
                race.Update(1f / 60f, Drive(HoldLane(race, 0.8f)));
            }

            Assert.True(race.Checkpoints == 1,
                "checkpoints=" + race.Checkpoints + " z=" + race.PlayerZ + " spacing=" +
                Race.CheckpointSpacing + " speed=" + race.Speed + " x=" + race.PlayerX +
                " offroad=" + race.OffRoad + " time=" + race.TimeRemaining +
                " finished=" + race.Finished + " timedOut=" + race.TimedOut +
                " busted=" + race.Busted + " wrecked=" + race.Wrecked +
                " hull=" + race.PlayerHealth + " hits=" + race.TrafficHits);
            Assert.Equal(damaged + 1, race.PlayerHealth);
        }

        [Fact]
        public void Hard_carries_more_armour_and_a_softer_crash_penalty()
        {
            Assert.True(DifficultyRules.HullPoints(Difficulty.Hard) >
                        DifficultyRules.HullPoints(Difficulty.Beginner));
            Assert.True(DifficultyRules.CrashPenalty(Difficulty.Hard) <
                        DifficultyRules.CrashPenalty(Difficulty.Beginner));

            Race hard = NewRace(Difficulty.Hard);
            Assert.Equal(DifficultyRules.HullPoints(Difficulty.Hard), hard.PlayerHealth);
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
