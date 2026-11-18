using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using PoliceChaser.Audio;
using PoliceChaser.Game;
using PoliceChaser.Graphics;

namespace PoliceChaser
{
    public enum GameScreen
    {
        Splash,
        Title,
        DifficultySelect,
        MissionSelect,
        Briefing,
        Racing,
        Busted,
        MissionCleared,
        CampaignComplete,
        GameOver,
        NameEntry,
        HighScores,
        Settings,
        Paused
    }

    /// <summary>
    /// The whole game. Art is baked at 4x and drawn to a 1280x960 render target that is smoothly
    /// scaled to fit any window or device, so the presentation is high resolution everywhere.
    /// </summary>
    public sealed class ChaseGame : Microsoft.Xna.Framework.Game
    {
        public const int VirtualWidth = RenderSettings.LogicalWidth;
        public const int VirtualHeight = RenderSettings.LogicalHeight;
        private const int S = RenderSettings.Scale;

#if ANDROID || IOS
        private const bool MobilePlatform = true;
#else
        private const bool MobilePlatform = false;
#endif

        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private RenderTarget2D _target;
        private int _canvasWidth = RenderSettings.Width;
        private int _canvasHeight = RenderSettings.Height;
        private int _originX;
        private bool _wide;
        private Texture2D _white;
        private GameFont _font;
        private Art _art;
        private ChipAudio _audio;
        private HighScoreStore _scores;
        private readonly RoadRenderer _roadRenderer = new RoadRenderer();
        private readonly List<WorldSprite> _worldSprites = new List<WorldSprite>();
        private readonly Random _random = new Random(20261113);

        private GameScreen _screen = GameScreen.Splash;
        private float _screenTime;
        private float _time;

        private Difficulty _difficulty = Difficulty.Beginner;
        private int _difficultyCursor;
        private int _missionCursor;
        private Mission _mission;
        private Race _race;
        private int _campaignIndex;
        private int _campaignRaw;
        private bool _scoreSubmitted;
        private int _finalScore;
        private ScoreEntry _pendingEntry;
        private int _highlightIndex = -1;
        private int _lastRank;
        private SettingsStore _settings;
        private int _settingsRow;
        private string _lastName = "PC1";
        private int _nameCursor;
        private char[] _nameLetters = { 'P', 'C', '1' };

        private float _countdown;
        private int _lastCountdownSecond = -1;
        private float _raceEndTimer;
        private string _radioText = "";
        private float _radioTimer;
        private float _radioFlash;
        private float _skyOffset;
        private float _shake;
        private float _lastSpeedRatio;
        private float _gearFlash;
        private bool _paused;

        private InputFrame _input;
        private bool _PointerSeen;
        private bool _mouseWasDown;
        private bool _gearKeyWasDown;
        private bool _gearPadWasDown;
        private TouchCollection _previousTouches;
        private bool _selfTest;
        private string _selfTestScreen = "title";
        private string _selfTestPath = "selftest.png";
        private int _selfTestFrames = 90;
        private int _selfTestFrame;
        private bool _selfTestTapped;
        private bool _selfTestCaptured;

        private struct InputFrame
        {
            public float Steer;
            public bool Up;
            public bool Down;
            public bool Left;
            public bool Right;
            public bool Confirm;
            public bool Back;
            public bool Pause;
            public bool Turbo;
            public bool Brake;
            public bool Gear;
            public Vector2 Pointer;
            public bool PointerDown;
            public bool PointerPressed;
        }

