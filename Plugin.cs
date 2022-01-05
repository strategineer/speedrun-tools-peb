using BepInEx;
using System.Diagnostics;
using System;
using UnityEngine;
using BepInEx.Configuration;
using MirrorInternalLogs;

namespace com.strategineer.PEBSpeedrunTools
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("pigEatBallGame.exe")]
    public class Plugin : BaseUnityPlugin
    {
        Stopwatch stopWatch = new Stopwatch();
        private static ConfigEntry<TextAnchor> _position;
        private static ConfigEntry<bool> _showTimer;

        const int MAX_STRING_SIZE = 499;

        private static readonly GUIStyle _textStyle = new GUIStyle();
        private static readonly GUIStyle _textShadowStyle = new GUIStyle();
        private static Rect _timerRect;
        private static Rect _timerShadowRect;
        private const int ScreenOffset = 10;
        private static bool isTimerOn = false;
        private static string _frameOutputText;

        private void Awake()
        {
            InternalUnityLogger.OnUnityInternalLog += HandleUnityLog;
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");

            _position = Config.Bind("Interface",
                "Screen position",
                TextAnchor.LowerRight,
                "Which corner of the screen to display the statistics in.");

            _showTimer = Config.Bind("Interface",
                "Show Timer",
                true,
                "Should show the speedrun timer?");
        }

        private void StartTimer()
        {
            isTimerOn = true;
            stopWatch.Start();
        }
        private void StopTimer()
        {
            stopWatch.Stop();
            isTimerOn = false;
        }

        void HandleUnityLog(object sender, UnityLogEventArgs e)
        {
            // TODO(strategineer): can we ignore dialog? that doesn't appear in the logs...
            if (e.Message.Contains("StartTestLevelFromFile, allocate newLevelObj:")
                || e.Message.Contains("LeaderboardFindResult"))
            {
                StopTimer();
            } else if (e.Message.Contains("LevelObj::MapLevelUnlockUpdate end")
                || e.Message.Contains("Finished BeginMainGame")
                || e.Message.Contains("BeginFirstLevelFile")
                || e.Message.Contains("Level.Reset finished"))
            {
                StartTimer();
            }
        }

        private static void updateLooks()
        {

            _textStyle.normal.textColor = isTimerOn ? Color.yellow : Color.green;
            _textShadowStyle.normal.textColor = Color.black;

            int w = Screen.width, h = Screen.height;
            _timerRect = new Rect(ScreenOffset, ScreenOffset, w - ScreenOffset * 2, h - ScreenOffset * 2);
            _timerShadowRect = _timerRect;
            _timerShadowRect.x += 3;
            _timerShadowRect.y += 3;

            _textStyle.alignment = _textShadowStyle.alignment = _position.Value;
            _textStyle.fontSize = _textShadowStyle.fontSize = h / 40;
        }

        private void Update()
        {
            updateLooks();
        }

        private void displayTime()
        {
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime;
            if (ts.Hours > 0)
            {
                elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            } else
            {
                elapsedTime = String.Format("{0:00}:{1:00}.{2:00}",
                ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            }
            
            _frameOutputText = elapsedTime;
            GUI.Label(_timerShadowRect, _frameOutputText, _textShadowStyle);
            GUI.Label(_timerRect, _frameOutputText, _textStyle);
        }

        private void OnGUI()
        {
            displayTime();
        }
    }
}
