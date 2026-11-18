using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace PoliceChaser.Android
{
    [Activity(
        Label = "@string/app_name",
        MainLauncher = true,
        Icon = "@mipmap/icon",
        Theme = "@style/PoliceChaserTheme",
        AlwaysRetainTaskState = true,
        LaunchMode = LaunchMode.SingleInstance,
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard |
                               ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize |
                               ConfigChanges.UiMode | ConfigChanges.Density)]
    public class Activity1 : AndroidGameActivity
    {
        private ChaseGame _game;
        private View _view;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            _game = new ChaseGame();

            var metrics = Resources?.DisplayMetrics;
            if (metrics != null)
            {
                _game.SetBackBuffer(metrics.WidthPixels, metrics.HeightPixels);
            }

            // Draw under the camera cutout too: the widened canvas keeps the picture's centre
            // and the HUD clear of it.
            if (Window?.Attributes != null)
            {
                Window.Attributes.LayoutInDisplayCutoutMode = LayoutInDisplayCutoutMode.Always;
            }

            _view = _game.Services.GetService(typeof(View)) as View;

            // Android 15+ draws edge to edge, so the game surface is attached with match-parent
            // layout params; otherwise MonoGame sizes it to the 4:3 backbuffer and leaves the
            // rest of a wide phone screen black.
            SetContentView(_view, new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                Window?.SetDecorFitsSystemWindows(false);
                HideSystemBars();
            }

            _game.Run();
        }

        /// <summary>Re-hides the status and navigation bars after a swipe or a focus change.</summary>
        private void HideSystemBars()
        {
            Window?.InsetsController?.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            if (hasFocus)
            {
                HideSystemBars();
            }
        }

        protected override void OnPause()
        {
            base.OnPause();
            _game?.PauseAudio();
        }

        protected override void OnResume()
        {
            base.OnResume();
            _game?.ResumeAudio();
        }
    }
}
