using BepInEx;
using System.Diagnostics;
using System;
using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;

namespace com.strategineer.PEBSpeedrunTools
{
    class TextGUI
    {
        const int SCREEN_OFFSET = 10;
        const int SHADOW_OFFSET = 2;

        private string _text;
        private GUIStyle _style = new GUIStyle();
        private GUIStyle _shadowStyle = new GUIStyle();
        private Rect _rect;
        private Rect _shadowRect;

        private bool _shouldShow;

        private TextAnchor _anchor;
        public TextGUI() : this(TextAnchor.UpperLeft, Color.yellow, "") { }
        public TextGUI(TextAnchor anchor, Color color, string text)
        {
            _shouldShow = true;
            _text = text;
            _anchor = anchor;

            int w = Screen.width, h = Screen.height;
            _rect = _shadowRect = new Rect(SCREEN_OFFSET, SCREEN_OFFSET, w - SCREEN_OFFSET * 2, h - SCREEN_OFFSET * 2);
            _shadowRect.x += SHADOW_OFFSET;
            _shadowRect.y += SHADOW_OFFSET;

            _style.fontSize = _shadowStyle.fontSize = h / 40;
            SetAnchor(anchor);
            SetColor(color);
            SetShadowColor(Color.black);
        }

        public void SetColor(Color color)
        {
            _style.normal.textColor = color;
        }
        public void SetShadowColor(Color color)
        {
            _shadowStyle.normal.textColor = color;
        }

        public void SetAnchor(TextAnchor anchor)
        {
            _style.alignment = _shadowStyle.alignment = anchor;
        }

        public void SetText(string text)
        {
            this._text = text;
        }

        public void SetActive(bool shouldShow)
        {
            _shouldShow = shouldShow;
        }

        public void Draw()
        {
            if (_shouldShow)
            {
                GUI.Label(_shadowRect, _text, _shadowStyle);
                GUI.Label(_rect, _text, _style);
            }
        }
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("pigEatBallGame.exe")]
    [HarmonyPatch]
    public class Plugin : BaseUnityPlugin
    {
        private static Harmony h;
        private static Stopwatch speedrunTimer = new Stopwatch();
        private static ConfigEntry<KeyCode> _kbmKeyToNotSkipLevelStart;
        private static ConfigEntry<TextAnchor> _timerPosition;
        private static ConfigEntry<TextAnchor> _debugMsgPosition;
        private static ConfigEntry<bool> _showTimer;
        private static ConfigEntry<bool> _showDebugText;
        private static ConfigEntry<bool> _speedrunModeEnabled;

        private static bool _gameLoaded = false;

        private static TextGUI _timerText = new TextGUI();
        private static TextGUI _debugText = new TextGUI();
        private static TextGUI _startupText = new TextGUI(TextAnchor.LowerCenter, Color.grey, $"strategineer's Pig Eat Ball Speedrun Tools version {PluginInfo.PLUGIN_VERSION} loaded.");
        private static bool _playerWantsToSkipLevelStart = false;
        private static bool _playerWantsLevelStart = false;
        private static bool _gameStarted = false;
        private static Stopwatch _playerWantsLevelStartStopwatch = new Stopwatch();
        private static bool _levelStartSkipped = false;
        private static LevelStartScreen levelStartScreen;
        private static bool _movementDetectedOnPauseMenu = false;

        static void Log(string msg)
        {
            Console.WriteLine(msg);
        }

        static void Debug(string msg)
        {
            _debugText.SetText(msg);
        }

        static void StartTimerIfNeeded(string reason)
        {
            if (!speedrunTimer.IsRunning && _gameStarted)
            {
                Log($"Starting timer because of {reason}");
                speedrunTimer.Start();

            }
        }
        static void StopTimerIfNeeded(string reason)
        {
            if (speedrunTimer.IsRunning && _gameStarted)
            {
                Log($"Stopping timer because of {reason}");
                speedrunTimer.Stop();
            }
        }
        static void ResetTimer(string reason)
        {
            Log($"Resetting timer because of {reason}");
            speedrunTimer.Reset();
        }

