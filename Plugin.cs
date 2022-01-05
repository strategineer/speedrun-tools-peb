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

        private static readonly GUIStyle _style = new GUIStyle();
        private static Rect _screenRect;
        private const int ScreenOffset = 10;

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

        void HandleUnityLog(object sender, UnityLogEventArgs e)
        {
            if (e.Message.Contains("StartTestLevelFromFile, allocate newLevelObj:"))
            {
                stopWatch.Stop();
            } else if (e.Message.Contains("LevelObj::MapLevelUnlockUpdate end"))
            {
                stopWatch.Start();
            }
        }

        private static void updateLooks()
        {
            _style.normal.textColor = Color.yellow;
            _style.normal.background = Texture2D.blackTexture;

            int w = Screen.width, h = Screen.height;
            _screenRect = new Rect(ScreenOffset, ScreenOffset, w - ScreenOffset * 2, h - ScreenOffset * 2);

            _style.alignment = _position.Value;
            _style.fontSize = h / 40;
        }

        private void Update()
        {
            updateLooks();
        }

        private void displayTime()
        {
            TimeSpan ts = stopWatch.Elapsed;
            // Format and display the TimeSpan value.
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                ts.Hours, ts.Minutes, ts.Seconds,
                ts.Milliseconds / 10);
            _frameOutputText = elapsedTime;
            GUI.Label(_screenRect, _frameOutputText, _style);
        }

        private void OnGUI()
        {
            displayTime();
        }
    }
}
