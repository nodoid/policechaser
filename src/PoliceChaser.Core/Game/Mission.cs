using System;
using System.Collections.Generic;

namespace PoliceChaser.Game
{
    /// <summary>Visual theme of a mission. Drives palette and roadside scenery.</summary>
    public enum MissionTheme
    {
        Harbour,
        Coast,
        Alpine,
        Neon,
        Desert,
        Arctic,
        Industrial,
        Highway,
        Canyon
    }

    public enum Weather
    {
        Clear,
        Rain,
        Snow,
        Sandstorm
    }

    /// <summary>
    /// One pursuit. These are an interpretation of the 8-bit chase format rather than a copy of
    /// it: the original stages are replaced with original locations and suspects. Series I ends
    /// with three new missions; Series II is an original sequel tour whose suspects are armed and
    /// shoot back at the player.
    /// </summary>
    public sealed class Mission
    {
        public string Id { get; }
        public string Title { get; }
        public string Tagline { get; }
        public string SuspectName { get; }
        public string SuspectCrime { get; }
        public MissionTheme Theme { get; }
        public Weather Weather { get; }
        public float LengthUnits { get; }
        public float BaseTimeSeconds { get; }
        public int RequiredRams { get; }
        public float SuspectBaseSpeed { get; }
        public float TrafficBase { get; }
        public int SuspectColor { get; }
        public bool IsNewMission { get; }

        /// <summary>1 for the original tour, 2 for the Series II sequel tour.</summary>
        public int Series { get; }

        /// <summary>Series II suspects fire at the player, who has a hull that can be wrecked.</summary>
        public bool SuspectArmed { get; }

        public Mission(
            string id,
            string title,
            string tagline,
            string suspectName,
            string suspectCrime,
            MissionTheme theme,
            Weather weather,
            float lengthUnits,
            float baseTimeSeconds,
            int requiredRams,
            float suspectBaseSpeed,
            float trafficBase,
            int suspectColor,
            bool isNewMission,
            int series = 1,
            bool suspectArmed = false)
        {
            Id = id;
            Title = title;
            Tagline = tagline;
            SuspectName = suspectName;
            SuspectCrime = suspectCrime;
            Theme = theme;
            Weather = weather;
            LengthUnits = lengthUnits;
            BaseTimeSeconds = baseTimeSeconds;
            RequiredRams = requiredRams;
            SuspectBaseSpeed = suspectBaseSpeed;
            TrafficBase = trafficBase;
            SuspectColor = suspectColor;
            IsNewMission = isNewMission;
            Series = series;
            SuspectArmed = suspectArmed;
        }

        /// <summary>Mission list in campaign order.</summary>
        public static IReadOnlyList<Mission> All { get; } = new[]
        {
            // ---- Series I ------------------------------------------------------------
            new Mission(
                "harbour",
                "HARBOUR PURSUIT",
                "A courier runner is heading for the dock gates.",
                "NIXON",
                "Armed robbery",
                MissionTheme.Harbour,
                Weather.Clear,
                82000f,
                82f,
                3,
                9400f,
                0.45f,
                0,
                false),

            new Mission(
                "coast",
                "SUNSET BOULEVARD",
                "The getaway car is racing the sun along the coast road.",
                "VEGA",
                "Car theft ring",
                MissionTheme.Coast,
                Weather.Clear,
                88000f,
                84f,
                3,
                9800f,
                0.5f,
                1,
                false),

            new Mission(
                "alpine",
                "ALPINE PASS",
                "A smuggler is running the high pass before the snow closes it.",
                "KESSLER",
                "Contraband smuggling",
                MissionTheme.Alpine,
                Weather.Snow,
                90000f,
                86f,
                4,
                9900f,
                0.55f,
                2,
                false),

            new Mission(
                "neon",
                "NEON GRID",
                "Data thieves are loose in the arcade district.",
                "GLITCH",
                "Corporate data theft",
                MissionTheme.Neon,
                Weather.Rain,
                94000f,
                88f,
                4,
                10100f,
                0.7f,
                3,
                true),

            new Mission(
                "desert",
                "DUNE RUNNER",
                "A hijacked rig is tearing across the salt flats.",
                "SABLE",
                "Hijacking",
                MissionTheme.Desert,
                Weather.Sandstorm,
                96000f,
                90f,
                4,
                10300f,
                0.6f,
                4,
                true),

            new Mission(
                "arctic",
                "FROSTLINE",
                "The last leg: a blizzard run for the border.",
                "MORROW",
                "Escaped convict",
                MissionTheme.Arctic,
                Weather.Snow,
                100000f,
                92f,
                5,
                10500f,
                0.65f,
                5,
                true),

            // ---- Series II: the sequel tour. Armed suspects, new territory. ----------
            new Mission(
                "steelworks",
                "IRONWORKS",
                "A gun runner is collecting his cargo from the old foundry.",
                "VOLT",
                "Arms trafficking",
                MissionTheme.Industrial,
                Weather.Rain,
                104000f,
                96f,
                5,
                10700f,
                0.75f,
                1,
                true,
                2,
                true),

            new Mission(
                "interstate",
                "INTERSTATE 9",
                "An armoured car crew is running the state line at speed.",
                "RIPPER",
                "Armoured car heist",
                MissionTheme.Highway,
                Weather.Clear,
                108000f,
                98f,
                5,
                10900f,
                0.85f,
                2,
                true,
                2,
                true),

            new Mission(
                "canyon",
                "RED CANYON",
                "A kidnapping suspect is heading for the desert rim.",
                "CROW",
                "Kidnapping",
                MissionTheme.Canyon,
                Weather.Sandstorm,
                112000f,
                100f,
                6,
                11100f,
                0.8f,
                4,
                true,
                2,
                true)
        };

        public static Mission ById(string id)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (string.Equals(All[i].Id, id, StringComparison.Ordinal))
                {
                    return All[i];
                }
            }

            return null;
        }

        public int Index
        {
            get
            {
                for (int i = 0; i < All.Count; i++)
                {
                    if (ReferenceEquals(All[i], this))
                    {
                        return i;
                    }
                }

                return 0;
            }
        }

        /// <summary>Missions launched after the original three are flagged as new content.</summary>
        public static int NewMissionCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < All.Count; i++)
                {
                    if (All[i].IsNewMission)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Number of missions in the Series II sequel tour.</summary>
        public static int SeriesTwoCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < All.Count; i++)
                {
                    if (All[i].Series == 2)
                    {
                        count++;
                    }
                }

                return count;
            }
        }
    }
}