        [HarmonyPatch]
        class Patch1
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MidGame), "FinishBeginMainGame")]
            static void PostfixMidGameFinishBeginMainGame()
            {
                _gameStarted = true;
                PigEatBallGame.staticThis.gameSettings.enableLeaderboards = false;
            }

            /// <summary>
            /// Just skip talking to the clam and start playing the level right away
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuClamTalkStart), nameof(MenuClamTalkStart.DoStartup))]
            static void PostfixSkipClamTalk(ref MenuClamTalkStart __instance)
            {
                Log($"Skipping clam talk");
                if (MidGame.staticMidGame.getCurrentMenu() is MenuClamTalkStart)
                {
                    __instance.callFunctionDelayed(13);
                }
            }


            /// <summary>
            /// Just skip the win screen and play the next level or return to the world screen if we've beaten the level
            ///   Otherwise, let's restart the current level
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuWinScreen), nameof(MenuWinScreen.DoStartup))]
            static void PostfixSkipWinScreen(ref MenuWinScreen __instance, bool ___singlePlayerFailure, ref int ___currentState, ref float ___currentStateTime)
            {
                Log("Skipping win screen");
                ___currentStateTime = 100000f;
                ___currentState = 42;
                var stateNumber = ___singlePlayerFailure ? 1 : 0;
                // stateNumber 0 to move on, stateNumber 1 to restart the current level
                __instance.callFunction(new GenericButtonActioner(stateNumber, 0));
            }

            /// <summary>
            /// This starts the timer if we're on a pause menu and then we select a different button
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuBase), nameof(MenuBase.setCurrentButton))]
            static void PostfixMenuPauseSetCurrentButton(MenuBase __instance, ButtonBase ___lastButton, ButtonBase ___currentButton)
            {
                if (__instance is MenuPause || __instance is MenuPauseInGame || __instance is MenuDisguises)
                {
                    if (!speedrunTimer.IsRunning
                        && ___lastButton != null
                        && ___currentButton != null
                        && ___lastButton != ___currentButton)
                    {
                        _movementDetectedOnPauseMenu = true;
                    }
                }
            }

            /// <summary>
            /// This starts the timer if we're on a pause menu and go into the disguises menu
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuBase), nameof(MenuBase.setSubMenu))]
            static void PostfixMenuPauseSetSubMenu(MenuBase __instance, MenuBase nextMenu)
            {
                if (!speedrunTimer.IsRunning
                    && __instance is MenuPause
                    && nextMenu is MenuDisguises)
                {
                    _movementDetectedOnPauseMenu = true;    
                }
            }

            /// <summary>
            /// display info on my mod tools, saying that it's properly loaded here and then remove it
            ///   when the menu changesThis starts the timer if we're on a pause menu and then we select a different button
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuStartGame), nameof(MenuStartGame.LoadContent))]
            static void PostfixMenuStartGameLoadContent()
            {
                _gameLoaded = true;
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(LevelStartScreen), nameof(LevelStartScreen.SetState))]
            static void PatchLevelStartScreenSetState(ref LevelStartScreen __instance)
            {
                if (__instance.currentState == LevelStartScreen.STATE_START_UP)
                {
                    if (!_playerWantsToSkipLevelStart)
                    {
                        if (_playerWantsLevelStart)
                        {
                            Log("Detected pre-buffered menu movement, don't skip level start menu.");
                        }
                        else
                        {
                            Log("No pre-buffered menu movement detected, skipping the level start menu.");
                            _playerWantsToSkipLevelStart = true;
                            levelStartScreen = __instance;
                                
                        }
                    }
                }
                else if (__instance.currentState == LevelStartScreen.STATE_OFF)
                {
                    _playerWantsToSkipLevelStart = false;
                    _levelStartSkipped = false;
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(LevelStartScreen), nameof(LevelStartScreen.MenuDraw))]
            [HarmonyPatch(typeof(LevelStartScreen), "UpdateBallView")]
            [HarmonyPatch(typeof(PigMenu), nameof(PigMenu.MenuDrawBackground))]
            [HarmonyPatch(typeof(PigMenu), nameof(PigMenu.DrawDarkOverlay))]
            static bool PatchSkipDrawingInGameMenusWhenSkippingTheLevelStart(PigMenu __instance)
            {
                return !_playerWantsToSkipLevelStart;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.SetFullScreenDark))]
            [HarmonyPatch(typeof(MenuWinScreen), nameof(MenuWinScreen.MenuDraw))]
            static bool PatchSkipDrawingInGameMenusWhenSkippingTheLevelStart2()
            {
                return false;
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.setCurrentMenu))]
            static void PrefixMidGameSetCurrentMenu(MenuBase _menu, ref MidGame __instance, MenuBase ___pauseMenu, MenuBase ___pauseMenuInGame, ref MenuBase ___currentMenu)
            {
                if (___currentMenu == ___pauseMenu || ___currentMenu == ___pauseMenuInGame)
                {
                    if( _menu != ___currentMenu)
                    {
                        _movementDetectedOnPauseMenu = false;
                    }
                }
                if (_menu is MenuFront)
                {
                    StopTimerIfNeeded("Back to front menu");
                    speedrunTimer.Reset();
                    _gameStarted = false;
                }
            }
        }

        private void Awake()
        {
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");


            _kbmKeyToNotSkipLevelStart = Config.Bind("Controls",
                "Key to prevent utomatic level start menu skipping (for KBM only)",
                KeyCode.Z,
                "Keyboard Key to use to prevent the automatic skipping of the level start menu (allowing you to pick a disguise/powerup).");


            _timerPosition = Config.Bind("Interface",
                "Timer position",
                TextAnchor.UpperLeft,
                "Which corner of the screen to display the timer in.");

            _timerPosition.SettingChanged += (sender, args) => UpdateTextUIs();

            _debugMsgPosition = Config.Bind("Interface",
                "Debug Message position",
                TextAnchor.LowerRight,
                "Which corner of the screen to display the last debug message in.");

            _debugMsgPosition.SettingChanged += (sender, args) => UpdateTextUIs();

            _showTimer = Config.Bind("Show/Hide",
                "Show Timer",
                true,
                "Should show the speedrun timer?");

            _showTimer.SettingChanged += (sender, args) => UpdateTextUIs();

            _showDebugText = Config.Bind("Show/Hide",
                "Show Debug Text",
                false,
                "Should show the debug text?");

            _showDebugText.SettingChanged += (sender, args) => UpdateTextUIs();

            _speedrunModeEnabled = Config.Bind("Speedrun",
                "Speedrun Mode Enabled",
                true,
                "Should we try to automatically skip any menus/dialogs?");

            _speedrunModeEnabled.SettingChanged += (sender, args) => UpdateConfigSpeedrunModeEnabled();
        }
        private void Start()
        {
            UpdateConfigSpeedrunModeEnabled();
            UpdateTextUIs();
        }
        private void UpdateTextUIs()
        {
            _timerText.SetAnchor(_timerPosition.Value);
            _timerText.SetActive(_showTimer.Value);

            _debugText.SetAnchor(_debugMsgPosition.Value);
            _debugText.SetActive(_showDebugText.Value);
        }

        private void UpdateConfigSpeedrunModeEnabled()
        {
            if (_speedrunModeEnabled.Value)
            {
                Logger.LogInfo($"Patching game...");
                h = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
            }
            else
            {
                Logger.LogInfo($"Unpatching game...");
                if (h != null)
                {
                    h.UnpatchAll();
                    h = null;
                }
            }
        }
      
        private bool CheckLevelGameplayRunning()
        {
                return MidGame.staticMidGame != null
                && !MidGame.staticMidGame.GameplayPaused()
                && !MidGame.staticMidGame.LevelStartScreenActive()
                && MidGame.staticMidGame.getCurrentMenu() == null
                && !MidGame.staticMidGame.LevelGoalReached()
                && !MidGame.staticMidGame.winScreen.MenuActive();
        }


        private void Update()
        {
            if (!_speedrunModeEnabled.Value) { return; }
            if (CheckLevelGameplayRunning() || _movementDetectedOnPauseMenu)
            {
                StartTimerIfNeeded(_movementDetectedOnPauseMenu ? "Movement on pause menu" : "Gameplay");
            } else
            {
                StopTimerIfNeeded("No gameplay or movement on pause menu");
            }
            if (!_levelStartSkipped && _playerWantsToSkipLevelStart)
            {
                levelStartScreen.SetState(LevelStartScreen.STATE_SHUTDOWN_PRE);
                _levelStartSkipped = true;
            }
            if(_playerWantsLevelStartStopwatch.ElapsedMilliseconds > 500f)
            {
                _playerWantsLevelStart = false;
                _playerWantsLevelStartStopwatch.Reset();
            }
            // Press dpad left or right (or key on kbs) within 500ms of menu opening to prevent skipping the level start menu (to pick a powerup/disguises)
            if(Math.Abs(MidGame.staticMidGame.ActionMoveAxisX(0)) > 0.5f
                || Input.GetKeyDown(_kbmKeyToNotSkipLevelStart.Value))
            {
                _playerWantsLevelStart = true;
                _playerWantsLevelStartStopwatch.Restart();
            }
        }

        private void OnGUI()
        {
            if(!_speedrunModeEnabled.Value) { return; }

            TimeSpan ts = speedrunTimer.Elapsed;
            string elapsedTime;
            if (ts.Hours > 0)
            {
                elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            }
            else
            {
                elapsedTime = String.Format("{0:00}:{1:00}.{2:00}",
                ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            }

            if (_gameLoaded)
            {
                _timerText.SetColor(speedrunTimer.IsRunning ? Color.green : Color.grey);
                _timerText.SetText(elapsedTime);
                _timerText.Draw();
                _debugText.Draw();
                if (ts.TotalSeconds < 5)
                {
                    _startupText.Draw();
                }
            }
        }
    }
}
