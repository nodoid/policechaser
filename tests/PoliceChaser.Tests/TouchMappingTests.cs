using Microsoft.Xna.Framework;
using PoliceChaser.Graphics;
using Xunit;

namespace PoliceChaser.Tests
{
    public class TouchMappingTests
    {
        // iPhone 13 in landscape, as measured on device: the back buffer and window are
        // 2532x1170 while MonoGame reports touches in a 1280x960 display space.
        private const float DisplayWidth = 1280f;
        private const float DisplayHeight = 960f;
        private const float WindowWidth = 2532f;
        private const float WindowHeight = 1170f;

        [Theory]
        [InlineData(36f, 201f)]   // left pad
        [InlineData(104f, 201f)]  // right pad
        [InlineData(219f, 212f)]  // brake
        [InlineData(282f, 201f)]  // turbo
        public void Pads_map_back_to_their_logical_positions_on_iphone(float logicalX, float logicalY)
        {
            Vector2 screen = PictureToWindow(logicalX, logicalY);
            Vector2 reported = new Vector2(
                screen.X * DisplayWidth / WindowWidth,
                screen.Y * DisplayHeight / WindowHeight);

            Vector2 mapped = Map(reported);

            Assert.InRange(mapped.X, logicalX - 0.75f, logicalX + 0.75f);
            Assert.InRange(mapped.Y, logicalY - 0.75f, logicalY + 0.75f);
        }

        [Fact]
        public void Managed_touch_reports_are_not_shifted_on_desktop()
        {
            Vector2 mapped = TouchMapping.UndoTouchScale(
                new Vector2(416f, 804f), 1280f, 960f, 1280f, 960f);

            Assert.Equal(416f, mapped.X, 3);
            Assert.Equal(804f, mapped.Y, 3);
        }

        [Fact]
        public void Typewriter_letter_boxes_are_reachable_on_iphone()
        {
            // The UP key used for name entry, in logical layout units.
            Vector2 screen = PictureToWindow(160f, 86f);
            Vector2 reported = new Vector2(
                screen.X * DisplayWidth / WindowWidth,
                screen.Y * DisplayHeight / WindowHeight);
            Vector2 mapped = Map(reported);

            Assert.InRange(mapped.X, 159f, 161f);
            Assert.InRange(mapped.Y, 85f, 87f);
        }

        private static Vector2 Map(Vector2 reported)
        {
            Vector2 screen = TouchMapping.UndoTouchScale(
                reported, DisplayWidth, DisplayHeight, WindowWidth, WindowHeight);

            return TouchMapping.ToLogical(screen, WindowWidth, WindowHeight);
        }

        /// <summary>Where a logical point is drawn inside the letterboxed picture on the phone.</summary>
        private static Vector2 PictureToWindow(float logicalX, float logicalY)
        {
            float scale = System.MathF.Min(WindowWidth / 1280f, WindowHeight / 960f);
            float width = 1280f * scale;
            float height = 960f * scale;
            float offsetX = (WindowWidth - width) / 2f;
            float offsetY = (WindowHeight - height) / 2f;

            return new Vector2(
                offsetX + logicalX * (width / 320f),
                offsetY + logicalY * (height / 240f));
        }
    }
}
