using System;
using System.Collections.Generic;

namespace PoliceChaser.Game
{
    public struct RaceInput
    {
        public float Steer;
        public bool Accelerate;
        public bool Brake;
        public bool Turbo;

        public static RaceInput None => new RaceInput();
    }

    public enum RaceEventType
    {
        Ram,
        NearMiss,
        TrafficHit,
        Checkpoint,
        Busted,
        TimedOut,
        TurboEmpty,
        OffRoad,
        SuspectEscaping
    }

    public struct RaceEvent
    {
        public RaceEventType Type;
        public int Value;
    }

    public sealed class TrafficCar
    {
        public float Z;
        public float X;
        public float Speed;
        public int Kind;
        public bool Passed;
        public bool SameDirection;
    }

    /// <summary>
    /// Pure pursuit simulation: no MonoGame types, no drawing. Everything the unit tests care
    /// about (timing, ramming, scoring, difficulty weighting) is decided here.
    /// </summary>
    public sealed class Race
    {
        public const float MaxSpeed = 12000f;
        public const float TurboMaxSpeed = 15800f;
        public const float RoadHalfWidth = 1f;
        public const float OffRoadLimit = 1.03f;
        public const float RamRange = 950f;
        public const float RamLateral = 0.44f;
        public const float CheckpointSpacing = 26000f;

        private readonly Random _random;
        private float _spawnTimer;
        private float _ramCooldown;
        private float _escapeWarnTimer;
        private float _rivalPhase;
        private float _shake;
        private float _skid;

        public Mission Mission { get; }
        public Difficulty Difficulty { get; }
        public Road Road { get; }

        public float PlayerZ { get; private set; }
        public float PlayerX { get; private set; }
        public float Speed { get; private set; }
        public float Turbo { get; private set; }
        public float TimeRemaining { get; private set; }
        public float TotalTime { get; private set; }

        public float RivalZ { get; private set; }
        public float RivalX { get; private set; }
        public float RivalSpeed { get; private set; }
        public int RivalHealth { get; private set; }
        public int RivalMaxHealth { get; }

        public int Rams { get; private set; }
        public int Checkpoints { get; private set; }
        public int TrafficHits { get; private set; }
        public int NearMisses { get; private set; }
        public int RawScore { get; private set; }
        public int Score => DifficultyRules.Weight(Difficulty, RawScore);

        public bool Busted { get; private set; }
        public bool TimedOut { get; private set; }
        public bool Finished => Busted || TimedOut;

        public float Shake => _shake;
        public float Skid => _skid;
        public float Gap => RivalZ - PlayerZ;
        public float SpeedRatio => MathF.Min(1.6f, Speed / MaxSpeed);
        public bool OffRoad => MathF.Abs(PlayerX) > OffRoadLimit;
        public bool Escaping => Gap > 15000f;

        public List<TrafficCar> Traffic { get; } = new List<TrafficCar>(48);
        public List<RaceEvent> Events { get; } = new List<RaceEvent>(8);

        public Race(Mission mission, Difficulty difficulty, int seed = 0)
        {
            Mission = mission ?? throw new ArgumentNullException(nameof(mission));
            Difficulty = difficulty;
            Road = PoliceChaser.Game.Road.Build(mission, seed);
            _random = new Random(seed * 31 + 977);

            RivalMaxHealth = mission.RequiredRams;
            RivalHealth = mission.RequiredRams;

            float suspectScale = DifficultyRules.SuspectSpeedScale(difficulty);
            RivalSpeed = mission.SuspectBaseSpeed * suspectScale;
            RivalZ = mission.SuspectBaseSpeed * 4.2f;
            RivalX = 0f;
            PlayerZ = 0f;
            PlayerX = 0f;
            Speed = 3200f;
            Turbo = 1f;
            TotalTime = DifficultyRules.BaseTime(difficulty, mission.BaseTimeSeconds);
            TimeRemaining = TotalTime;
            _spawnTimer = 0.4f;
            _rivalPhase = 0.7f;

            for (int i = 0; i < 6; i++)
            {
                SpawnTraffic(PlayerZ + 5000f + i * 3400f);
            }
        }

