using System;

namespace PoliceChaser.Game
{
    /// <summary>Three skill levels. Beginner is tuned to the difficulty of the 8-bit original.</summary>
    public enum Difficulty
    {
        Beginner = 0,
        Medium = 1,
        Hard = 2
    }

    /// <summary>
    /// Every rule that varies with skill level lives here so it can be unit tested without a
    /// graphics device. Score weighting is part of the spec: a Medium run is worth half the
    /// Beginner score for the same driving, and Hard is worth a fifth.
    /// </summary>
    public static class DifficultyRules
    {
        public static readonly Difficulty[] All =
        {
            Difficulty.Beginner, Difficulty.Medium, Difficulty.Hard
        };

        public static string Name(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Medium: return "MEDIUM";
                case Difficulty.Hard: return "HARD";
                default: return "BEGINNER";
            }
        }

        public static string Blurb(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Medium:
                    return "Faster suspects. More traffic. Half points.";
                case Difficulty.Hard:
                    return "Ruthless. Heavy traffic. A fifth of the points.";
                default:
                    return "The original ride. Full points.";
            }
        }

        /// <summary>1x for beginner, 2x for medium, 5x for hard.</summary>
        public static int ScoreDivisor(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Medium: return 2;
                case Difficulty.Hard: return 5;
                default: return 1;
            }
        }

        /// <summary>Applies the skill weighting to a raw (beginner-equivalent) score.</summary>
        public static int Weight(Difficulty difficulty, int rawScore)
        {
            if (rawScore < 0)
            {
                rawScore = 0;
            }

            return rawScore / ScoreDivisor(difficulty);
        }

        /// <summary>Multiplier for the speed the suspect car can sustain.</summary>
        public static float SuspectSpeedScale(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Medium: return 1.06f;
                case Difficulty.Hard: return 1.14f;
                default: return 1.0f;
            }
        }

        /// <summary>Multiplier for the rate the pursuit clock burns down.</summary>
        public static float TimeDrain(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Medium: return 1.15f;
                case Difficulty.Hard: return 1.35f;
                default: return 1.0f;
            }
        }

        /// <summary>Multiplier for traffic density. Hard throws a lot more cars at you.</summary>
        public static float TrafficDensity(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Medium: return 1.15f;
                case Difficulty.Hard: return 1.25f;
                default: return 1.0f;
            }
        }

        /// <summary>How hard the suspect car pulls away when it is ahead.</summary>
        public static float EscapeAggression(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Medium: return 0.14f;
                case Difficulty.Hard: return 0.08f;
                default: return 0.06f;
            }
        }

        /// <summary>Rubber banding that helps a beginner close a gap. Never helps on hard.</summary>
        public static float CatchUpAssist(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Medium: return 0.16f;
                case Difficulty.Hard: return 0f;
                default: return 0.3f;
            }
        }

        /// <summary>The time limit for one pursuit, in seconds, before skill drain is applied.</summary>
        public static float BaseTime(Difficulty difficulty, float missionSeconds)
        {
            switch (difficulty)
            {
                case Difficulty.Medium: return missionSeconds * 0.92f;
                case Difficulty.Hard: return missionSeconds * 0.84f;
                default: return missionSeconds;
            }
        }

        /// <summary>
        /// How many traffic crashes the car can survive. Hard mode throws 1.8x the traffic, so it
        /// carries one spare plate to stay winnable.
        /// </summary>
        public static int HullPoints(Difficulty difficulty)
        {
            return difficulty == Difficulty.Hard ? 6 : 4;
        }

        /// <summary>
        /// Seconds lost to a traffic crash. Hard's clock already burns 35% faster, so its crash
        /// penalty is trimmed rather than stacked on top of the drain.
        /// </summary>
        public static float CrashPenalty(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Medium: return 2.2f;
                case Difficulty.Hard: return 1.8f;
                default: return 2.5f;
            }
        }

        public static float Clamp01(float value)
        {
            return MathF.Max(0f, MathF.Min(1f, value));
        }
    }
}
