using PoliceChaser.Game;
using Xunit;

namespace PoliceChaser.Tests
{
    public class SettingsTests
    {
        [Fact]
        public void Volumes_are_clamped_and_snapped_to_ten_steps()
        {
            Assert.Equal(1f, GameSettings.Clamp(1.7f));
            Assert.Equal(0f, GameSettings.Clamp(-0.4f));
            Assert.Equal(0.7f, GameSettings.Clamp(0.73f), 3);
        }

        [Fact]
        public void Settings_survive_a_restart()
        {
            var file = new MemorySettingsFile();
            var first = new SettingsStore(file);
            first.SetMusic(0.4f);
            first.SetEffects(0.8f);

            var second = new SettingsStore(file);
            Assert.Equal(0.4f, second.Value.Music, 3);
            Assert.Equal(0.8f, second.Value.Effects, 3);
        }

        [Fact]
        public void A_corrupt_settings_file_falls_back_to_defaults()
        {
            var store = new SettingsStore(new MemorySettingsFile { Contents = "{ not json" });
            Assert.Equal(1f, store.Value.Music, 3);
            Assert.Equal(1f, store.Value.Effects, 3);
        }
    }
}