        public void Update(float dt, RaceInput input)
        {
            if (Finished)
            {
                return;
            }

            if (dt <= 0f)
            {
                return;
            }

            if (dt > 0.05f)
            {
                dt = 0.05f;
            }

            Events.Clear();
            _shake = MathF.Max(0f, _shake - dt * 3.2f);
            _skid = MathF.Max(0f, _skid - dt * 2.4f);
            _ramCooldown = MathF.Max(0f, _ramCooldown - dt);

            float steer = Math.Clamp(input.Steer, -1f, 1f);
            bool offRoad = OffRoad;
            float drain = DifficultyRules.TimeDrain(Difficulty) * (offRoad ? 1.45f : 1f);
            if (Escaping)
            {
                drain *= 1.35f;
            }

            TimeRemaining -= dt * drain;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                TimedOut = true;
                Events.Add(new RaceEvent { Type = RaceEventType.TimedOut });
                return;
            }

            // ---- longitudinal physics -------------------------------------------------
            bool boosting = input.Turbo && Turbo > 0.02f && !offRoad;
            float maxSpeed = boosting ? TurboMaxSpeed : MaxSpeed;

            float acceleration;
            if (offRoad)
            {
                acceleration = -5200f;
            }
            else if (input.Brake)
            {
                acceleration = -10500f;
            }
            else if (input.Accelerate || boosting)
            {
                float headroom = SpeedRatio < 0.92f ? 1f : 0.3f;
                acceleration = 6900f * headroom;
            }
            else
            {
                acceleration = -1600f;
            }

            if (boosting)
            {
                acceleration += 5200f;
                Turbo = MathF.Max(0f, Turbo - 0.34f * dt);
                if (Turbo <= 0f)
                {
                    Events.Add(new RaceEvent { Type = RaceEventType.TurboEmpty });
                }
            }
            else
            {
                Turbo = MathF.Min(1f, Turbo + 0.062f * dt);
            }

            Speed += acceleration * dt;
            if (Speed > maxSpeed)
            {
                Speed = MathF.Max(maxSpeed, Speed - 5200f * dt);
            }

            if (offRoad)
            {
                Speed = MathF.Min(Speed, 3800f + 2400f * MathF.Abs(PlayerX - OffRoadLimit));
                _skid = 1f;
                if (_random.NextDouble() < dt * 9.0)
                {
                    Events.Add(new RaceEvent { Type = RaceEventType.OffRoad });
                }
            }

            Speed = Math.Clamp(Speed, 0f, TurboMaxSpeed);

            // ---- lateral physics ------------------------------------------------------
            float curve = Road.CurveAt(PlayerZ);
            float grip = MathF.Min(1.35f, 0.3f + SpeedRatio);
            PlayerX += steer * 2.35f * grip * dt;
            PlayerX -= curve * SpeedRatio * SpeedRatio * 0.052f * dt * 10f;
            PlayerX = Math.Clamp(PlayerX, -2.3f, 2.3f);

            PlayerZ += Speed * dt;
            PlayerX -= curve * SpeedRatio * 0.00004f;

            // ---- suspect -------------------------------------------------------------
            float suspectScale = DifficultyRules.SuspectSpeedScale(Difficulty);
            float baseSuspect = Mission.SuspectBaseSpeed * suspectScale;
            float gap = Gap;

            float escape = 0f;
            if (gap > 7000f)
            {
                escape = mission_escape();
            }

            float closing = 1f + DifficultyRules.CatchUpAssist(Difficulty) * (gap > 9000f ? 1f : 0f);
            float modulate = 1f + 0.045f * MathF.Sin(_rivalPhase) + 0.02f * MathF.Sin(_rivalPhase * 2.7f);
            _rivalPhase += dt * 1.6f;

            RivalSpeed = baseSuspect * closing * modulate * (1f + escape);
            RivalZ += RivalSpeed * dt;

            float suspectGap = RivalZ - PlayerZ;
            RivalX = MathF.Sin(RivalPhase()) * 0.42f;

            _escapeWarnTimer -= dt;
            if (Escaping && _escapeWarnTimer <= 0f)
            {
                _escapeWarnTimer = 2.6f;
                Events.Add(new RaceEvent { Type = RaceEventType.SuspectEscaping });
            }

