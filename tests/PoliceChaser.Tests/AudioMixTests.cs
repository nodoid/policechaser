using PoliceChaser.Audio;
using Xunit;

namespace PoliceChaser.Tests
{
    public class AudioMixTests
    {
        [Fact]
        public void Crash_layers_fit_under_the_clipping_ceiling()
        {
            float stack = ChipAudio.Mix.Crash + ChipAudio.Mix.Screech + ChipAudio.Mix.Squelch;
            Assert.True(stack <= 1f, "crash stack peaks at " + stack);
        }

        [Fact]
        public void Music_and_engine_sit_below_the_effects()
        {
            Assert.True(ChipAudio.Mix.Music < ChipAudio.Mix.Crash);
            Assert.True(ChipAudio.Mix.EngineMax < ChipAudio.Mix.Ram);
            Assert.True(ChipAudio.Mix.Siren < ChipAudio.Mix.EngineMax);
            Assert.True(ChipAudio.Mix.Squelch < ChipAudio.Mix.Gunshot);
        }
    }
}
