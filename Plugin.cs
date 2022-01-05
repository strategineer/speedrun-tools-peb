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

            _style.fontSize = h / 40;
            SetAnchor(anchor);
            SetColor(color);
            SetColor(Color.black);
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
            _style.alignment = anchor;
        }

        public void SetText(string text)
        {
            this._text = text;
        }

        public void Draw()
        {
            GUI.Label(_rect, _text, _style);
            GUI.Label(_shadowRect, _text, _shadowStyle);
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
        // start text
        private static bool _showStartUpText = false;

        private static TextGUI _timerText = new TextGUI();
        private static TextGUI _debugText = new TextGUI();
        private static TextGUI _startupText = new TextGUI(TextAnchor.MiddleLeft, Color.white, $"strategineer's Pig Eat Ball Speedrun Tools version {PluginInfo.PLUGIN_VERSION} loaded.");
        private static bool isTimerOn = false;

        static void Log(string msg)
        {
            Console.WriteLine(msg);
            _debugText.SetText(msg);
        }

        static void StartTimerIfNeeded(string reason)
        {
            Log($"PatchStartTimer: {reason}");
            if (!isTimerOn)
            {
                Log($"Starting timer because of {reason}");
                isTimerOn = true;
                stopWatch.Start();
            }
        }
        static void StopTimerIfNeeded(string reason)
        {
            Log($"PatchEndTimer: {reason}");
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
            [HarmonyPostfix]
            [HarmonyPatch(typeof(LevelObj), nameof(LevelObj.MapLevelUnlockUpdate))]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.BeginFirstLevelFile))]
            [HarmonyPatch(typeof(MidGame), "FinishBeginMainGame")]
            [HarmonyPatch(typeof(LevelObj), nameof(LevelObj.Reset))]
            static void PatchStartTimer(MethodBase __originalMethod)
            {
                StartTimerIfNeeded(__originalMethod.Name);
            }
            [HarmonyPrefix]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.StartTestLevelFromFile), new Type[] { typeof(LevelObj), typeof(string), typeof(string), typeof(string) })]
            static void PrefixStopTimer(MethodBase __originalMethod)
            {
                StopTimerIfNeeded(__originalMethod.Name);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuClamTalkStart), nameof(MenuClamTalkStart.DoStartup))]
            static void PostfixSkipClamTalk(ref MenuClamTalkStart __instance)
            {
                if (_speedrunModeEnabled.Value)
                {
                    Log($"Skipping clam talk");
                    // Just skip talking to the clam and start playing the level right away
                    if (MidGame.staticMidGame.getCurrentMenu() is MenuClamTalkStart)
                    {
                        __instance.callFunctionDelayed(13);
                    }
                }
            }


            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuWinScreen), nameof(MenuWinScreen.DoStartup))]
            static void PostfixSkipWinScreen(ref MenuWinScreen __instance, bool ___singlePlayerFailure, ref int ___currentState, ref float ___currentStateTime)
            {
                if (_speedrunModeEnabled.Value)
                {
                    Log("Skipping win screen");
                    // Just skip the win screen and play the next level or return to the world screen if we've beaten the level
                    // Otherwise, let's restart the current level
                    ___currentStateTime = 100000f;
                    ___currentState = 42;
                    var stateNumber = ___singlePlayerFailure ? 1 : 0;
                    // stateNumber 0 to move on, stateNumber 1 to restart the current level
                    __instance.callFunction(new GenericButtonActioner(stateNumber, 0));
                }
            }
            /* todo see if this is needed
            [HarmonyPostfix]
            [HarmonyPatch(typeof(PigMenu), nameof(PigMenu.setSubMenu))]
            static void PostfixPigMenuSetSubMenu(ref PigMenu __instance, MenuBase ___currentSubMenu)
            {
                if (___currentSubMenu is MenuDisguises)
                {
                    StartTimerIfNeeded("Disguises Menu entered");
                }
            }
            */

            [HarmonyPostfix]
            [HarmonyPatch(typeof(MenuBase), nameof(MenuBase.setCurrentButton))]
            // This starts the timer if we're on a pause menu and then we select a different button
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

            [HarmonyPostfix]
            [HarmonyPatch(typeof(MidGame), nameof(MidGame.setCurrentMenu))]
            static void PatchSetCurrentMenu(ref MidGame __instance, MenuBase ___pauseMenu, MenuBase ___startGameMenu, MenuBase ___pauseMenuInGame, ref MenuBase ___currentMenu)
            {
                // this might not be right place for this but we should disable the leaderboards if we're speedrunning
                if (_speedrunModeEnabled.Value)
                {
                    PigEatBallGame.staticThis.gameSettings.enableLeaderboards = false;
                }
                if (___currentMenu == ___startGameMenu)
                {
                    // Initial game menu entered
                    // todo I can display info on my mod tools, saying that it's properly loaded here and then remove it
                    // when the menu changes
                    ResetTimer("Start game menu entered");
                }
                if(___currentMenu != null
                    // this breaks our existing logic that skips these menus anyway
                    && ___currentMenu is not MenuClamTalkStart
                    // we can use this to warp
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

            if (_showTimer.Value)
            {
                _timerText.SetText(elapsedTime);
                _timerText.Draw();
            }
            if (_showDebugText.Value)
            {
                _debugText.Draw();
            }
            if (_showStartUpText)
            {
                _startupText.Draw();
            }
        }
    }
}
