using BepInEx;
using System.Diagnostics;
using System;
using UnityEngine;
using BepInEx.Configuration;
using HarmonyLib;
using System.Reflection;

// todo figure out how to start the game world and give the player control faster than normal after the levelstartscreen
// todo investigate timer situation, remove start timer events, or figure out how to make that cleaner (if possible)
namespace com.strategineer.PEBSpeedrunTools
{
    class TextGUI
    {
        const int SCREEN_OFFSET = 10;

        private string _text;
        private GUIStyle _style = new GUIStyle();
        private GUIStyle _shadowStyle = new GUIStyle();
        private static Rect _rect;
        private static Rect _shadowRect;

        private static TextAnchor _anchor;
        public TextGUI() : this(TextAnchor.UpperLeft, Color.yellow, "") { }
        public TextGUI(TextAnchor anchor, Color color, string text)
        {
            _text = text;
            _anchor = anchor;

            int w = Screen.width, h = Screen.height;
            _rect = _shadowRect = new Rect(SCREEN_OFFSET, SCREEN_OFFSET, w - SCREEN_OFFSET * 2, h - SCREEN_OFFSET * 2);
            _shadowRect.x += 3;
            _shadowRect.y += 3;

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

        public void Draw()
        {
            GUI.Label(_shadowRect, _text, _shadowStyle);
            GUI.Label(_rect, _text, _style);
        }
    }

    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("pigEatBallGame.exe")]
    [HarmonyPatch]
    public class Plugin : BaseUnityPlugin
    {
        private static Harmony h;
        private static Stopwatch stopWatch = new Stopwatch();
        private static ConfigEntry<TextAnchor> _timerPosition;
        private static ConfigEntry<TextAnchor> _debugMsgPosition;
        private static ConfigEntry<bool> _showTimer;
        private static ConfigEntry<bool> _showDebugText;
        private static ConfigEntry<bool> _speedrunModeEnabled;

        private static bool _gameLoaded = false;

        private static TextGUI _timerText = new TextGUI();
        private static TextGUI _debugText = new TextGUI();
        private static TextGUI _startupText = new TextGUI(TextAnchor.LowerCenter, Color.green, $"strategineer's Pig Eat Ball Speedrun Tools version {PluginInfo.PLUGIN_VERSION} loaded.");
        private static bool isTimerOn = false;
        private static bool _playerWantsToSkipLevelStart = false;
        private static bool _playerWantsLevelStart = false;
        private static Stopwatch _playerWantsLevelStartStopwatch = new Stopwatch();
        private static bool _levelStartSkipped = false;
        private static LevelStartScreen levelStartScreen;
        private static Stopwatch _playerWantsToSkipLevelStartStopwatch = new Stopwatch();

        static void Log(string msg)
        {
            Console.WriteLine(msg);
            _debugText.SetText(msg);
        }

        static void StartTimerIfNeeded(string reason)
        {
            Log($"StartTimerIfNeeded: {reason}");
            if (!isTimerOn)
            {
                Log($"Starting timer because of {reason}");
                isTimerOn = true;
                stopWatch.Start();

            }
        }
        static void StopTimerIfNeeded(string reason)
        {
            Log($"StopTimerIfNeeded: {reason}");
            if (isTimerOn)
            {
                Log($"Stopping timer because of {reason}");
                stopWatch.Stop();
                isTimerOn = false;
            }
        }
        static void ResetTimer(string reason)
        {       
            Log($"Resetting timer because of {reason}");
            stopWatch.Reset();
            isTimerOn = false;
        }