            // ---- ramming -------------------------------------------------------------
            if (_ramCooldown <= 0f && suspectGap < RamRange && suspectGap > -RamRange &&
                MathF.Abs(PlayerX - RivalX) < RamLateral && Speed > RivalSpeed * 0.82f)
            {
                RivalHealth = Math.Max(0, RivalHealth - 1);
                Rams++;
                int award = 320 + Rams * 80;
                RawScore += award;
                _shake = 1f;
                _ramCooldown = 0.55f;

                Speed = MathF.Max(2400f, RivalSpeed * 0.70f);
                RivalZ = PlayerZ + 1250f;
                Turbo = MathF.Min(1f, Turbo + 0.16f);
                Events.Add(new RaceEvent { Type = RaceEventType.Ram, Value = award });

                if (RivalHealth <= 0)
                {
                    int timeBonus = (int)MathF.Round(TimeRemaining) * 45;
                    int bustBonus = 4500 + Mission.Index * 750;
                    RawScore += bustBonus + timeBonus;
                    Busted = true;
                    Events.Add(new RaceEvent
                    {
                        Type = RaceEventType.Busted,
                        Value = bustBonus + timeBonus
                    });
                    return;
                }
            }

            // ---- checkpoints ---------------------------------------------------------
            float nextCheckpoint = Checkpoints == 0
                ? CheckpointSpacing
                : CheckpointSpacing * (Checkpoints + 1);
            if (PlayerZ >= nextCheckpoint)
            {
                Checkpoints++;
                TimeRemaining = MathF.Min(TotalTime + 30f, TimeRemaining + 14f);
                RawScore += 450 + Checkpoints * 120;
                Events.Add(new RaceEvent { Type = RaceEventType.Checkpoint, Value = 14 });
            }

            // ---- traffic -------------------------------------------------------------
            UpdateTraffic(dt);

            if (Speed > MaxSpeed * 0.65f)
            {
                Turbo = MathF.Min(1f, Turbo + dt * 0.03f);
            }
        }

        private float mission_escape()
        {
            return DifficultyRules.EscapeAggression(Difficulty) *
                   DifficultyRules.Clamp01((Gap - 7000f) / 12000f);
        }

        private float RivalPhase()
        {
            return RivalZ * 0.00018f + 0.8f;
        }

        private void UpdateTraffic(float dt)
        {
            float density = Mission.TrafficBase * DifficultyRules.TrafficDensity(Difficulty);
            _spawnTimer -= dt * density;

            int guard = 0;
            while (_spawnTimer <= 0f && Traffic.Count < 26 && guard++ < 4)
            {
                SpawnTraffic(PlayerZ + 17000f + (float)_random.NextDouble() * 9000f);
                _spawnTimer += 0.55f + (float)_random.NextDouble() * 0.4f;
            }

            for (int i = Traffic.Count - 1; i >= 0; i--)
            {
                TrafficCar car = Traffic[i];
                car.Z += car.Speed * dt;

                float dz = car.Z - PlayerZ;
                float dx = car.X - PlayerX;

                if (!car.Passed && dz < 0f && MathF.Abs(dx) < 0.62f && dz > -1400f)
                {
                    car.Passed = true;
                    NearMisses++;
                    RawScore += 55;
                    Events.Add(new RaceEvent { Type = RaceEventType.NearMiss, Value = 55 });
                }

                if (MathF.Abs(dz) < 430f && MathF.Abs(dx) < 0.33f)
                {
                    Speed *= 0.42f;
                    TimeRemaining = MathF.Max(0.1f, TimeRemaining - 1.8f);
                    TrafficHits++;
                    _shake = 1f;
                    Traffic.RemoveAt(i);
                    Events.Add(new RaceEvent { Type = RaceEventType.TrafficHit });
                    continue;
                }

                if (dz < -3200f || dz > 34000f)
                {
                    Traffic.RemoveAt(i);
                }
            }

            Traffic.Sort((a, b) => b.Z.CompareTo(a.Z));
        }

        private void SpawnTraffic(float z)
        {
            if (Traffic.Count >= 26)
            {
                return;
            }

            bool sameDirection = _random.Next(100) < 78;
            Traffic.Add(new TrafficCar
            {
                Z = z,
                X = -0.86f + (float)_random.NextDouble() * 1.72f,
                Speed = sameDirection
                    ? 1500f + (float)_random.NextDouble() * 3000f
                    : -(1800f + (float)_random.NextDouble() * 3000f),
                Kind = _random.Next(4),
                SameDirection = sameDirection
            });
        }

        /// <summary>Used by the renderer for collision smoke/spark placement.</summary>
        public TrafficCar NearestTraffic(out float distance)
        {
            TrafficCar best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Traffic.Count; i++)
            {
                float d = Traffic[i].Z - PlayerZ;
                if (d > 0f && d < bestDistance)
                {
                    bestDistance = d;
                    best = Traffic[i];
                }
            }

            distance = bestDistance;
            return best;
        }
    }
}