        public ChaseGame()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1280,
                PreferredBackBufferHeight = 960,
                SynchronizeWithVerticalRetrace = true,
                IsFullScreen = false,
                GraphicsProfile = GraphicsProfile.Reach
            };

            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            IsFixedTimeStep = true;
            TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
            Window.AllowUserResizing = true;
            Window.Title = "Police Chaser";
        }

        /// <summary>
        /// Gives the Android host the real display size. MonoGame otherwise keeps the 4:3
        /// preferred backbuffer, sizes the surface to it and leaves the rest of a wide phone
        /// screen black. Must be called before Run().
        /// </summary>
        public void SetBackBuffer(int width, int height)
        {
            if (width <= 0 || height <= 0)
            {
                return;
            }

            _graphics.PreferredBackBufferWidth = Math.Max(width, height);
            _graphics.PreferredBackBufferHeight = Math.Min(width, height);
        }

        /// <summary>
        /// Makes sure the render target matches the canvas for the current display, recreating
        /// it only when the display aspect has changed (rotation, split screen, foldable).
        /// </summary>
        private void EnsureCanvas()
        {
            UpdateCanvas();

            if (_target == null || _target.Width != _canvasWidth || _target.Height != _canvasHeight)
            {
                _target?.Dispose();
                _target = new RenderTarget2D(GraphicsDevice, _canvasWidth, _canvasHeight, false,
                    SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            }
        }

        /// <summary>
        /// Chooses the canvas the game is drawn into. On a phone it is widened to the display's
        /// aspect with the authored 320x240 picture centred inside it, so the world reaches the
        /// edges of the screen instead of sitting between letterbox bars; desktop windows and
        /// portrait displays keep the plain 4:3 canvas.
        /// </summary>
        private void UpdateCanvas()
        {
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;

            if (!MobilePlatform || width <= 0 || height <= 0 || width <= height)
            {
                SetCanvas(RenderSettings.Width, RenderSettings.Height);
                return;
            }

            int canvas = (int)MathF.Round(RenderSettings.Height * width / (float)height);
            canvas = Math.Clamp(canvas, RenderSettings.Width, RenderSettings.Width * 2);

            // A multiple of 16 keeps the centred picture on whole logical pixels so layout and
            // hit testing never fall between them.
            canvas = Math.Min(RenderSettings.Width * 2, (canvas + 15) & ~15);
            SetCanvas(canvas, RenderSettings.Height);
        }

        private void SetCanvas(int width, int height)
        {
            _canvasWidth = width;
            _canvasHeight = height;
            _originX = (width - RenderSettings.Width) / 2;
            _wide = width > RenderSettings.Width;
        }

        /// <summary>Left edge of the 320-wide layout inside the canvas, in logical units.</summary>
        private int OriginLogical => _originX / S;

        /// <summary>The whole canvas in logical units, including the mobile side margins.</summary>
        private Rectangle CanvasLogical => new Rectangle(-OriginLogical, 0, _canvasWidth / S, _canvasHeight / S);

        protected override void Initialize()
        {
            Window.Title = "Police Chaser";
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            EnsureCanvas();

            _white = new Texture2D(GraphicsDevice, 1, 1);
            _white.SetData(new[] { Color.White });

            _font = new GameFont();
            _art = new Art(GraphicsDevice);
            _audio = new ChipAudio();
            _settings = new SettingsStore(new LocalSettingsFile(LocalHighScoreFile.DefaultRoot));
            _scores = new HighScoreStore(new LocalHighScoreFile(LocalHighScoreFile.DefaultRoot));
            ApplySettings();

            TouchPanel.EnableMouseTouchPoint = true;
            TouchPanel.EnableMouseGestures = false;

            if (_selfTest)
            {
                // Never let a smoke test touch (or pollute) the player's real table.
                _scores = new HighScoreStore(new MemoryHighScoreFile());
                _settings = new SettingsStore(new MemorySettingsFile());
                ApplySettings();
                SeedSelfTestScores();
                PrepareSelfTest();
            }


            base.LoadContent();
        }

        protected override void UnloadContent()
        {
            _scores?.Save();
            _settings?.Save();
            _audio?.Dispose();
            _art?.Dispose();
            _font?.Dispose();
            _white?.Dispose();
            _target?.Dispose();
            base.UnloadContent();
        }

        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            _scores?.Save();
            base.OnExiting(sender, args);
        }

        /// <summary>Called by the mobile hosts when the app goes to the background.</summary>
        public void PauseAudio()
        {
            _audio?.PauseRaceAudio(true);
        }

        /// <summary>Called by the mobile hosts when the app comes back to the foreground.</summary>
        public void ResumeAudio()
        {
            _audio?.PauseRaceAudio(false);
        }

        /// <summary>
        /// Configuration hook for automated smoke tests: shows a specific screen, renders a
        /// number of frames, writes one PNG and exits. Used by the macOS and Windows build checks.
        /// </summary>
        public void EnableSelfTest(string screen, int frames, string shotPath)
        {
            _selfTest = true;
            _selfTestScreen = string.IsNullOrEmpty(screen) ? "title" : screen.ToLowerInvariant();
            _selfTestFrames = Math.Max(1, frames);
            _selfTestPath = string.IsNullOrEmpty(shotPath) ? "selftest.png" : shotPath;
        }

        // ===================================================================== update

        protected override void Update(GameTime gameTime)
        {
            try
            {
                UpdateFrame(gameTime);
            }
            catch (Exception ex)
            {
                WriteCrash("Update", ex);
                throw;
            }
        }

        /// <summary>Writes the exception to the app data folder so device crashes can be read back.</summary>
        private void WriteCrash(string where, Exception ex)
        {
            try
            {
                string root = LocalHighScoreFile.DefaultRoot;
                System.IO.Directory.CreateDirectory(root);
                System.IO.File.WriteAllText(System.IO.Path.Combine(root, "policechaser-crash.txt"),
                    DateTime.UtcNow.ToString("u") + " " + where + Environment.NewLine + ex);
            }
            catch (Exception)
            {
                // Diagnostics must never mask the original failure.
            }
        }

        private void UpdateFrame(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt > 0.1f)
            {
                dt = 0.1f;
            }

            _time += dt;
            _screenTime += dt;

            if (_selfTest)
            {
                UpdateSelfTest(dt);
                base.Update(gameTime);
                return;
            }

            ReadInput();
            UpdateRadio(dt);

            // A portrait window cannot show the 4:3 picture usefully; hold the game on a prompt
            // until the device or window is turned back to landscape.
            if (PortraitWindow)
            {
                base.Update(gameTime);
                return;
            }

            switch (_screen)
            {
                case GameScreen.Splash:
                    UpdateSplash();
                    break;
                case GameScreen.Title:
                    UpdateTitle();
                    break;
                case GameScreen.Settings:
                    UpdateSettings();
                    break;
                case GameScreen.DifficultySelect:
                    UpdateDifficultySelect();
                    break;
                case GameScreen.MissionSelect:
                    UpdateMissionSelect();
                    break;
                case GameScreen.Briefing:
                    UpdateBriefing();
                    break;
                case GameScreen.Racing:
                    UpdateRacing(dt);
                    break;
                case GameScreen.Paused:
                    UpdatePaused();
                    break;
                case GameScreen.Busted:
                    UpdateBusted();
                    break;
                case GameScreen.MissionCleared:
                    UpdateMissionCleared();
                    break;
                case GameScreen.CampaignComplete:
                    UpdateCampaignComplete();
                    break;
                case GameScreen.GameOver:
                    UpdateGameOver();
                    break;
                case GameScreen.NameEntry:
                    UpdateNameEntry();
                    break;
                case GameScreen.HighScores:
                    UpdateHighScores();
                    break;
            }

            if (_audio != null)
            {
                _audio.UpdateRaceAudio(_lastSpeedRatio, _race?.OffRoad ?? false, _paused, _input.Turbo);
            }

            base.Update(gameTime);
        }

        private void SetScreen(GameScreen screen)
        {
            if (_screen == GameScreen.Racing && screen != GameScreen.Paused)
            {
                _audio.StopRaceAudio();
            }

            _screen = screen;
            _screenTime = 0;

            switch (screen)
            {
                case GameScreen.Splash:
                    _audio.StopMusic();
                    break;
                case GameScreen.Title:
                    _audio.PlayMusic(ChipAudio.TrackTitle);
                    break;
                case GameScreen.DifficultySelect:
                case GameScreen.MissionSelect:
                case GameScreen.Briefing:
                case GameScreen.Settings:
                    _audio.PlayMusic(ChipAudio.TrackTitle);
                    break;
                case GameScreen.Racing:
                    _audio.PlayMusic(ChipAudio.TrackRace);
                    _audio.StartRaceEngine();
                    _paused = false;
                    break;
                case GameScreen.Paused:
                    _audio.PauseRaceAudio(true);
                    break;
                case GameScreen.Busted:
                    _audio.PlayMusic(ChipAudio.TrackBusted, false);
                    break;
                case GameScreen.MissionCleared:
                    _audio.PlayMusic(ChipAudio.TrackHighScore);
                    break;
                case GameScreen.CampaignComplete:
                case GameScreen.GameOver:
                    _audio.StopRaceAudio();
                    _audio.PlayMusic(ChipAudio.TrackGameOver, false);
                    break;
                case GameScreen.NameEntry:
                case GameScreen.HighScores:
                    _audio.PlayMusic(ChipAudio.TrackHighScore);
                    break;
            }
        }

        private void UpdateSplash()
        {
            if (_screenTime > 3.8f || (_screenTime > 0.8f && _input.Confirm))
            {
                SetScreen(GameScreen.Title);
            }
        }

        private void UpdateTitle()
        {
            if (_input.PointerPressed && SettingsButton.Contains(_input.Pointer.ToPoint()))
            {
                _audio.Play(_audio.Select);
                SetScreen(GameScreen.Settings);
                return;
            }

            if (_input.PointerPressed && new Rectangle(196, 182, 124, 34).Contains(_input.Pointer.ToPoint()))
            {
                _highlightIndex = -1;
                _audio.Play(_audio.Select);
                SetScreen(GameScreen.HighScores);
                return;
            }

            if (_input.Confirm)
            {
                _audio.Play(_audio.Select);
                _difficultyCursor = (int)_difficulty;
                SetScreen(GameScreen.DifficultySelect);
            }
        }

        // ------------------------------------------------------------------ settings

        private static Rectangle SettingsButton => new Rectangle(96, 206, 100, 24);
        private static Rectangle SettingsDone => new Rectangle(120, 198, 80, 26);
        private static Rectangle SettingsGearRow => new Rectangle(20, 152, 280, 40);
        private static Rectangle SettingsAuto => new Rectangle(112, 158, 78, 24);
        private static Rectangle SettingsManual => new Rectangle(198, 158, 78, 24);
        private static Rectangle SettingsRow(int row) => new Rectangle(20, 68 + row * 42, 280, 40);
        private static Rectangle SettingsMinus(int row) => new Rectangle(96, 76 + row * 42, 24, 22);
        private static Rectangle SettingsPlus(int row) => new Rectangle(232, 76 + row * 42, 24, 22);
        private static Rectangle SettingsBar(int row) => new Rectangle(126, 78 + row * 42, 100, 18);

        private void ApplySettings()
        {
            _audio.MusicVolume = _settings.Value.Music;
            _audio.EffectsVolume = _settings.Value.Effects;
            _audio.ApplyMusicVolume();
        }

        private void UpdateSettings()
        {
            bool touchUi = MobilePlatform || _PointerSeen;

            if (touchUi && _input.PointerPressed)
            {
                Point point = _input.Pointer.ToPoint();

                for (int row = 0; row < 2; row++)
                {
                    if (SettingsMinus(row).Contains(point))
                    {
                        _settingsRow = row;
                        AdjustVolume(-0.1f);
                        _input.Confirm = false;
                        return;
                    }

                    if (SettingsPlus(row).Contains(point))
                    {
                        _settingsRow = row;
                        AdjustVolume(0.1f);
                        _input.Confirm = false;
                        return;
                    }

                    if (SettingsRow(row).Contains(point))
                    {
                        _settingsRow = row;
                        _audio.Play(_audio.Select);
                        _input.Confirm = false;
                        return;
                    }
                }

                if (SettingsGearRow.Contains(point))
                {
                    _settingsRow = 2;
                    _audio.Play(_audio.Select);
                    _input.Confirm = false;
                    return;
                }

                if (SettingsAuto.Contains(point))
                {
                    _settingsRow = 2;
                    SetGearbox(false);
                    _input.Confirm = false;
                    return;
                }

                if (SettingsManual.Contains(point))
                {
                    _settingsRow = 2;
                    SetGearbox(true);
                    _input.Confirm = false;
                    return;
                }

                if (SettingsDone.Contains(point))
                {
                    _audio.Play(_audio.Select);
                    SetScreen(GameScreen.Title);
                    return;
                }

                _input.Confirm = false;
                return;
            }

            if (_input.Up)
            {
                _settingsRow = (_settingsRow + 2) % 3;
                _audio.Play(_audio.Select);
            }

            if (_input.Down)
            {
                _settingsRow = (_settingsRow + 1) % 3;
                _audio.Play(_audio.Select);
            }

            if (_input.Left || _input.Right)
            {
                if (_settingsRow < 2)
                {
                    AdjustVolume(_input.Right ? 0.1f : -0.1f);
                }
                else
                {
                    SetGearbox(!_settings.Value.ManualGears);
                }
            }

            if (_input.Confirm || _input.Back)
            {
                _audio.Play(_audio.Select);
                SetScreen(GameScreen.Title);
            }
        }

        private void AdjustVolume(float delta)
        {
            if (_settingsRow == 0)
            {
                _settings.SetMusic(_audio.MusicVolume + delta);
                _audio.MusicVolume = _settings.Value.Music;
                _audio.ApplyMusicVolume();
                return;
            }

            _settings.SetEffects(_audio.EffectsVolume + delta);
            _audio.EffectsVolume = _settings.Value.Effects;
            _audio.Play(_audio.Select);
        }

        private void DrawSettings()
        {
            Background(MissionTheme.Neon);
            SirenStrip(0);

            Panel(16, 22, 288, 206, new Color(8, 10, 26, 240), new Color(250, 210, 80));
            TextShadow("SETTINGS", 160, 30, new Color(250, 220, 120), 2);
            TextCentered("AUDIO", 160, 54, new Color(140, 150, 185));

            DrawVolumeRow(0, "MUSIC", _audio.MusicVolume);
            DrawVolumeRow(1, "EFFECTS", _audio.EffectsVolume);
            DrawGearRow();

            TextCentered("UP/DOWN SELECT - LEFT/RIGHT ADJUST", 160, 190, new Color(150, 170, 210));
            DrawKey(SettingsDone, "DONE");
        }

        private void DrawVolumeRow(int row, string label, float value)
        {
            int y = 76 + row * 42;
            bool selected = _settingsRow == row;

            Panel(24, y - 8, 272, 36,
                selected ? new Color(24, 32, 66) : new Color(10, 14, 28),
                selected ? new Color(250, 210, 80) : new Color(50, 60, 90));

            Text(label, 34, y + 2, selected ? Color.White : new Color(190, 200, 220));
            DrawKey(SettingsMinus(row), "-");
            DrawKey(SettingsPlus(row), "+");

            Rectangle bar = SettingsBar(row);
            Rect(bar.X, bar.Y, bar.Width, bar.Height, new Color(20, 24, 40));
            int filled = (int)MathF.Round(value * 10f);
            for (int i = 0; i < 10; i++)
            {
                Rect(bar.X + 2 + i * 10, bar.Y + 3, 7, bar.Height - 6,
                    i < filled ? new Color(250, 210, 80) : new Color(60, 66, 92));
            }

            Text((filled * 10).ToString("D2") + "%", 262, y + 2, new Color(160, 190, 230));
        }

        private void DrawGearRow()
        {
            bool selected = _settingsRow == 2;
            Panel(24, 152, 272, 36,
                selected ? new Color(24, 32, 66) : new Color(10, 14, 28),
                selected ? new Color(250, 210, 80) : new Color(50, 60, 90));

            Text("GEARS", 34, 164, selected ? Color.White : new Color(190, 200, 220));
            DrawOptionChip(SettingsAuto, "AUTO", !_settings.Value.ManualGears);
            DrawOptionChip(SettingsManual, "MANUAL", _settings.Value.ManualGears);
        }

        private void DrawOptionChip(Rectangle rect, string label, bool on)
        {
            Rect(rect.X, rect.Y, rect.Width, rect.Height, on ? new Color(40, 50, 100) : new Color(14, 18, 34));
            HudFrame(rect.X, rect.Y, rect.Width, rect.Height,
                on ? new Color(250, 210, 80) : new Color(70, 80, 110));
            TextCentered(label, rect.X + rect.Width / 2, rect.Y + rect.Height / 2 - 3,
                on ? Color.White : new Color(170, 180, 210));
        }

        private void SetGearbox(bool manual)
        {
            _settings.SetManualGears(manual);
            _audio.Play(_audio.Select);
        }

        private void UpdateDifficultySelect()
        {
            if (_input.Up)
            {
                _difficultyCursor = (_difficultyCursor + 2) % 3;
                _audio.Play(_audio.Select);
            }

            if (_input.Down)
            {
                _difficultyCursor = (_difficultyCursor + 1) % 3;
                _audio.Play(_audio.Select);
            }

            int tapped = TappedRow(3, 82, 42);
            if (tapped >= 0)
            {
                _difficultyCursor = tapped;
                _input.Confirm = true;
            }

            if (_input.Confirm)
            {
                _difficulty = DifficultyRules.All[_difficultyCursor];
                _audio.Play(_audio.Beep);
                _missionCursor = 0;
                SetScreen(GameScreen.MissionSelect);
            }
            else if (_input.Back)
            {
                SetScreen(GameScreen.Title);
            }
        }

        private void UpdateMissionSelect()
        {
            if (_input.Left && _missionCursor % 3 > 0)
            {
                _missionCursor--;
                _audio.Play(_audio.Select);
            }

            if (_input.Right && _missionCursor % 3 < 2)
            {
                _missionCursor++;
                _audio.Play(_audio.Select);
            }

            if (_input.Up && _missionCursor >= 3)
            {
                _missionCursor -= 3;
                _audio.Play(_audio.Select);
            }

            if (_input.Down && _missionCursor + 3 < Mission.All.Count)
            {
                _missionCursor += 3;
                _audio.Play(_audio.Select);
            }

            Rectangle[] cards = MissionCards();
            for (int i = 0; i < cards.Length; i++)
            {
                if (TapOn(cards[i]))
                {
                    _missionCursor = i;
                    _input.Confirm = true;
                }
            }

            if (_input.Confirm)
            {
                _mission = Mission.All[_missionCursor];
                _audio.Play(_audio.Beep);
                SetScreen(GameScreen.Briefing);
            }
            else if (_input.Back)
            {
                SetScreen(GameScreen.DifficultySelect);
            }
        }

        private void UpdateBriefing()
        {
            if (_input.Confirm || TapOn(CanvasLogical))
            {
                StartCampaign();
            }
            else if (_input.Back)
            {
                SetScreen(GameScreen.MissionSelect);
            }
        }

        private void StartCampaign()
        {
            _campaignIndex = _missionCursor;
            _campaignRaw = 0;
            _scoreSubmitted = false;
            _finalScore = 0;
            _highlightIndex = -1;
            _lastRank = 0;
            StartMission();
        }

        private const float CrashFlashTime = 0.7f;
        private float _crashTimer;
        private int _crashSide = 1;

        private void StartMission()
        {
            _mission = Mission.All[_campaignIndex];
            _race = new Race(_mission, _difficulty, Environment.TickCount + _campaignIndex * 977,
                _settings.Value.ManualGears);
            _countdown = 3.35f;
            _lastCountdownSecond = -1;
            _shake = 0f;
            _crashTimer = 0f;
            _skyOffset = 0f;
            _radioText = "SUSPECT SPOTTED - GO!";
            _radioTimer = 2.4f;
            _audio.Play(_audio.SirenShort, 0.7f);
            SetScreen(GameScreen.Racing);
        }

        private void UpdateRacing(float dt)
        {
            if (_input.Pause)
            {
                _paused = true;
                SetScreen(GameScreen.Paused);
                return;
            }

            if (_race == null)
            {
                return;
            }

            _shake = MathF.Max(0f, _shake - dt * 3.5f);
            _crashTimer = MathF.Max(0f, _crashTimer - dt);
            _gearFlash = MathF.Max(0f, _gearFlash - dt);

            if (_countdown > 0f)
            {
                _countdown -= dt;
                int second = (int)MathF.Ceiling(_countdown);
                if (second != _lastCountdownSecond && second > 0)
                {
                    _lastCountdownSecond = second;
                    _audio.Play(_audio.Countdown, 0.7f);
                }

                if (_countdown <= 0f)
                {
                    _audio.Play(_audio.Countdown, 0.9f, 0.35f);
                    _lastCountdownSecond = 0;
                }

                _lastSpeedRatio = 0f;
                return;
            }

            var raceInput = new RaceInput
            {
                Steer = _input.Steer,
                Accelerate = !_input.Brake,
                Brake = _input.Brake,
                Turbo = _input.Turbo,
                Gear = _input.Gear
            };

            _race.Update(dt, raceInput);
            _lastSpeedRatio = _race.SpeedRatio;
            _shake = MathF.Max(_shake, _race.Shake);

            float curve = _race.Road.CurveAt(_race.PlayerZ);
            _skyOffset += curve * _race.SpeedRatio * dt * 42f;

            HandleRaceEvents();

            if (_race.Busted)
            {
                _raceEndTimer += dt;
                if (_raceEndTimer > 2.2f)
                {
                    _campaignRaw += _race.RawScore;
                    _raceEndTimer = 0f;
                    _audio.Play(_audio.Checkpoint, 0.8f);
                    SetScreen(GameScreen.Busted);
                }
            }
            else if (_race.TimedOut)
            {
                _raceEndTimer += dt;
                if (_raceEndTimer > 1.2f)
                {
                    _campaignRaw += _race.RawScore;
                    _raceEndTimer = 0f;
                    _audio.StopRaceAudio();
                    SetScreen(GameScreen.GameOver);
                }
            }
            else if (_race.Wrecked)
            {
                _raceEndTimer += dt;
                if (_raceEndTimer > 2.2f)
                {
                    _campaignRaw += _race.RawScore;
                    _raceEndTimer = 0f;
                    _audio.StopRaceAudio();
                    SetScreen(GameScreen.GameOver);
                }
            }
            else
            {
                _raceEndTimer = 0f;
            }
        }

        private void HandleRaceEvents()
        {
            for (int i = 0; i < _race.Events.Count; i++)
            {
                RaceEvent evt = _race.Events[i];
                switch (evt.Type)
                {
                    case RaceEventType.Ram:
                        _shake = 1f;
                        _audio.Play(_audio.Ram, ChipAudio.Mix.Ram);
                        SetRadio("RAMMED HIM! +" + evt.Value);
                        break;
                    case RaceEventType.GearChange:
                        _gearFlash = 0.6f;
                        _audio.Play(_audio.Select, 0.2f);
                        break;
                    case RaceEventType.TrafficHit:
                        _shake = 1f;
                        _crashTimer = CrashFlashTime;
                        _crashSide = _race.PlayerX >= 0f ? -1 : 1;
                        _audio.Play(_audio.Crash, ChipAudio.Mix.Crash);
                        _audio.Play(_audio.Screech, ChipAudio.Mix.Screech);
                        _audio.Play(_audio.Squelch, ChipAudio.Mix.Squelch);
                        SetRadio(evt.Value > 0 ? "CRASH! HULL " + evt.Value : "CAR WRECKED");
                        break;
                    case RaceEventType.Repaired:
                        _audio.Play(_audio.Checkpoint, ChipAudio.Mix.Checkpoint);
                        SetRadio("HULL PATCHED UP - " + evt.Value);
                        break;
                    case RaceEventType.Checkpoint:
                        _audio.Play(_audio.Checkpoint, ChipAudio.Mix.Checkpoint);
                        SetRadio("CHECKPOINT - TIME EXTENDED");
                        break;
                    case RaceEventType.TurboEmpty:
                        SetRadio("TURBO EMPTY");
                        break;
                    case RaceEventType.OffRoad:
                        _audio.Play(_audio.Skid, ChipAudio.Mix.Skid);
                        break;
                    case RaceEventType.SuspectEscaping:
                        _audio.Play(_audio.Wrong, ChipAudio.Mix.Warning);
                        SetRadio("SUSPECT ESCAPING!");
                        break;
                    case RaceEventType.GunfireWarning:
                        _audio.Play(_audio.Squelch, ChipAudio.Mix.Squelch);
                        SetRadio("SHOTS INCOMING - WEAVE!");
                        break;
                    case RaceEventType.Gunfire:
                        _shake = MathF.Max(_shake, 0.5f);
                        _audio.Play(_audio.Gunshot, ChipAudio.Mix.Gunshot);
                        SetRadio("SUSPECT IS SHOOTING!");
                        break;
                    case RaceEventType.PlayerHit:
                        _shake = 1f;
                        _audio.Play(_audio.Gunshot, ChipAudio.Mix.Gunshot);
                        _audio.Play(_audio.Squelch, ChipAudio.Mix.Squelch);
                        SetRadio("TAKING FIRE! HULL " + evt.Value);
                        break;
                    case RaceEventType.Wrecked:
                        _shake = 1f;
                        _audio.Play(_audio.Crash, ChipAudio.Mix.Crash);
                        _audio.Play(_audio.Screech, ChipAudio.Mix.Screech);
                        SetRadio("CAR WRECKED");
                        break;
                    case RaceEventType.NearMiss:
                        break;
                }
            }
        }

        private void SetRadio(string text)
        {
            _radioText = text;
            _radioTimer = 2.2f;
            _radioFlash = 1f;
        }

        private void UpdateRadio(float dt)
        {
            if (_radioTimer > 0f)
            {
                _radioTimer -= dt;
            }

            if (_radioFlash > 0f)
            {
                _radioFlash -= dt * 2f;
            }
        }

        private void UpdatePaused()
        {
            if (_input.Confirm || _input.Pause)
            {
                _paused = false;
                _audio.PauseRaceAudio(false);
                SetScreen(GameScreen.Racing);
            }
            else if (_input.Back)
            {
                _paused = false;
                _audio.PauseRaceAudio(false);
                _campaignRaw += _race?.RawScore ?? 0;
                SetScreen(GameScreen.GameOver);
            }
        }

        private void UpdateBusted()
        {
            if (_screenTime > 2.0f || _input.Confirm)
            {
                _campaignIndex++;
                if (_campaignIndex >= Mission.All.Count)
                {
                    _finalScore = DifficultyRules.Weight(_difficulty, _campaignRaw);
                    SetScreen(GameScreen.CampaignComplete);
                }
                else
                {
                    SetScreen(GameScreen.MissionCleared);
                }
            }
        }

        private void UpdateMissionCleared()
        {
            if (_screenTime > 1.2f && (_input.Confirm || _screenTime > 6f))
            {
                StartMission();
            }
        }

        private void UpdateCampaignComplete()
        {
            if (_screenTime > 1.0f && (_input.Confirm || _screenTime > 5.5f))
            {
                CompleteRun(_mission);
            }
        }

        private void UpdateGameOver()
        {
            if (_screenTime > 1.2f && (_input.Confirm || _screenTime > 6f))
            {
                CompleteRun(_mission);
            }
        }

        private void CompleteRun(Mission mission)
        {
            if (_scoreSubmitted)
            {
                SetScreen(GameScreen.HighScores);
                return;
            }

            int score = _finalScore > 0
                ? _finalScore
                : DifficultyRules.Weight(_difficulty, _campaignRaw);

            _finalScore = score;
            _pendingEntry = new ScoreEntry
            {
                Name = _lastName,
                Score = score,
                Difficulty = _difficulty,
                MissionId = mission?.Id ?? "",
                MissionTitle = mission?.Title ?? "",
                Stage = (mission?.Index ?? 0) + 1,
                Outcome = RunOutcomeText(),
                Rams = _race?.Rams ?? 0,
                Ticks = DateTime.UtcNow.Ticks
            };

            if (_scores.Table.Qualifies(score) || score > 0)
            {
                _nameLetters = new[]
                {
                    _lastName.Length > 0 ? char.ToUpperInvariant(_lastName[0]) : 'A',
                    _lastName.Length > 1 ? char.ToUpperInvariant(_lastName[1]) : 'A',
                    _lastName.Length > 2 ? char.ToUpperInvariant(_lastName[2]) : 'A'
                };
                _nameCursor = 0;
                SetScreen(GameScreen.NameEntry);
            }
            else
            {
                _scoreSubmitted = true;
                SetScreen(GameScreen.HighScores);
            }
        }

        /// <summary>How the run ended, in the words the high-score card uses.</summary>
        private string RunOutcomeText()
        {
            if (_screen == GameScreen.CampaignComplete)
            {
                return "ALL STAGES CLEARED";
            }

            if (_race?.Wrecked == true)
            {
                return _race.WreckedBy == WreckCause.Gunfire
                    ? "SHOT BY THE SUSPECT"
                    : "RAMMED A CAR";
            }

            if (_race?.TimedOut == true)
            {
                return "RAN OUT OF TIME";
            }

            return "SUSPECT ESCAPED";
        }

        private char[] _letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.!-".ToCharArray();

        private static Rectangle NameBox(int index)
        {
            return new Rectangle(107 + index * 38, 95, 30, 36);
        }

        private static Rectangle NameUpButton => new Rectangle(107, 80, 106, 13);
        private static Rectangle NameDownButton => new Rectangle(107, 134, 106, 13);
        private static Rectangle NameOkButton => new Rectangle(222, 95, 60, 36);

        private void UpdateNameEntry()
        {
            // On touch screens every part of name entry is an explicit, tappable control.
            bool touchUi = MobilePlatform || _PointerSeen;
            if (touchUi && _input.PointerPressed)
            {
                Point point = _input.Pointer.ToPoint();
                bool handled = false;

                for (int i = 0; i < 3; i++)
                {
                    if (NameBox(i).Contains(point))
                    {
                        _nameCursor = i;
                        _audio.Play(_audio.Select);
                        handled = true;
                    }
                }

                if (NameUpButton.Contains(point))
                {
                    ChangeNameLetter(1);
                    handled = true;
                }
                else if (NameDownButton.Contains(point))
                {
                    ChangeNameLetter(-1);
                    handled = true;
                }
                else if (NameOkButton.Contains(point))
                {
                    SubmitNameEntry();
                    return;
                }

                // The tap belongs to the name-entry UI even if it hit empty space.
                _input.Confirm = false;
                if (handled)
                {
                    return;
                }
            }

            if (_input.Left)
            {
                _nameCursor = (_nameCursor + 2) % 3;
                _audio.Play(_audio.Select);
            }

            if (_input.Right)
            {
                _nameCursor = (_nameCursor + 1) % 3;
                _audio.Play(_audio.Select);
            }

            if (_input.Up)
            {
                ChangeNameLetter(1);
            }

            if (_input.Down)
            {
                ChangeNameLetter(-1);
            }

            if (_input.Confirm)
            {
                SubmitNameEntry();
            }
        }

        private void ChangeNameLetter(int direction)
        {
            int current = Array.IndexOf(_letters, _nameLetters[_nameCursor]);
            current = (current + direction + _letters.Length) % _letters.Length;
            _nameLetters[_nameCursor] = _letters[current];
            _audio.Play(_audio.Select);
        }

        private void SubmitNameEntry()
        {
            string name = new string(_nameLetters);
            _pendingEntry.Name = name;
            _lastName = name;
            _lastRank = _scores.Submit(_pendingEntry);
            _highlightIndex = _lastRank - 1;
            _scoreSubmitted = true;
            _audio.Play(_audio.Beep, 0.9f, 0.2f);
            SetScreen(GameScreen.HighScores);
        }

        private static string ConfirmPrompt => MobilePlatform ? "TAP" : "FIRE";

        private void UpdateHighScores()
        {
            if (_screenTime > 0.5f && (_input.Confirm || _input.Back))
            {
                _audio.Play(_audio.Select);
                SetScreen(GameScreen.Title);
            }
        }

        private void PrepareSelfTest()
        {
            switch (_selfTestScreen)
            {
                case "splash":
                    SetScreen(GameScreen.Splash);
                    break;
                case "difficulty":
                    _difficultyCursor = 1;
                    SetScreen(GameScreen.DifficultySelect);
                    break;
                case "mission":
                    _missionCursor = 3;
                    SetScreen(GameScreen.MissionSelect);
                    break;
                case "briefing":
                    _missionCursor = 3;
                    _mission = Mission.All[3];
                    SetScreen(GameScreen.Briefing);
                    break;
                case "race":
                    _missionCursor = 0;
                    _difficulty = Difficulty.Beginner;
                    StartCampaign();
                    _countdown = 0f;
                    break;
                case "race2":
                    _missionCursor = 3;
                    _difficulty = Difficulty.Medium;
                    StartCampaign();
                    _countdown = 0f;
                    break;
                case "race3":
                    _missionCursor = 6;
                    _difficulty = Difficulty.Beginner;
                    StartCampaign();
                    _countdown = 0f;
                    break;
                case "briefing2":
                    _missionCursor = 6;
                    _mission = Mission.All[6];
                    SetScreen(GameScreen.Briefing);
                    break;
                case "wreck":
                    _missionCursor = 0;
                    StartCampaign();
                    _countdown = 0f;
                    for (int i = 0; i < 60 * 180 && _screen == GameScreen.Racing; i++)
                    {
                        TrafficCar nearest = null;
                        float best = float.MaxValue;
                        foreach (TrafficCar car in _race.Traffic)
                        {
                            float dz = car.Z - _race.PlayerZ;
                            if (dz > 200f && dz < best)
                            {
                                best = dz;
                                nearest = car;
                            }
                        }

                        _input.Steer = nearest != null
                            ? Math.Clamp((nearest.X - _race.PlayerX) * 3f, -1f, 1f)
                            : 0f;
                        _input.Turbo = true;
                        _input.Brake = false;
                        UpdateRacing(1f / 60f);
                        _audio.UpdateRaceAudio(_lastSpeedRatio, _race.OffRoad, false, true);
                    }

                    break;
                case "crash":
                    _PointerSeen = true;
                    _missionCursor = 0;
                    _difficulty = Difficulty.Beginner;
                    StartCampaign();
                    _countdown = 0f;
                    _crashTimer = CrashFlashTime;
                    _crashSide = 1;
                    break;
                case "pads":
                    _PointerSeen = true;
                    _missionCursor = 0;
                    _difficulty = Difficulty.Beginner;
                    _settings.SetManualGears(true);
                    StartCampaign();
                    _countdown = 0f;
                    break;
                case "busted":
                    _missionCursor = 0;
                    StartCampaign();
                    _countdown = 0f;
                    for (int i = 0; i < 90; i++)
                    {
                        _race.Update(1f / 60f, new RaceInput { Accelerate = true });
                    }

                    SetScreen(GameScreen.Busted);
                    break;
                case "gameover":
                    _missionCursor = 0;
                    StartCampaign();
                    _countdown = 0f;
                    _campaignRaw = 18450;
                    SetScreen(GameScreen.GameOver);
                    break;
                case "name":
                case "nametap":
                    _PointerSeen = true;
                    _pendingEntry = new ScoreEntry
                    {
                        Score = 9120,
                        Difficulty = Difficulty.Hard,
                        MissionId = "arctic",
                        MissionTitle = "FROSTLINE",
                        Stage = 6,
                        Outcome = "SHOT BY THE SUSPECT"
                    };
                    SetScreen(GameScreen.NameEntry);
                    break;
                case "settings":
                    SetScreen(GameScreen.Settings);
                    break;
                case "scores":
                    // A scratch table for the screenshot: never touch the player's real scores.
                    _scores = new HighScoreStore(new MemoryHighScoreFile());
                    _scores.Submit(new ScoreEntry { Name = "PC1", Score = 24800, Difficulty = Difficulty.Hard, MissionId = "canyon", MissionTitle = "RED CANYON", Stage = 9, Outcome = "ALL STAGES CLEARED", Ticks = 1 });
                    _scores.Submit(new ScoreEntry { Name = "PC2", Score = 18420, Difficulty = Difficulty.Medium, MissionId = "arctic", MissionTitle = "FROSTLINE", Stage = 6, Outcome = "SHOT BY THE SUSPECT", Ticks = 2 });
                    _scores.Submit(new ScoreEntry { Name = "PC3", Score = 15310, Difficulty = Difficulty.Beginner, MissionId = "neon", MissionTitle = "NEON GRID", Stage = 4, Outcome = "RAMMED A CAR", Ticks = 3 });
                    _scores.Submit(new ScoreEntry { Name = "PC4", Score = 12040, Difficulty = Difficulty.Hard, MissionId = "harbour", MissionTitle = "HARBOUR PURSUIT", Stage = 1, Outcome = "RAN OUT OF TIME", Ticks = 4 });
                    _scores.Submit(new ScoreEntry { Name = "PC5", Score = 9120, Difficulty = Difficulty.Medium, MissionId = "alpine", MissionTitle = "ALPINE PASS", Stage = 3, Outcome = "RAMMED A CAR", Ticks = 5 });

                    _highlightIndex = 3;
                    SetScreen(GameScreen.HighScores);
                    break;
                case "touch":
                    RunTouchAudit();
                    SetScreen(GameScreen.Title);
                    break;
                default:
                    SetScreen(GameScreen.Title);
                    break;
            }
        }

        private void AuditTap(int x, int y)
        {
            _input = default;
            _input.Pointer = new Vector2(x, y);
            _input.PointerDown = true;
            _input.PointerPressed = true;
            // ReadInput turns the first frame of a touch into a confirm; emulate that here.
            _input.Confirm = true;
        }

        /// <summary>
        /// Drives a tap through every interactive screen and reports pass/fail. Run with
        /// <c>--selftest touch</c>; the audit is written next to the screenshot.
        /// </summary>
        private void RunTouchAudit()
        {
            _PointerSeen = true;
            var lines = new List<string>();
            int failures = 0;

            void Check(string name, bool passed)
            {
                lines.Add((passed ? "PASS  " : "FAIL  ") + name);
                if (!passed)
                {
                    failures++;
                }
            }

            SetScreen(GameScreen.Title);
            AuditTap(250, 198);
            UpdateTitle();
            Check("title: VIEW TABLE button", _screen == GameScreen.HighScores);

            SetScreen(GameScreen.Title);
            AuditTap(SettingsButton.Center.X, SettingsButton.Center.Y);
            UpdateTitle();
            Check("title: SETTINGS button", _screen == GameScreen.Settings);

            int musicStep = (int)MathF.Round(_audio.MusicVolume * 10f);
            AuditTap(SettingsMinus(0).Center.X, SettingsMinus(0).Center.Y);
            UpdateSettings();
            Check("settings: minus lowers music",
                (int)MathF.Round(_audio.MusicVolume * 10f) < musicStep);

            AuditTap(SettingsDone.Center.X, SettingsDone.Center.Y);
            UpdateSettings();
            Check("settings: DONE returns to title", _screen == GameScreen.Title);

            _screenTime = 1f;
            AuditTap(160, 120);
            UpdateHighScores();
            Check("high scores: tap to return", _screen == GameScreen.Title);

            AuditTap(160, 100);
            UpdateTitle();
            Check("title: tap to deploy", _screen == GameScreen.DifficultySelect);

            AuditTap(160, 145);
            UpdateDifficultySelect();
            Check("skill: tap second row", _screen == GameScreen.MissionSelect && _difficultyCursor == 1);

            Rectangle card = MissionCards()[4];
            AuditTap(card.Center.X, card.Center.Y);
            UpdateMissionSelect();
            Check("mission: tap fifth card", _screen == GameScreen.Briefing && _missionCursor == 4);

            AuditTap(160, 120);
            UpdateBriefing();
            Check("briefing: tap to start", _screen == GameScreen.Racing);

            InputFrame pad = default;
            ApplyRacingTouch(ref pad, new Vector2(SteerLeftPad.Center.X, SteerLeftPad.Center.Y), true);
            Check("race: left pad steers left", pad.Steer < -0.9f);

            pad = default;
            ApplyRacingTouch(ref pad, new Vector2(SteerRightPad.Center.X, SteerRightPad.Center.Y), true);
            Check("race: right pad steers right", pad.Steer > 0.9f);

            pad = default;
            ApplyRacingTouch(ref pad, new Vector2(TurboPad.Center.X, TurboPad.Center.Y), true);
            Check("race: TURBO pad", pad.Turbo);

            pad = default;
            ApplyRacingTouch(ref pad, new Vector2(BrakePad.Center.X, BrakePad.Center.Y), true);
            Check("race: BRK pad", pad.Brake);

            _settings.SetManualGears(true);
            pad = default;
            pad.PointerPressed = true;
            ApplyRacingTouch(ref pad, new Vector2(GearPad.Center.X, GearPad.Center.Y), true);
            Check("race: GEAR pad shifts", pad.Gear);
            _settings.SetManualGears(false);

            pad = default;
            ApplyRacingTouch(ref pad, new Vector2(280, 130), true);
            Check("race: drag right steers", pad.Steer > 0.9f);

            SetScreen(GameScreen.Busted);
            AuditTap(160, 120);
            UpdateBusted();
            Check("busted: tap to continue", _screen == GameScreen.MissionCleared);

            _screenTime = 2f;
            AuditTap(160, 120);
            UpdateMissionCleared();
            Check("mission cleared: tap to continue", _screen == GameScreen.Racing);

            SetScreen(GameScreen.GameOver);
            _campaignRaw = 18450;
            _screenTime = 2f;
            AuditTap(160, 120);
            UpdateGameOver();
            Check("game over: tap to file the score", _screen == GameScreen.NameEntry);

            char before = _nameLetters[0];
            AuditTap(NameUpButton.Center.X, NameUpButton.Center.Y);
            UpdateNameEntry();
            Check("name entry: UP key changes the letter", _nameLetters[0] != before);

            AuditTap(NameBox(2).Center.X, NameBox(2).Center.Y);
            UpdateNameEntry();
            Check("name entry: letter box selects", _nameCursor == 2);

            string entered = new string(_nameLetters);
            AuditTap(NameOkButton.Center.X, NameOkButton.Center.Y);
            UpdateNameEntry();
            Check("name entry: OK submits", _screen == GameScreen.HighScores);
            Check("name entry: typed name lands on the table",
                _scores.Table.Entries.Exists(e => e.Name == entered));

            SetScreen(GameScreen.Paused);
            AuditTap(160, 120);
            UpdatePaused();
            Check("pause: tap to resume", _screen == GameScreen.Racing);

            lines.Add(failures == 0
                ? "ALL " + lines.Count + " TOUCH CHECKS PASSED"
                : failures + " OF " + lines.Count + " TOUCH CHECKS FAILED");

            string auditPath = System.IO.Path.IsPathRooted(_selfTestPath)
                ? _selfTestPath + ".audit.txt"
                : System.IO.Path.Combine(LocalHighScoreFile.DefaultRoot, _selfTestPath + ".audit.txt");

            try
            {
                File.WriteAllLines(auditPath, lines);
            }
            catch (Exception)
            {
                // Diagnostics must never stop the game.
            }

            Console.WriteLine(string.Join(Environment.NewLine, lines));
        }

        private void SeedSelfTestScores()
        {
            var entries = new[]
            {
                new { Name = "ACE", Score = 24800, Difficulty = Difficulty.Beginner, MissionId = "arctic" },
                new { Name = "NEO", Score = 19650, Difficulty = Difficulty.Medium, MissionId = "desert" },
                new { Name = "JAX", Score = 15200, Difficulty = Difficulty.Beginner, MissionId = "neon" },
                new { Name = "PC1", Score = 11400, Difficulty = Difficulty.Hard, MissionId = "alpine" },
                new { Name = "RIO", Score = 8750, Difficulty = Difficulty.Medium, MissionId = "coast" },
                new { Name = "MAX", Score = 6200, Difficulty = Difficulty.Beginner, MissionId = "harbour" },
                new { Name = "ZED", Score = 3100, Difficulty = Difficulty.Hard, MissionId = "neon" }
            };

            long ticks = DateTime.UtcNow.Ticks;
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                _scores.Submit(new ScoreEntry
                {
                    Name = entry.Name,
                    Score = entry.Score,
                    Difficulty = entry.Difficulty,
                    MissionId = entry.MissionId,
                    MissionTitle = Mission.ById(entry.MissionId)?.Title ?? "",
                    Ticks = ticks + i
                });
            }
        }

        private void UpdateSelfTest(float dt)
        {
            // Feeds one real touch tap through the name-entry handler so the mobile keys can be
            // checked without a phone in hand.
            if (_selfTestScreen == "nametap" && !_selfTestTapped && _screenTime > 0.1f)
            {
                _selfTestTapped = true;
                InputFrame saved = _input;
                _input.Pointer = new Vector2(NameUpButton.Center.X, NameUpButton.Center.Y);
                _input.PointerPressed = true;
                _input.Confirm = false;
                UpdateNameEntry();
                _input = saved;
            }

            if (_screen == GameScreen.Racing && _race != null)
            {
                _input.Steer = MathF.Sin(_screenTime * 1.25f) * 0.3f;
                _input.Turbo = _screenTime > 1.2f;
                _input.Brake = false;
                UpdateRacing(dt);
                _audio.UpdateRaceAudio(_lastSpeedRatio, _race.OffRoad, false, _input.Turbo);
            }
        }

        private void CaptureSelfTest()
        {
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            var data = new Color[width * height];

            // A bare file name goes to the writable app data folder; the app bundle is read-only
            // on iOS. Absolute paths keep working (the desktop harness uses /tmp).
            string path = System.IO.Path.IsPathRooted(_selfTestPath)
                ? _selfTestPath
                : System.IO.Path.Combine(LocalHighScoreFile.DefaultRoot, _selfTestPath);

            try
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path) ?? LocalHighScoreFile.DefaultRoot);
                GraphicsDevice.GetBackBufferData(data);
                PngWriter.Write(path, width, height, data);
                File.WriteAllText(path + ".txt",
                    "screen=" + _screen + " size=" + width + "x" + height + Environment.NewLine);
            }
            catch (Exception ex)
            {
                try
                {
                    File.WriteAllText(path + ".txt", "FAILED: " + ex.Message + Environment.NewLine);
                }
                catch (Exception)
                {
                    // Nothing writable to report into.
                }
            }

