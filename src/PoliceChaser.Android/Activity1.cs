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
            _view = _game.Services.GetService(typeof(View)) as View;

            SetContentView(_view);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                Window?.SetDecorFitsSystemWindows(false);
                Window?.InsetsController?.Hide(WindowInsets.Type.StatusBars() | WindowInsets.Type.NavigationBars());
            }

            _game.Run();
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