        [HarmonyPatch]
        class Patch1
        {
            /// <summary>
            /// Start the timer after these functions execute.
            /// 
            /// todo I might not need all of these.
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(LevelObj), nameof(LevelObj.MapLevelUnlockUpdate))]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.BeginFirstLevelFile))]
            [HarmonyPatch(typeof(MidGame), "FinishBeginMainGame")]
            [HarmonyPatch(typeof(LevelObj), nameof(LevelObj.Reset))]
            static void PostfixStartTimer(MethodBase __originalMethod)
            {
                StartTimerIfNeeded(__originalMethod.Name);
            }
            
            /// <summary>
            /// Stop the timer before these functions execute
            /// </summary>
            [HarmonyPrefix]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.StartTestLevelFromFile), new Type[] { typeof(LevelObj), typeof(string), typeof(string), typeof(string) })]
            static void PrefixStopTimer(MethodBase __originalMethod)
            {
                StopTimerIfNeeded(__originalMethod.Name);
            }


            /// <summary>
            /// Just skip talking to the clam and start playing the level right away
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuClamTalkStart), nameof(MenuClamTalkStart.DoStartup))]
            static void PostfixSkipClamTalk(ref MenuClamTalkStart __instance)
            {
                if (_speedrunModeEnabled.Value)
                {
                    Log($"Skipping clam talk");
                    if (MidGame.staticMidGame.getCurrentMenu() is MenuClamTalkStart)
                    {
                        __instance.callFunctionDelayed(13);
                    }
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
                if (_speedrunModeEnabled.Value)
                {
                    Log("Skipping win screen");
                    ___currentStateTime = 100000f;
                    ___currentState = 42;
                    var stateNumber = ___singlePlayerFailure ? 1 : 0;
                    // stateNumber 0 to move on, stateNumber 1 to restart the current level
                    __instance.callFunction(new GenericButtonActioner(stateNumber, 0));
                }
            }

            /// <summary>
            /// This starts the timer if we're on a pause menu and then we select a different button
            /// </summary>
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuBase), nameof(MenuBase.setCurrentButton))]
            static void PostfixMenuPauseSetCurrentButton(MenuBase __instance, ButtonBase ___lastButton, ButtonBase ___currentButton)
            {
                if (__instance is MenuPause || __instance is MenuPauseInGame)
                {
                    if (!isTimerOn
                        && ___lastButton != null
                        && ___currentButton != null
                        && ___lastButton != ___currentButton)
                    {
                        StartTimerIfNeeded("Movement detected in pause menu");
                    }
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
                if (_speedrunModeEnabled.Value)
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
                        _playerWantsToSkipLevelStartStopwatch.Reset();
                        _playerWantsToSkipLevelStart = false;
                        _levelStartSkipped = false;
                    }
                }
            }

            [HarmonyPrefix]
            [HarmonyPatch(typeof(LevelStartScreen), nameof(LevelStartScreen.MenuDraw))]
            [HarmonyPatch(typeof(PigMenu), nameof(PigMenu.MenuDrawBackground))]
            [HarmonyPatch(typeof(LevelStartScreen), "UpdateBallView")]
            [HarmonyPatch(typeof(MenuWinScreen), nameof(MenuWinScreen.MenuDraw))]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.SetFullScreenDark))]
            [HarmonyPatch(typeof(PigMenu), nameof(PigMenu.DrawDarkOverlay))]
            static bool PatchSkipDrawingInGameMenusWhenSkippingTheLevelStart()
            {
                return !_playerWantsToSkipLevelStart;
            }

            /// <summary>
            /// Start and stop the timer when most menus are entered and when any menu is exited.
            /// </summary> 
            [HarmonyPostfix]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.setCurrentMenu))]
            static void PatchMidGameSetCurrentMenu(ref MidGame __instance, MenuBase ___pauseMenu, MenuBase ___pauseMenuInGame, ref MenuBase ___currentMenu)
            {
                Log($"MidGame:SetCurrentMenu {___currentMenu}");
                // this might not be right place for this but we should disable the leaderboards if we're speedrunning
                if (_speedrunModeEnabled.Value)
                {
                    PigEatBallGame.staticThis.gameSettings.enableLeaderboards = false;
                }

                if (___currentMenu != null
                    // we skip these menus so ignore it
                    && ___currentMenu is not MenuClamTalkStart
                    // we can use this to warp (relevant for the speedrun)
                    && ___currentMenu is not MenuPearls
                    // this is the menu that shows up when we launch into a level?
                    && ___currentMenu is not LevelStartScreen)
                {
                    StopTimerIfNeeded($"Menu/Dialog entered {___currentMenu}");
                } else
                {
                    StartTimerIfNeeded("Menu/Dialog exited");
                }
            }
        }

        private void Awake()
        {
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            _timerPosition = Config.Bind("Interface",
                "Timer position",
                TextAnchor.UpperLeft,
                "Which corner of the screen to display the timer in.");

            _debugMsgPosition = Config.Bind("Interface",
                "Debug Message position",
                TextAnchor.LowerRight,
                "Which corner of the screen to display the last debug message in.");

            _showTimer = Config.Bind("Show/Hide",
                "Show Timer",
                true,
                "Should show the speedrun timer?");

            _showDebugText = Config.Bind("Show/Hide",
                "Show Debug Text",
                false,
                "Should show the debug text?");

            _speedrunModeEnabled = Config.Bind("Speedrun",
                "Speedrun Mode Enabled",
                true,
                "Should we try to automatically skip any menus/dialogs?");

            _debugText.SetAnchor(_debugMsgPosition.Value);
            _timerText.SetAnchor(_timerPosition.Value);
        }

        private void Start()
        {
            h = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        }
        
        private void Update()
        {
            if (_speedrunModeEnabled.Value)
            {
                if (!_levelStartSkipped && _playerWantsToSkipLevelStart)
                {
                    levelStartScreen.SetState(251);
                    _levelStartSkipped = true;
                }
                if(_playerWantsLevelStartStopwatch.ElapsedMilliseconds > 500f)
                {
                    _playerWantsLevelStart = false;
                    _playerWantsLevelStartStopwatch.Reset();
                }
                // todo and setup a configurable keyboard key for playing on kbm
                // Hold dpad left or right and remember that for 500ms
                if(Math.Abs(MidGame.staticMidGame.ActionMoveAxisX(0)) > 0.5f)
                {
                    _playerWantsLevelStart = true;
                    _playerWantsLevelStartStopwatch.Restart();
                }
                    
            }
        }

        private void OnGUI()
        {

            TimeSpan ts = stopWatch.Elapsed;
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
                if (_showTimer.Value)
                {
                    _timerText.SetText(elapsedTime);
                    _timerText.Draw();
                }
                if (_showDebugText.Value)
                {
                    _debugText.Draw();
                }
                if (ts.TotalSeconds < 5)
                {
                    _startupText.Draw();
                }
            }
        }
    }
}
