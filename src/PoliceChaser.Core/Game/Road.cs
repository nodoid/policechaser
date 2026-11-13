using System;
using System.Collections.Generic;

namespace PoliceChaser.Game
{
    public struct RoadSegment
    {
        public float Curve;
        public float Y;
    }

    public enum PropKind
    {
        Tree,
        Palm,
        Pine,
        Cactus,
        Building,
        Lamp,
        Sign,
        Rock,
        SnowPole,
        Billboard
    }

    public struct RoadProp
    {
        public int Segment;
        public float X;
        public PropKind Kind;
        public int Variant;
    }

    /// <summary>
    /// A procedural pseudo-3D road. Segments are evenly spaced along Z and carry a curve and an
    /// elevation, exactly like the scanline racers of the era, but the layout is generated per
    /// mission/difficulty/seed so every pursuit is a different stretch of road.
    /// </summary>
    public sealed class Road
    {
        public const float SegmentLength = 200f;
        public const int SegmentCount = 1400;

        private readonly List<RoadSegment> _segments;
        private readonly List<RoadProp> _props;

        public Road(List<RoadSegment> segments, List<RoadProp> props)
        {
            _segments = segments;
            _props = props;
        }

        public int Count => _segments.Count;
        public IReadOnlyList<RoadSegment> Segments => _segments;
        public IReadOnlyList<RoadProp> Props => _props;

        public RoadSegment SegmentAt(float z)
        {
            if (z < 0f)
            {
                z = 0f;
            }

            int index = (int)(z / SegmentLength) % _segments.Count;
            return _segments[index];
        }

        public int IndexAt(float z)
        {
            if (z < 0f)
            {
                z = 0f;
            }

            return (int)(z / SegmentLength) % _segments.Count;
        }

        public float CurveAt(float z)
        {
            return SegmentAt(z).Curve;
        }

        public float YAt(float z)
        {
            return SegmentAt(z).Y;
        }

        /// <summary>Interpolated road height, used for the camera so hills are smooth.</summary>
        public float HeightAt(float z)
        {
            float clamped = z < 0f ? 0f : z;
            int index = (int)(clamped / SegmentLength) % _segments.Count;
            int next = (index + 1) % _segments.Count;
            float t = (clamped % SegmentLength) / SegmentLength;
            return _segments[index].Y + (_segments[next].Y - _segments[index].Y) * t;
        }

        public static Road Build(Mission mission, int seed)
        {
            var random = new Random(seed ^ (mission.Index * 7919) ^ 0x5EED);
            var segments = new List<RoadSegment>(SegmentCount + 256);
            var props = new List<RoadProp>(SegmentCount);

            float y = 0f;
            segments.Add(new RoadSegment { Curve = 0f, Y = 0f });

            while (segments.Count < SegmentCount)
            {
                int kind = random.Next(100);
                float curve;
                if (kind < 34)
                {
                    curve = 0f;
                }
                else if (kind < 64)
                {
                    curve = (random.Next(2) == 0 ? -1f : 1f) * (1.6f + (float)random.NextDouble() * 1.7f);
                }
                else if (kind < 86)
                {
                    curve = (random.Next(2) == 0 ? -1f : 1f) * (3.2f + (float)random.NextDouble() * 2.0f);
                }
                else
                {
                    curve = (random.Next(2) == 0 ? -1f : 1f) * (5.0f + (float)random.NextDouble() * 1.6f);
                }

                int length = 34 + random.Next(70);
                float deltaY = 0f;
                if (random.Next(100) < 46)
                {
                    deltaY = ((float)random.NextDouble() * 2f - 1f) * 2600f;
                }

                AppendSection(segments, length, curve, y, y + deltaY);
                y += deltaY;
            }

            BuildProps(mission, random, props);

            return new Road(segments, props);
        }

        private static void AppendSection(List<RoadSegment> segments, int length, float curve, float y0, float y1)
        {
            for (int i = 1; i <= length; i++)
            {
                float t = i / (float)length;
                // Smooth ramp in and out of a bend so the steering load eases rather than snaps.
                float shape = 0.5f - 0.5f * MathF.Cos(t * MathF.PI * 2f);
                float curveNow = curve * shape;
                float smooth = t * t * (3f - 2f * t);
                segments.Add(new RoadSegment
                {
                    Curve = curveNow,
                    Y = y0 + (y1 - y0) * smooth
                });
            }
        }

        private static void BuildProps(Mission mission, Random random, List<RoadProp> props)
        {
            PropKind nearKind;
            PropKind farKind;
            switch (mission.Theme)
            {
                case MissionTheme.Coast:
                    nearKind = PropKind.Palm;
                    farKind = PropKind.Palm;
                    break;
                case MissionTheme.Alpine:
                    nearKind = PropKind.Pine;
                    farKind = PropKind.Rock;
                    break;
                case MissionTheme.Neon:
                    nearKind = PropKind.Building;
                    farKind = PropKind.Billboard;
                    break;
                case MissionTheme.Desert:
                    nearKind = PropKind.Cactus;
                    farKind = PropKind.Rock;
                    break;
                case MissionTheme.Arctic:
                    nearKind = PropKind.SnowPole;
                    farKind = PropKind.Pine;
                    break;
                default:
                    nearKind = PropKind.Building;
                    farKind = PropKind.Lamp;
                    break;
            }

            int index = 6;
            while (index < SegmentCount - 8)
            {
                int step = 4 + random.Next(7);
                index += step;

                bool left = random.Next(2) == 0;
                float side = left ? -1f : 1f;
                float offset = 1.5f + (float)random.NextDouble() * 2.6f;

                // Large scenery sits further back so it can never cover the carriageway.
                if (nearKind == PropKind.Building)
                {
                    offset = 3.4f + (float)random.NextDouble() * 2.4f;
                }

                props.Add(new RoadProp
                {
                    Segment = index,
                    X = side * offset,
                    Kind = nearKind,
                    Variant = random.Next(4)
                });

                if (random.Next(100) < 40)
                {
                    float farOffset = 2.0f + (float)random.NextDouble() * 2.5f;
                    if (farKind == PropKind.Building || farKind == PropKind.Billboard)
                    {
                        farOffset += 1.6f;
                    }

                    props.Add(new RoadProp
                    {
                        Segment = index + 2,
                        X = -side * farOffset,
                        Kind = farKind,
                        Variant = random.Next(4)
                    });
                }

                if (random.Next(100) < 8)
                {
                    props.Add(new RoadProp
                    {
                        Segment = index + 5,
                        X = side * 1.25f,
                        Kind = PropKind.Sign,
                        Variant = random.Next(3)
                    });
                }

                if (mission.Theme == MissionTheme.Harbour && random.Next(100) < 22)
                {
                    props.Add(new RoadProp
                    {
                        Segment = index + 1,
                        X = side * (3.4f + (float)random.NextDouble()),
                        Kind = PropKind.Billboard,
                        Variant = random.Next(3)
                    });
                }
            }
        }
    }
}