#if !IOS
            // iOS forbids programmatic termination; the harness only runs on desktop anyway.
            Exit();
#endif
        }

        // ===================================================================== input

        private void ReadInput()
        {
            var frame = new InputFrame();
            KeyboardState keys = Keyboard.GetState();
            GamePadState pad = GamePad.GetState(PlayerIndex.One);

            frame.Left = keys.IsKeyDown(Keys.Left) || keys.IsKeyDown(Keys.A) || pad.DPad.Left == ButtonState.Pressed;
            frame.Right = keys.IsKeyDown(Keys.Right) || keys.IsKeyDown(Keys.D) || pad.DPad.Right == ButtonState.Pressed;
            frame.Up = keys.IsKeyDown(Keys.Up) || keys.IsKeyDown(Keys.W) || pad.DPad.Up == ButtonState.Pressed;
            frame.Down = keys.IsKeyDown(Keys.Down) || keys.IsKeyDown(Keys.S) || pad.DPad.Down == ButtonState.Pressed;

            frame.Steer = (frame.Right ? 1f : 0f) - (frame.Left ? 1f : 0f);
            frame.Steer += pad.ThumbSticks.Left.X;
            frame.Steer = Math.Clamp(frame.Steer, -1f, 1f);

            bool keyConfirm = keys.IsKeyDown(Keys.Enter) || keys.IsKeyDown(Keys.Space);
            bool padConfirm = pad.Buttons.A == ButtonState.Pressed || pad.Buttons.Start == ButtonState.Pressed;
            frame.Turbo = keys.IsKeyDown(Keys.Space) || keys.IsKeyDown(Keys.Z) || pad.Buttons.B == ButtonState.Pressed ||
                          pad.Buttons.X == ButtonState.Pressed;
            frame.Brake = keys.IsKeyDown(Keys.Down) || keys.IsKeyDown(Keys.S) || keys.IsKeyDown(Keys.LeftControl) ||
                          pad.Triggers.Left > 0.2f;
            frame.Pause = keys.IsKeyDown(Keys.Escape) || keys.IsKeyDown(Keys.P) || pad.Buttons.Start == ButtonState.Pressed;
            frame.Back = keys.IsKeyDown(Keys.Escape) || pad.Buttons.B == ButtonState.Pressed;

            // Gear is a one-shot toggle, not a held state, so LO/HI flips once per press.
            bool gearKey = keys.IsKeyDown(Keys.X);
            bool gearPad = pad.Buttons.Y == ButtonState.Pressed;
            frame.Gear = (gearKey && !_gearKeyWasDown) || (gearPad && !_gearPadWasDown);
            _gearKeyWasDown = gearKey;
            _gearPadWasDown = gearPad;

            MouseState mouse = Mouse.GetState();
            bool mouseDown = mouse.LeftButton == ButtonState.Pressed;
            bool mousePressed = mouseDown && !_mouseWasDown;
            _mouseWasDown = mouseDown;

            Vector2 pointer = new Vector2(mouse.X, mouse.Y);
            bool pointerDown = mouseDown;
            bool pointerPressed = mousePressed;

            TouchCollection touches = TouchPanel.GetState();
            if (touches.Count > 0)
            {
                TouchLocation touch = touches[0];
                pointer = ToVirtual(touch.Position);
                pointerDown = true;
                pointerPressed = touch.State == TouchLocationState.Pressed;

                if (touch.State == TouchLocationState.Moved)
                {
                    pointerPressed = false;
                }

                // Detect the first frame of a new finger when the platform reports Moved.
                if (touch.State == TouchLocationState.Moved && _previousTouches.Count == 0)
                {
                    pointerPressed = true;
                }

                _PointerSeen = true;
                frame.Turbo |= MobilePlatform;
            }

            if (MobilePlatform && _PointerSeen)
            {
                // Name entry has its own on-screen keys, so a stray tap must not submit the
                // score before the player has had a chance to pick their letters.
                bool touchConfirms = _screen != GameScreen.NameEntry;
                frame.Confirm = keyConfirm || padConfirm || (pointerPressed && touchConfirms);
                if (pointerDown)
                {
                    frame.Turbo = false;
                }
            }
            else
            {
                frame.Confirm = keyConfirm || padConfirm || pointerPressed;
            }

            frame.Pointer = pointer;
            frame.PointerDown = pointerDown;
            frame.PointerPressed = pointerPressed;

            if (_screen == GameScreen.Racing && MobilePlatform && _PointerSeen)
            {
                ApplyRacingTouch(ref frame, pointer, pointerDown);
            }

            _previousTouches = touches;
            _input = frame;
        }

        private void ApplyRacingTouch(ref InputFrame frame, Vector2 pointer, bool down)
        {
            if (!down)
            {
                return;
            }

            Point point = pointer.ToPoint();

            if (GearPad.Contains(point) && frame.PointerPressed && _settings.Value.ManualGears)
            {
                frame.Gear = true;
            }
            else if (TurboPad.Contains(point))
            {
                frame.Turbo = true;
            }
            else if (BrakePad.Contains(point))
            {
                frame.Brake = true;
                frame.Steer = 0f;
            }
            else if (SteerLeftPad.Contains(point))
            {
                frame.Steer = -1f;
            }
            else if (SteerRightPad.Contains(point))
            {
                frame.Steer = 1f;
            }
            else
            {
                // Drag steering anywhere on the lower screen, so a slightly missed pad still
                // turns the car instead of doing nothing.
                if (pointer.Y > 120)
                {
                    float centre = VirtualWidth / 2f;
                    frame.Steer = Math.Clamp((pointer.X - centre) / 70f, -1f, 1f);
                }
            }
        }

        private bool TapOn(Rectangle rectangle)
        {
            return _input.PointerPressed && rectangle.Contains(_input.Pointer.ToPoint());
        }

        private int TappedRow(int count, int startY, int rowHeight)
        {
            if (!_input.PointerPressed)
            {
                return -1;
            }

            int index = (int)((_input.Pointer.Y - startY) / rowHeight);
            if (index < 0 || index >= count)
            {
                return -1;
            }

            return index;
        }

        // ===================================================================== draw

        protected override void Draw(GameTime gameTime)
        {
            try
            {
                DrawFrame(gameTime);
            }
            catch (Exception ex)
            {
                WriteCrash("Draw", ex);
                throw;
            }
        }

        private void DrawFrame(GameTime gameTime)
        {
            // A rotation, split screen or foldable can change the display mid-session.
            EnsureCanvas();

            GraphicsDevice.SetRenderTarget(_target);
            GraphicsDevice.Clear(Color.Black);

            // The 320x240 layout is authored once; on a phone the batch is shifted so it sits
            // centred in the wider canvas while the world reaches both screen edges.
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Matrix.CreateTranslation(_originX, 0f, 0f));

            if (PortraitWindow)
            {
                DrawRotatePrompt();
            }
            else switch (_screen)
            {
                case GameScreen.Splash:
                    DrawSplash();
                    break;
                case GameScreen.Title:
                    DrawTitle();
                    break;
                case GameScreen.Settings:
                    DrawSettings();
                    break;
                case GameScreen.DifficultySelect:
                    DrawDifficultySelect();
                    break;
                case GameScreen.MissionSelect:
                    DrawMissionSelect();
                    break;
                case GameScreen.Briefing:
                    DrawBriefing();
                    break;
                case GameScreen.Racing:
                case GameScreen.Paused:
                    DrawRace();
                    break;
                case GameScreen.Busted:
                    DrawRace();
                    DrawBustedOverlay();
                    break;
                case GameScreen.MissionCleared:
                    DrawRace();
                    DrawMissionCleared();
                    break;
                case GameScreen.CampaignComplete:
                    DrawCampaignComplete();
                    break;
                case GameScreen.GameOver:
                    DrawGameOver();
                    break;
                case GameScreen.NameEntry:
                    DrawNameEntry();
                    break;
                case GameScreen.HighScores:
                    DrawHighScores();
                    break;
            }

            _spriteBatch.End();

            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(new Color(4, 4, 10));

            Rectangle destination = ComputeDestination();
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            _spriteBatch.Draw(_target, destination, Color.White);
            _spriteBatch.End();

            // iOS has no Exit(), so the capture must be a one-shot or it would rewrite the
            // screenshot on every frame.
            if (_selfTest && !_selfTestCaptured)
            {
                _selfTestFrame++;
                if (_selfTestFrame >= _selfTestFrames)
                {
                    _selfTestCaptured = true;
                    CaptureSelfTest();
                }
            }

            base.Draw(gameTime);
        }

        private Rectangle ComputeDestination()
        {
            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;
            if (width <= 0 || height <= 0)
            {
                return new Rectangle(0, 0, _canvasWidth, _canvasHeight);
            }

            if (_wide)
            {
                // The canvas was widened to the display's aspect, so it covers the whole screen.
                return new Rectangle(0, 0, width, height);
            }

            float scale = MathF.Min(width / (float)_canvasWidth, height / (float)_canvasHeight);
            int w = (int)(_canvasWidth * scale);
            int h = (int)(_canvasHeight * scale);
            return new Rectangle((width - w) / 2, (height - h) / 2, w, h);
        }

        private Vector2 ToVirtual(Vector2 screen)
        {
            // MonoGame hands back touches in TouchPanel display space (1280x960 on iOS), which is
            // not the resolution the picture is drawn at, so undo that scale before hit testing.
            Rectangle window = Window.ClientBounds;
            screen = TouchMapping.UndoTouchScale(screen, TouchPanel.DisplayWidth, TouchPanel.DisplayHeight,
                window.Width, window.Height);

            int width = GraphicsDevice.PresentationParameters.BackBufferWidth;
            int height = GraphicsDevice.PresentationParameters.BackBufferHeight;

            if (_wide)
            {
                return TouchMapping.FullBleedToLogical(screen, width, height,
                    _canvasWidth, _canvasHeight, _originX);
            }

            return TouchMapping.ToLogical(screen, width, height);
        }

        // ------------------------------------------------------------------ primitives

        private void Rect(int x, int y, int w, int h, Color color)
        {
            _spriteBatch.Draw(_white, new Rectangle(x * S, y * S, w * S, h * S), color);
        }

        /// <summary>Draws in canvas pixels, ignoring the centred picture's side margins.</summary>
        private void CanvasRect(int x, int y, int w, int h, Color color)
        {
            _spriteBatch.Draw(_white, new Rectangle(x - _originX, y, w, h), color);
        }

        /// <summary>A band (logical y and height) that runs all the way to both screen edges.</summary>
        private void FullRect(int y, int h, Color color)
        {
            CanvasRect(0, y * S, _canvasWidth, h * S, color);
        }

        /// <summary>Tiles an image across the canvas so menu artwork reaches the screen edges.</summary>
        private void TileImage(Texture2D texture, int y, int w, int h, Color tint)
        {
            int tile = w * S;
            for (int x = -_originX; x < _canvasWidth - _originX; x += tile)
            {
                _spriteBatch.Draw(texture, new Rectangle(x, y * S, tile, h * S), tint);
            }
        }

        /// <summary>Draws a baked texture using logical (320x240) coordinates.</summary>
        private void Image(Texture2D texture, int x, int y, int w, int h, Color? tint = null)
        {
            _spriteBatch.Draw(texture, new Rectangle(x * S, y * S, w * S, h * S), tint ?? Color.White);
        }

        private void Panel(int x, int y, int w, int h, Color fill, Color border)
        {
            Rect(x, y, w, h, fill);
            Rect(x, y, w, 2, border);
            Rect(x, y + h - 2, w, 2, border);
            Rect(x, y, 2, h, border);
            Rect(x + w - 2, y, 2, h, border);
        }

        private void Text(string text, int x, int y, Color color, int scale = 1)
        {
            _font.Draw(_spriteBatch, text, x, y, color, scale);
        }

        private void TextCentered(string text, int centerX, int y, Color color, int scale = 1)
        {
            _font.DrawCentered(_spriteBatch, text, centerX, y, color, scale);
        }

        /// <summary>Draws text word-wrapped to a character budget, one line per 10 logical pixels.</summary>
        private void TextWrapped(string text, int x, int y, int maxChars, Color color, int lineHeight = 10)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            int start = 0;
            int line = 0;
            while (start < text.Length)
            {
                int length = Math.Min(maxChars, text.Length - start);
                if (start + length < text.Length)
                {
                    int cut = text.LastIndexOf(' ', Math.Min(text.Length - 1, start + length - 1), length);
                    if (cut > start)
                    {
                        length = cut - start;
                    }
                }

                Text(text.Substring(start, length).Trim(), x, y + line * lineHeight, color);
                start += length;
                while (start < text.Length && text[start] == ' ')
                {
                    start++;
                }

                line++;
                if (line > 4)
                {
                    break;
                }
            }
        }

        private void TextShadow(string text, int centerX, int y, Color color, int scale = 1)
        {
            _font.DrawCenteredShadowed(_spriteBatch, text, centerX, y, color, new Color(0, 0, 0, 190), scale);
        }

        private void Bar(int x, int y, int w, int h, float fill, Color back, Color front, Color border)
        {
            Rect(x, y, w, h, border);
            Rect(x + 1, y + 1, w - 2, h - 2, back);
            int inner = (int)((w - 2) * Math.Clamp(fill, 0f, 1f));
            if (inner > 0)
            {
                Rect(x + 1, y + 1, inner, h - 2, front);
            }
        }

        private bool Blink(float rate = 2f)
        {
            return ((int)(_time * rate)) % 2 == 0;
        }

        private void Background(MissionTheme theme)
        {
            ThemePalette palette = _art.Palette(theme);
            for (int y = 0; y < VirtualHeight; y++)
            {
                float t = y / (float)VirtualHeight;
                FullRect(y, 1, Color.Lerp(palette.SkyTop, new Color(8, 8, 16), t * 0.75f));
            }
        }

        private void SirenStrip(int y)
        {
            int phase = (int)(_time * 8f) % 4;
            Color left = phase < 2 ? new Color(220, 40, 48) : new Color(60, 40, 48);
            Color right = phase < 2 ? new Color(40, 60, 120) : new Color(60, 120, 230);
            int half = _canvasWidth / 2;
            CanvasRect(0, y * S, half, 3 * S, left);
            CanvasRect(half, y * S, _canvasWidth - half, 3 * S, right);
        }

        // ------------------------------------------------------------------ splash

        private void DrawSplash()
        {
            Background(MissionTheme.Harbour);
            FullRect(0, VirtualHeight, new Color(6, 8, 20, 235));

            Image(_art.SplashArt, 80, 36, 160, 96);
            SirenStrip(0);

            TextShadow("POLICE", 160, 132, new Color(250, 250, 255), 4);
            TextShadow("CHASER", 160, 162, new Color(250, 210, 80), 4);

            TextCentered("A 128K-STYLE PURSUIT", 160, 190, new Color(150, 170, 210), 1);
            TextCentered("ALL-THE-JOHNSONS", 160, 202, new Color(110, 120, 150), 1);

            float progress = Math.Clamp(_screenTime / 3.2f, 0f, 1f);
            Bar(60, 216, 200, 8, progress, new Color(18, 22, 34), new Color(240, 60, 60), new Color(70, 80, 110));

            if (Blink(3f))
            {
                TextCentered("LOADING", 160, 228, new Color(200, 206, 220), 1);
            }
        }

        private bool PortraitWindow =>
            GraphicsDevice.PresentationParameters.BackBufferHeight >
            GraphicsDevice.PresentationParameters.BackBufferWidth;

        private void DrawRotatePrompt()
        {
            Background(MissionTheme.Neon);
            SirenStrip(0);

            Panel(30, 74, 260, 92, new Color(8, 10, 26, 240), new Color(250, 210, 80));
            TextShadow("ROTATE DEVICE", 160, 88, new Color(250, 220, 120), 2);
            TextCentered("POLICE CHASER IS PLAYED IN LANDSCAPE", 160, 118, new Color(170, 190, 230));
            TextCentered("TURN THE DEVICE SIDEWAYS", 160, 132, new Color(150, 170, 210));
            TextCentered("THE 4:3 PICTURE SCALES TO FIT THE SCREEN", 160, 146, new Color(120, 140, 175));
        }

        // ------------------------------------------------------------------ title

        private void DrawTitle()
        {
            ThemePalette palette = _art.Palette(MissionTheme.Neon);
            for (int y = 0; y < VirtualHeight; y++)
            {
                float t = y / (float)VirtualHeight;
                FullRect(y, 1, Color.Lerp(palette.SkyTop, palette.SkyBottom, t * t));
            }

            Image(_art.Sun(MissionTheme.Neon), 240, 8, 48, 48);
            TileImage(_art.Skyline(MissionTheme.Neon, false), 54, 320, 96, Color.White * 0.9f);

            for (int i = 0; i < 10; i++)
            {
                FullRect(96 + i * 4, 2, new Color(20, 16, 40, 120));
            }

            SirenStrip(0);

            Image(_art.Badge, 6, 8, 26, 30);
            Image(_art.Badge, 288, 8, 26, 30);

            TextShadow("POLICE", 160, 40, new Color(250, 250, 255), 4);
            TextShadow("CHASER", 160, 70, new Color(250, 210, 80), 4);

            Panel(44, 112, 232, 40, new Color(8, 10, 26, 220), new Color(250, 210, 80));
            TextCentered("NINE MISSIONS - THREE SKILL LEVELS", 160, 118, new Color(160, 220, 255));
            TextCentered("SERIES II: ARMED SUSPECTS", 160, 132, new Color(250, 140, 140));

            if (Blink(2.2f))
            {
                TextCentered(ConfirmPrompt + " TO DEPLOY", 160, 164, Color.White);
            }

            if (MobilePlatform)
            {
                TextCentered("ON-SCREEN PADS IN RACE", 110, 176, new Color(120, 160, 210));
            }

            ScoreEntry best = _scores.Table.Best();
            if (best != null)
            {
                Text("HI " + best.Score.ToString("D6"), 8, 180, new Color(250, 210, 80));
                Text(best.Name + " " + DifficultyRules.Name(best.Difficulty), 8, 192, new Color(150, 160, 190));
            }

            Panel(202, 174, 112, 30, new Color(8, 10, 26, 210), new Color(80, 90, 130));
            Text("HIGH SCORES", 214, 180, new Color(190, 200, 230));
            Text("VIEW TABLE", 214, 192, new Color(130, 145, 180));

            DrawKey(SettingsButton, "SETTINGS");
        }

        // ------------------------------------------------------------------ menus

        private Rectangle[] MissionCards()
        {
            var cards = new Rectangle[Mission.All.Count];
            for (int i = 0; i < cards.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;
                cards[i] = new Rectangle(6 + col * 104, 42 + row * 41, 100, 37);
            }

            return cards;
        }

        private void DrawDifficultySelect()
        {
            Background(MissionTheme.Neon);
            SirenStrip(0);

            TextShadow("SELECT SKILL", 160, 18, Color.White, 2);

            for (int i = 0; i < 3; i++)
            {
                Difficulty difficulty = DifficultyRules.All[i];
                bool selected = i == _difficultyCursor;
                int y = 82 + i * 42;

                Panel(24, y, 272, 36,
                    selected ? new Color(30, 40, 90) : new Color(12, 16, 34),
                    selected ? new Color(250, 210, 80) : new Color(60, 70, 110));

                Text(DifficultyRules.Name(difficulty), 34, y + 5, selected ? Color.White : new Color(190, 200, 220), 2);
                Text(DifficultyRules.Blurb(difficulty), 34, y + 24, new Color(150, 170, 210), 1);

                string points = difficulty == Difficulty.Beginner ? "FULL POINTS" :
                    difficulty == Difficulty.Medium ? "HALF POINTS" : "FIFTH POINTS";
                Text(points, 288 - _font.Measure(points, 1), y + 8, new Color(250, 210, 80), 1);

                if (selected)
                {
                    Text(">", 16, y + 10, new Color(250, 210, 80), 2);
                }
            }

            TextCentered("UP/DOWN TO CHOOSE - FIRE TO CONFIRM", 160, 214, new Color(130, 140, 175));
            TextCentered("M - MENU", 160, 228, new Color(90, 100, 130));
        }

        private void DrawMissionSelect()
        {
            Background(MissionTheme.Harbour);
            SirenStrip(0);

            TextShadow("SELECT MISSION", 160, 8, Color.White, 2);
            Text("SKILL: " + DifficultyRules.Name(_difficulty), 8, 28, new Color(250, 210, 80));

            Rectangle[] cards = MissionCards();
            for (int i = 0; i < cards.Length; i++)
            {
                Mission mission = Mission.All[i];
                ThemePalette palette = _art.Palette(mission.Theme);
                bool selected = i == _missionCursor;
                Rectangle card = cards[i];

                Panel(card.X, card.Y, card.Width, card.Height,
                    Color.Lerp(palette.BuildingDark, new Color(6, 8, 20), 0.35f),
                    selected ? new Color(250, 210, 80) : new Color(70, 80, 110));

                Rect(card.X + 3, card.Y + 3, 14, 10, palette.RumbleLight);
                Text((i + 1).ToString(), card.X + 6, card.Y + 5, new Color(20, 24, 40));

                if (mission.IsNewMission)
                {
                    bool series2 = mission.Series == 2;
                    int badgeWidth = series2 ? 16 : 26;
                    Rect(card.X + card.Width - badgeWidth - 3, card.Y + 3, badgeWidth, 10,
                        series2 ? new Color(250, 180, 60) : new Color(220, 60, 70));
                    TextCentered(series2 ? "II" : "NEW", card.X + card.Width - badgeWidth / 2 - 3, card.Y + 5,
                        series2 ? new Color(30, 20, 8) : Color.White);
                }

                TextCentered(CardTitle(mission), card.X + card.Width / 2, card.Y + 16, Color.White);
                TextCentered(ThemeLabel(mission.Theme), card.X + card.Width / 2, card.Y + 26,
                    new Color(170, 182, 210));
            }

            Mission current = Mission.All[_missionCursor];
            Panel(6, 164, 308, 62, new Color(6, 8, 20, 228), new Color(60, 70, 110));
            Text(current.Tagline, 12, 168, new Color(200, 210, 235));
            string detail = "SUSPECT " + current.SuspectName + " - " + current.SuspectCrime;
            Text(detail, 12, 180, new Color(250, 210, 80));
            Text("RAMS " + current.RequiredRams + "   TIME " + (int)current.BaseTimeSeconds + "S",
                12, 192, new Color(150, 170, 210));
            Text(current.SuspectArmed ? "SERIES II - ARMED SUSPECT - HULL AT RISK" : "SERIES I - TRAFFIC AND TIME ONLY",
                12, 204, current.SuspectArmed ? new Color(255, 120, 120) : new Color(150, 170, 210));

            TextCentered("ARROWS TO CHOOSE - FIRE TO START", 160, 232, new Color(90, 100, 130));
        }

        private static string CardTitle(Mission mission)
        {
            string title = mission.Title;
            int space = title.IndexOf(' ');
            return space > 0 ? title.Substring(0, space) : title;
        }

        private static string ThemeLabel(MissionTheme theme)
        {
            switch (theme)
            {
                case MissionTheme.Coast: return "COAST ROAD";
                case MissionTheme.Alpine: return "MOUNTAIN PASS";
                case MissionTheme.Neon: return "NIGHT CITY";
                case MissionTheme.Desert: return "SALT FLATS";
                case MissionTheme.Arctic: return "BLIZZARD";
                case MissionTheme.Industrial: return "FOUNDRY";
                case MissionTheme.Highway: return "INTERSTATE";
                case MissionTheme.Canyon: return "RED ROCK";
                default: return "DOCKS";
            }
        }

        private void DrawBriefing()
        {
            Background(_mission.Theme);
            SirenStrip(0);

            Panel(16, 22, 288, 172, new Color(8, 10, 26, 235), new Color(250, 210, 80));

            Text("STAGE " + (_campaignIndex + 1) + "/" + Mission.All.Count + " - PURSUIT BRIEFING",
                26, 28, new Color(250, 210, 80));
            DrawSuspectPhoto(216, 88, 80, 62);
            TextCentered(_mission.Title, 160, 42, Color.White, 2);

            TextWrapped(_mission.Tagline, 26, 64, 46, new Color(180, 195, 230));

            Text("SUSPECT:", 26, 90, new Color(140, 150, 185));
            Text(_mission.SuspectName, 86, 90, new Color(255, 140, 140), 2);
            Text("OFFENCE:", 26, 106, new Color(140, 150, 185));
            Text(_mission.SuspectCrime, 86, 106, new Color(200, 210, 235));

            Text("TARGET:", 26, 122, new Color(140, 150, 185));
            Text("RAM " + _mission.RequiredRams + " TIMES TO STOP", 86, 122, new Color(200, 210, 235));

            Text("SKILL:", 26, 138, new Color(140, 150, 185));
            Text(DifficultyRules.Name(_difficulty), 86, 138, new Color(250, 210, 80));

            Text("TIME:", 26, 154, new Color(140, 150, 185));
            Text((int)DifficultyRules.BaseTime(_difficulty, _mission.BaseTimeSeconds) + " SECONDS", 86, 154,
                new Color(200, 210, 235));

            if (_mission.SuspectArmed)
            {
                Text("WARNING:", 26, 170, new Color(255, 120, 120));
                Text("ARMED - HULL AT RISK", 86, 170, new Color(255, 160, 160));
            }

            if (Blink(2f))
            {
                TextCentered("FIRE TO DEPLOY", 160, 182, Color.White);
            }

            FullRect(200, 24, new Color(10, 12, 24));
            TextCentered("STEER WITH LEFT/RIGHT - BRAKE DOWN - TURBO FIRE", 160, 206, new Color(140, 160, 200));
            TextCentered("MOBILE: ON-SCREEN PADS", 160, 217, new Color(100, 110, 140));
        }

        /// <summary>A framed file photo of the suspect's car, echoing the original's stage card.</summary>
        private void DrawSuspectPhoto(int x, int y, int w, int h)
        {
            Rect(x, y, w, h, new Color(12, 14, 26));

            Texture2D car = _art.SuspectCars[Math.Clamp(_mission.SuspectColor, 0, _art.SuspectCars.Length - 1)];
            int carW = w - 12;
            int carH = car.Height * carW / car.Width;
            if (carH > h - 12)
            {
                carH = h - 12;
                carW = car.Width * carH / car.Height;
            }

            _spriteBatch.Draw(car,
                new Rectangle((x + (w - carW) / 2) * S, (y + (h - carH) / 2) * S, carW * S, carH * S),
                Color.White);

            Rect(x, y, w, 1, Color.White);
            Rect(x, y + h - 1, w, 1, Color.White);
            Rect(x, y, 1, h, Color.White);
            Rect(x + w - 1, y, 1, h, Color.White);
            TextCentered("SUSPECT CAR", x + w / 2, y + h + 3, new Color(140, 160, 195));
        }

        // ------------------------------------------------------------------ race

        private void DrawRace()
        {
            if (_race == null)
            {
                return;
            }

            ThemePalette palette = _art.Palette(_mission.Theme);
            _roadRenderer.ScreenWidth = _canvasWidth;
            _roadRenderer.OriginX = _originX;

            float shakeAmount = (_shake * 3f + (_race.OffRoad ? 1.4f : 0f)) * S;
            float shakeX = MathF.Sin(_time * 90f) * shakeAmount;
            float shakeY = MathF.Cos(_time * 73f) * shakeAmount * 0.6f;

            BuildWorldSprites();

            _roadRenderer.SkylineOffset = _skyOffset;
            _roadRenderer.Render(_spriteBatch, _white, _art, _race.Road, palette, _mission.Theme,
                _race.PlayerZ, _race.PlayerX, _race.Road.HeightAt(_race.PlayerZ) + 1000f,
                shakeX, shakeY, _worldSprites);

            DrawWeather(palette);
            DrawPlayerCar(shakeX, shakeY);
            DrawHud();

            if (_screen == GameScreen.Racing && _countdown > 0f)
            {
                TextCentered("STAGE " + (_campaignIndex + 1) + " - " + _mission.Title, 160, 74,
                    new Color(200, 215, 245));

                int second = (int)MathF.Ceiling(_countdown);
                if (second > 0)
                {
                    TextShadow(second.ToString(), 160, 96, new Color(250, 210, 80), 6);
                }
                else
                {
                    TextShadow("GO!", 160, 96, new Color(120, 250, 140), 5);
                }
            }

            if (_screen == GameScreen.Racing && _race != null && _race.GunWarning > 0f)
            {
                float pulse = 0.5f + 0.5f * MathF.Sin(_time * 26f);
                Color warn = new Color(255, 70, 70) * (0.30f + 0.45f * pulse);
                FullRect(0, 3, warn);
                FullRect(VirtualHeight - 4, 4, warn);
            }

            if (_screen == GameScreen.Paused)
            {
                FullRect(0, VirtualHeight, new Color(0, 0, 0, 170));
                TextShadow("PAUSED", 160, 100, Color.White, 4);
                TextCentered(MobilePlatform ? "TAP TO RESUME" : "FIRE TO RESUME - BACK TO QUIT", 160, 140,
                    new Color(190, 200, 230));
            }
        }

        private void DrawPlayerCar(float shakeX, float shakeY)
        {
            int baseY = 164 * S;
            float crash = Math.Clamp(_crashTimer / CrashFlashTime, 0f, 1f);
            int lean = (int)(_input.Steer * 4f) * S;
            int bump = (int)(MathF.Sin(_time * 18f) * 1.2f * Math.Clamp(_race.SpeedRatio, 0f, 1f) * S);

            // A shunt kicks the car sideways and makes it shudder for a moment.
            int wobble = (int)(MathF.Sin(_time * 46f) * 11f * crash * _crashSide) * S;
            int jolt = (int)(MathF.Sin(_time * 31f) * 3f * crash) * S;

            Texture2D car = _input.Steer < -0.2f ? _art.PlayerCarLeft
                          : _input.Steer > 0.2f ? _art.PlayerCarRight
                          : _art.PlayerCar;
            int x = 160 * S - car.Width / 2 + lean + wobble + (int)(shakeX * 0.6f);
            int y = baseY + bump + jolt + (int)(shakeY * 0.6f);
            Color tint = crash > 0f && MathF.Sin(_time * 60f) > 0f
                ? new Color(255, 190, 165)
                : Color.White;

            _spriteBatch.Draw(_art.Shadow, new Rectangle(x + 4 * S, y + 34 * S, 56 * S, 12 * S), Color.White);
            _spriteBatch.Draw(car, new Rectangle(x, y, car.Width, car.Height), tint);

            if (crash > 0f)
            {
                DrawCrashSparks(160 * S + wobble, y + 12 * S, crash);
            }

            // Brake lights glow.
            if (_input.Brake)
            {
                _spriteBatch.Draw(_white, new Rectangle(x + 8 * S, y + 18 * S, 10 * S, 4 * S), new Color(255, 90, 90, 180));
                _spriteBatch.Draw(_white, new Rectangle(x + 46 * S, y + 18 * S, 10 * S, 4 * S), new Color(255, 90, 90, 180));
            }
        }

        private void DrawCrashSparks(int centreX, int centreY, float life)
        {
            for (int i = 0; i < 14; i++)
            {
                float angle = i * 0.45f + _time * 2.2f;
                float distance = (1f - life) * 70f + 10f;
                int x = centreX + (int)(MathF.Cos(angle) * distance) * S;
                int y = centreY + (int)(MathF.Sin(angle) * distance * 0.55f) * S;
                int size = Math.Max(1, (int)(4f * life)) * S;
                int alpha = (int)(230f * life);
                _spriteBatch.Draw(_white, new Rectangle(x, y, size, size),
                    new Color(255, 220, 130, alpha));
            }

            FullRect(0, 2, new Color(255, 90, 70, (int)(120f * life)));
        }

        // The road is 4000 world units wide. A car is a shade over a lane, which is the
        // same on-screen width the player's own sprite has at the bottom of the road, so
        // traffic, suspects and the player all read at one scale.
        private const float CarWorldWidth = 430f;
        private const float VanWorldWidth = 520f;

        private void BuildWorldSprites()
        {
            _worldSprites.Clear();

            Road road = _race.Road;
            float cameraZ = _race.PlayerZ;
            float horizon = RoadRenderer.DrawDistance * Road.SegmentLength;

            // Roadside props.
            IReadOnlyList<RoadProp> props = road.Props;
            for (int i = 0; i < props.Count; i++)
            {
                RoadProp prop = props[i];
                float z = prop.Segment * Road.SegmentLength;
                float dz = z - cameraZ;
                if (dz < 900f || dz > horizon)
                {
                    continue;
                }

                _worldSprites.Add(new WorldSprite
                {
                    Z = z,
                    X = prop.X,
                    Texture = _art.Prop(prop.Kind, prop.Variant),
                    WorldWidth = Art.PropWorldWidth(prop.Kind),
                    Tint = Color.White
                });
            }

            // Target car.
            Texture2D suspectTexture = _art.SuspectCars[Math.Clamp(_mission.SuspectColor, 0, _art.SuspectCars.Length - 1)];
            _worldSprites.Add(new WorldSprite
            {
                Z = _race.RivalZ,
                X = _race.RivalX,
                Texture = suspectTexture,
                WorldWidth = CarWorldWidth,
                Tint = Color.White
            });

            // Traffic.
            for (int i = 0; i < _race.Traffic.Count; i++)
            {
                TrafficCar car = _race.Traffic[i];
                float dz = car.Z - cameraZ;
                if (dz < 900f || dz > horizon)
                {
                    continue;
                }

                _worldSprites.Add(new WorldSprite
                {
                    Z = car.Z,
                    X = car.X,
                    Texture = car.SameDirection ? _art.TrafficCars[car.Kind % _art.TrafficCars.Length]
                                               : _art.OncomingCars[car.Kind % _art.OncomingCars.Length],
                    WorldWidth = car.Kind == 2 ? VanWorldWidth : CarWorldWidth,
                    Tint = Color.White
                });
            }
        }

        private void DrawWeather(ThemePalette palette)
        {
            if (_mission.Weather == Weather.Clear)
            {
                return;
            }

            int count = _mission.Weather == Weather.Rain ? 80 : _mission.Weather == Weather.Snow ? 60 : 100;
            count = count * _canvasWidth / RenderSettings.Width;

            // Weather falls across the whole screen, not only the centred picture.
            float span = _canvasWidth / (float)S;

            for (int i = 0; i < count; i++)
            {
                float seedX = -OriginLogical + (i * 97.3f) % span;
                float seedY = (i * 53.7f) % 200f;
                float speed = _mission.Weather == Weather.Sandstorm ? 220f : 180f;
                float x = seedX;
                float y = (seedY + _time * speed) % 220f;

                if (_mission.Weather == Weather.Rain)
                {
                    _spriteBatch.Draw(_art.RainDrop(), new Rectangle((int)(x * S), (int)(y * S), 2 * S, 8 * S), Color.White * 0.75f);
                }
                else if (_mission.Weather == Weather.Snow)
                {
                    float drift = MathF.Sin(_time + i) * 6f;
                    _spriteBatch.Draw(_art.SnowFlake(), new Rectangle((int)((x + drift) * S), (int)(y * S), 3 * S, 3 * S), Color.White);
                }
                else
                {
                    _spriteBatch.Draw(_white, new Rectangle((int)(x * S), (int)(y * S), 14 * S, S),
                        new Color(240, 210, 160, 90));
                }
            }

            if (_mission.Weather == Weather.Sandstorm)
            {
                FullRect(18, 190, new Color(220, 180, 120, 30));
            }

            if (_mission.Weather == Weather.Snow)
            {
                FullRect(18, 190, new Color(200, 220, 255, 18));
            }
        }

        private void DrawHud()
        {
            // Top HUD modelled on the arcade original: a white-framed score / timer / speed
            // panel, the stage readout underneath, and the HQ video link beside it.
            FullRect(0, 48, new Color(6, 8, 18));
            HudFrame(2, 2, 220, 44, new Color(236, 240, 248));
            HudFrame(226, 2, 92, 44, new Color(236, 240, 248));

            Color label = new Color(96, 190, 214);
            Color digits = new Color(120, 240, 255);
            Color stage = new Color(120, 250, 140);

            Text("SCORE", 8, 7, label);
            Text(TotalScore().ToString("D7"), 38, 7, digits);

            float time = _race.TimeRemaining;
            Color timeColor = time < 10f && Blink(4f) ? new Color(255, 90, 90) : new Color(250, 210, 80);
            Text("TIME", 96, 7, label);
            Text(((int)MathF.Ceiling(time)).ToString("D3"), 122, 7, timeColor);

            Text("SPEED", 154, 7, label);
            Text(((int)(_race.Speed / Race.MaxSpeed * 180f)).ToString("D3"), 192, 7, digits);

            Text("STAGE", 8, 28, stage);
            Text((_campaignIndex + 1) + "/" + Mission.All.Count, 42, 28, stage);

            Text("GEAR", 86, 28, stage);
            Text(_race.GearLabel, 114, 28,
                _gearFlash > 0f ? new Color(250, 210, 80) : stage);

            Text("RANGE", 150, 28, stage);
            Text(((int)(_race.Gap / 10f)).ToString("D4") + "M", 186, 28, stage);

            // HQ video link: the dispatcher above, the player below, as in the original.
            int dispatcherFrame = _radioTimer > 0f ? 1 + (int)(_time * 10f) % 3 : 0;
            Image(_art.HqOperatorFrames[dispatcherFrame], 230, 5, 26, 16);
            Image(_art.PlayerPortrait, 230, 25, 26, 16);
            Text("HQ", 260, 7, new Color(120, 200, 240));
            Text("YOU", 260, 27, new Color(180, 210, 240));

            if (_radioFlash > 0f)
            {
                HudFrame(228, 4, 88, 20, new Color(250, 210, 80));
            }

            // Bottom console.
            FullRect(208, VirtualHeight - 208, new Color(8, 10, 22));
            FullRect(208, 1, new Color(60, 70, 110));

            Text("TURBO", 8, 214, new Color(140, 150, 185));
            Bar(48, 215, 100, 8, _race.Turbo, new Color(24, 28, 44),
                new Color(80, 150, 255), new Color(60, 70, 110));

            Text("SUSPECT", 160, 212, new Color(140, 150, 185));
            for (int i = 0; i < _race.RivalMaxHealth; i++)
            {
                int x = 160 + i * 13;
                Rect(x, 222, 11, 8, i < _race.RivalHealth ? new Color(230, 60, 60) : new Color(40, 44, 60));
            }

            Text("HULL", 160, 232, new Color(140, 150, 185));
            for (int i = 0; i < _race.PlayerMaxHealth; i++)
            {
                Rect(186 + i * 10, 233, 7, 7,
                    i < _race.PlayerHealth ? new Color(90, 220, 140) : new Color(40, 44, 60));
            }

            Text("HI " + _scores.BestScore().ToString("D6"), 250, 212, new Color(250, 210, 80));

            if (_radioTimer > 0f && !string.IsNullOrEmpty(_radioText))
            {
                string message = "HQ " + _radioText;
                int w = _font.Measure(message, 1) + 18;
                int x = 160 - w / 2;
                Panel(x, 51, w, 15, new Color(6, 8, 18, 235), new Color(236, 240, 248));
                TextCentered(message, 160, 55,
                    _radioFlash > 0f ? Color.White : new Color(250, 220, 150), 1);
            }

            if (_PointerSeen && _screen != GameScreen.Paused && (MobilePlatform || _selfTest))
            {
                DrawTouchControls();
            }
        }

        private void HudFrame(int x, int y, int w, int h, Color color)
        {
            Rect(x, y, w, 1, color);
            Rect(x, y + h - 1, w, 1, color);
            Rect(x, y, 1, h, color);
            Rect(x + w - 1, y, 1, h, color);
        }

        // Touch pads live in the two bottom corners, under each thumb. The renderer and the hit
        // test share these rectangles so a button can never drift away from where it is drawn.
        private static readonly Rectangle SteerLeftPad = new Rectangle(4, 146, 60, 60);
        private static readonly Rectangle SteerRightPad = new Rectangle(68, 146, 60, 60);
        private static readonly Rectangle BrakePad = new Rectangle(198, 146, 48, 60);
        private static readonly Rectangle TurboPad = new Rectangle(250, 146, 64, 60);
        // Manual-gearbox button, shown above TURBO only when the player has opted in.
        private static readonly Rectangle GearPad = new Rectangle(250, 108, 64, 32);

        private void DrawTouchControls()
        {
            DrawTouchPad(SteerLeftPad, "<", 2);
            DrawTouchPad(SteerRightPad, ">", 2);
            DrawTouchPad(BrakePad, "BRK", 1);
            DrawTouchPad(TurboPad, "TURBO", 1);

            if (_settings.Value.ManualGears)
            {
                DrawTouchPad(GearPad, "GEAR " + _race.GearLabel, 1);
            }
        }

        private void DrawTouchPad(Rectangle rectangle, string label, int scale)
        {
            Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height, new Color(10, 16, 32, 150));
            Rect(rectangle.X, rectangle.Y, rectangle.Width, 2, new Color(150, 185, 245, 210));
            Rect(rectangle.X, rectangle.Y + rectangle.Height - 2, rectangle.Width, 2, new Color(150, 185, 245, 210));
            Rect(rectangle.X, rectangle.Y, 2, rectangle.Height, new Color(150, 185, 245, 210));
            Rect(rectangle.X + rectangle.Width - 2, rectangle.Y, 2, rectangle.Height, new Color(150, 185, 245, 210));
            TextCentered(label, rectangle.X + rectangle.Width / 2,
                rectangle.Y + rectangle.Height / 2 - (scale > 1 ? 6 : 2), new Color(220, 235, 255, 240), scale);
        }

        private int TotalScore()
        {
            int raw = _campaignRaw + (_race?.RawScore ?? 0);
            return DifficultyRules.Weight(_difficulty, raw);
        }

        private void DrawBustedOverlay()
        {
            FullRect(0, VirtualHeight, new Color(0, 0, 0, 150));

            int wobble = (int)(MathF.Sin(_time * 12f) * 2f);
            Panel(36, 62 + wobble, 248, 104, new Color(8, 14, 30, 240), new Color(120, 240, 140));

            TextShadow("BUSTED!", 160, 70 + wobble, new Color(120, 250, 150), 4);
            TextCentered("SUSPECT " + _mission.SuspectName + " APPREHENDED", 160, 102 + wobble, Color.White);
            TextCentered("RAMS " + _race.Rams + "   +" + _race.RawScore.ToString("D5") + " PTS",
                160, 118 + wobble, new Color(250, 210, 80));

            if (Blink(3f))
            {
                TextCentered(ConfirmPrompt + " TO CONTINUE", 160, 138 + wobble, new Color(180, 200, 240));
            }
        }

        private void DrawMissionCleared()
        {
            FullRect(0, VirtualHeight, new Color(0, 0, 0, 170));
            Panel(30, 58, 260, 110, new Color(8, 14, 30, 245), new Color(250, 210, 80));

            TextShadow("MISSION CLEARED", 160, 68, new Color(250, 220, 120), 2);
            TextCentered(_mission.Title, 160, 92, Color.White);
            TextCentered("SCORE " + TotalScore().ToString("D6"), 160, 108, new Color(180, 220, 255), 1);

            Mission next = _campaignIndex + 1 < Mission.All.Count ? Mission.All[_campaignIndex + 1] : null;
            if (next != null)
            {
                TextCentered("NEXT: " + next.Title, 160, 126, new Color(250, 160, 160));
            }

            if (Blink(2.5f))
            {
                TextCentered(ConfirmPrompt + " TO CONTINUE", 160, 148, new Color(180, 200, 240));
            }
        }

        private void DrawCampaignComplete()
        {
            Background(MissionTheme.Neon);
            SirenStrip(0);

            Panel(20, 30, 280, 180, new Color(8, 10, 26, 240), new Color(250, 210, 80));
            TextShadow("ALL MISSIONS CLEARED", 160, 42, new Color(120, 250, 150), 2);
            TextCentered("THE CITY IS SAFE... FOR NOW", 160, 68, new Color(180, 200, 235));

            TextCentered("FINAL SCORE", 160, 92, new Color(140, 150, 185));
            TextCentered(DifficultyRules.Weight(_difficulty, _campaignRaw).ToString("D6"), 160, 104,
                new Color(250, 210, 80), 3);

            TextCentered("SKILL " + DifficultyRules.Name(_difficulty), 160, 136, Color.White);
            TextCentered("MISSIONS " + Mission.All.Count + "   RAMS " + TotalRams(), 160, 150,
                new Color(160, 180, 215));

            if (Blink(2f))
            {
                TextCentered(ConfirmPrompt + " TO FILE THE REPORT", 160, 176, new Color(200, 220, 250));
            }
        }

        private int TotalRams()
        {
            int rams = _race?.Rams ?? 0;
            return rams + Math.Max(0, _campaignIndex) * 3;
        }

        private void DrawGameOver()
        {
            Background(MissionTheme.Harbour);
            FullRect(0, VirtualHeight, new Color(0, 0, 0, 120));
            SirenStrip(0);

            // The game over card.
            Panel(26, 34, 268, 176, new Color(10, 12, 26, 245), new Color(220, 60, 70));
            Rect(26, 34, 268, 3, new Color(250, 210, 80));

            TextShadow("GAME OVER", 160, 46, new Color(250, 90, 90), 4);
            TextCentered("PURSUIT FAILED", 160, 80, new Color(200, 210, 235));

            if (_race?.Wrecked == true)
            {
                Text("CAUSE", 44, 100, new Color(130, 140, 175));
                Text(_race.WreckedBy == WreckCause.Gunfire ? "SHOT BY THE SUSPECT" : "RAMMED A CAR",
                    44, 110, new Color(255, 160, 160));
                DrawWreckedCar(190, 96, 88, 62);
            }
            else
            {
                Text("SUSPECT", 44, 100, new Color(130, 140, 175));
                Text(_mission.SuspectName + " GOT AWAY", 44, 110, new Color(255, 160, 160));
            }

            Text("STAGE " + (_mission.Index + 1) + "/" + Mission.All.Count +
                 "   RAMS " + (_race?.Rams ?? 0), 44, 128, new Color(160, 180, 215));
            Text(_mission.Title, 44, 138, Color.White);

            Text("SCORE", 44, 156, new Color(130, 140, 175));
            Text(DifficultyRules.Weight(_difficulty, _campaignRaw).ToString("D6"), 44, 166,
                new Color(250, 210, 80), 2);

            if (_race?.Wrecked != true)
            {
                Text("SKILL " + DifficultyRules.Name(_difficulty), 180, 100, new Color(160, 180, 215));
                Text("CHECKPOINTS " + (_race?.Checkpoints ?? 0), 180, 114, new Color(160, 180, 215));
            }

            if (Blink(2f))
            {
                TextCentered(ConfirmPrompt + " TO SEE THE HIGH SCORES", 160, 190, new Color(210, 225, 250));
            }
        }

        private void DrawWreckedCar(int x, int y, int w, int h)
        {
            Rect(x - 6, y + h - 4, w + 12, 6, new Color(0, 0, 0, 130));

            _spriteBatch.Draw(_art.PlayerCar, new Rectangle(x * S, y * S, w * S, h * S),
                new Color(165, 162, 168));

            // Dents in the bodywork.
            Rect(x + w / 4, y + h / 3, w / 6, 4, new Color(70, 66, 70, 200));
            Rect(x + w / 2, y + h / 2, w / 7, 3, new Color(70, 66, 70, 200));

            // Smoke curling off the bonnet.
            for (int i = 0; i < 5; i++)
            {
                float t = (_time * 0.55f + i * 0.21f) % 1f;
                int size = 3 + (int)(t * 7f);
                int sx = x + w / 2 - 4 + (int)(MathF.Sin(_time * 2.3f + i) * 9f);
                int sy = y + 6 - (int)(t * 40f);
                Rect(sx, sy, size, size, new Color(140, 138, 150, (int)(130f * (1f - t))));
            }

            int flicker = (int)(MathF.Sin(_time * 17f) * 2f);
            Rect(x + w / 3, y + h / 2 + flicker, 6, 6, new Color(255, 150, 50, 210));
            Rect(x + w / 3 + 2, y + h / 2 + 1 + flicker, 3, 4, new Color(255, 230, 140, 230));
        }

        private void DrawNameEntry()
        {
            Background(MissionTheme.Neon);
            SirenStrip(0);

            Panel(24, 40, 272, 140, new Color(8, 10, 26, 240), new Color(250, 210, 80));
            TextShadow("NEW HIGH SCORE", 160, 48, new Color(250, 220, 120), 2);
            TextCentered("SCORE " + _pendingEntry.Score.ToString("D6"), 160, 68, Color.White);

            string summary = _pendingEntry.RunSummary;
            Color summaryColor = _pendingEntry.Outcome == "ALL STAGES CLEARED"
                ? new Color(140, 250, 160)
                : new Color(255, 165, 165);

            bool touchUi = MobilePlatform || _PointerSeen;

            if (touchUi)
            {
                DrawKey(NameUpButton, "UP");
            }

            for (int i = 0; i < 3; i++)
            {
                Rectangle box = NameBox(i);
                bool selected = i == _nameCursor;
                Panel(box.X, box.Y, box.Width, box.Height,
                    selected ? new Color(40, 50, 100) : new Color(14, 18, 34),
                    selected ? new Color(250, 210, 80) : new Color(70, 80, 110));
                TextCentered(_nameLetters[i].ToString(), box.X + box.Width / 2, box.Y + 9, Color.White, 2);
            }

            if (touchUi)
            {
                DrawKey(NameDownButton, "DOWN");
                DrawKey(NameOkButton, "OK");
                TextCentered("TAP A BOX - UP/DOWN CHANGES IT", 160, 153, new Color(150, 170, 210));
                TextCentered(string.IsNullOrEmpty(summary) ? "OK ENTERS THE NAME" : summary,
                    160, 164, summaryColor);
            }
            else
            {
                if (!string.IsNullOrEmpty(summary))
                {
                    TextCentered(summary, 160, 82, summaryColor);
                }

                TextCentered("UP/DOWN LETTER - LEFT/RIGHT MOVE", 160, 146, new Color(150, 170, 210));

                if (Blink(2f))
                {
                    TextCentered("FIRE TO CONFIRM", 160, 158, new Color(200, 220, 250));
                }
            }
        }

        private void DrawKey(Rectangle rect, string label)
        {
            Rect(rect.X, rect.Y, rect.Width, rect.Height, new Color(30, 38, 64, 220));
            Rect(rect.X, rect.Y, rect.Width, 1, new Color(120, 150, 210, 150));
            Rect(rect.X, rect.Y + rect.Height - 1, rect.Width, 1, new Color(120, 150, 210, 150));
            TextCentered(label, rect.X + rect.Width / 2, rect.Y + rect.Height / 2 - 3, new Color(200, 220, 250));
        }

        private void DrawHighScores()
        {
            Background(MissionTheme.Neon);
            SirenStrip(0);

            TextShadow("HALL OF FAME", 160, 8, new Color(250, 220, 120), 2);

            Panel(8, 28, 304, 186, new Color(8, 10, 26, 240), new Color(250, 210, 80));

            Text("RANK", 22, 34, new Color(140, 150, 185));
            Text("SCORE", 90, 34, new Color(140, 150, 185));
            Text("STAGE", 172, 34, new Color(140, 150, 185));
            Text("NAME", 244, 34, new Color(140, 150, 185));
            Rect(14, 44, 292, 1, new Color(60, 70, 110));

            HighScoreTable table = _scores.Table;
            if (table.Entries.Count == 0)
            {
                TextCentered("NO SCORES YET - SET THE PACE", 160, 104, new Color(160, 170, 200));
            }

            for (int i = 0; i < table.Entries.Count && i < HighScoreTable.MaxEntries; i++)
            {
                ScoreEntry entry = table.Entries[i];
                int y = 50 + i * 14;
                bool highlight = i == _highlightIndex;
                if (highlight)
                {
                    Rect(12, y - 2, 296, 15, new Color(50, 40, 20));
                }

                Color rankColor = i == 0 ? new Color(250, 210, 80) : i == 1 ? new Color(210, 215, 230) : i == 2 ? new Color(220, 160, 100) : new Color(160, 170, 200);
                Text((i + 1).ToString("D2"), 24, y, rankColor);
                Text(entry.Score.ToString("D6"), 90, y, highlight ? new Color(250, 210, 80) : Color.White);
                Text(entry.StageLabel, 172, y, new Color(120, 220, 150));
                Text(entry.Name, 244, y, highlight ? Color.White : new Color(220, 225, 240));
            }

            ScoreEntry highlighted = _highlightIndex >= 0 && _highlightIndex < table.Entries.Count
                ? table.Entries[_highlightIndex]
                : null;
            if (highlighted != null)
            {
                TextCentered(highlighted.DifficultyLabel + " - " + highlighted.MissionLabel,
                    160, 192, new Color(160, 190, 230));
                TextCentered(string.IsNullOrEmpty(highlighted.RunSummary) ? "LEGACY ENTRY" : highlighted.RunSummary,
                    160, 202, new Color(250, 200, 200));
            }
            else
            {
                TextCentered("TOP 10 - SCORE IS WEIGHTED BY SKILL", 160, 200, new Color(140, 150, 185));
            }
            if (Blink(2f))
            {
                TextCentered(ConfirmPrompt + " TO RETURN", 160, 218, Color.White);
            }
        }

    }
}
