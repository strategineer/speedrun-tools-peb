using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace com.strategineer.PEBSpeedrunTools
{
    public partial class Plugin
    {
        class AutoSplitter
        {
            private Socket _socket;
            private bool _hasStarted = false;
            private bool _hasFinished = false;
            private HashSet<string> _splitIds = new HashSet<string>();
            public enum Command
            {
                START,
                SPLIT,
                PAUSE,
                RESUME,
                RESET,
                INIT_GAMETIME,
                PAUSE_GAMETIME,
                UNPAUSE_GAMETIME
            }

            public bool IsStarted()
            {
                return _hasStarted;
            }

            public bool IsFinished()
            {
                return _hasFinished;
            }

            public void StartOrResume()
            {
                if (_hasFinished) return;
                if(!_hasStarted)
                {
                    _hasStarted = true;
                    Reset();
                    SendCommand(Command.START);
                    SendCommand(Command.INIT_GAMETIME);
                } else
                {
                    UnpauseGametime();
                }
            }
            public void FinishRun()
            {
                if (_hasFinished) return;
                PauseGametime();
                SendCommand(Command.PAUSE);
                _hasFinished = true;

            }
            public void UnpauseGametime()
            {
                if (_hasFinished) return;
                if (!_hasStarted) { return; }
                SendCommand(Command.UNPAUSE_GAMETIME);
            }

            public void SplitIfNeeded(string id)
            {
                if (_hasFinished) return;
                if (!_hasStarted) { return; }
                if (!_splitIds.Contains(id))
                {
                    _splitIds.Add(id);
                    SendCommand(Command.SPLIT);
                }
            }

            public void PauseGametime()
            {
                if (_hasFinished) return;
                if (!_hasStarted) { return; }
                SendCommand(Command.PAUSE_GAMETIME);
            }

            public void Reset()
            {
                if (_hasFinished) return;
                SendCommand(Command.RESET);
                _splitIds.Clear();
            }

            public AutoSplitter(string ip, int port)
            {
                try
                {
                    _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    IPAddress ipAdd = IPAddress.Parse(ip);
                    IPEndPoint remoteEP = new IPEndPoint(ipAdd, port);
                    _socket.Connect(remoteEP);
                }
                catch
                {
                    Log($"Can't create autosplitter with ip {ip} and port {port}");
                    throw;
                }
            }
            ~AutoSplitter()
            {
                if (_socket != null)
                {
                    _socket.Close();
                    _socket = null;
                }
            }

            private void SendCommand(Command command)
            {
                if (
                    _socket == null
                    || !_socket.Connected)
                {
                    Log("Could not send reset command to Live Split\nnot connected!");
                    return;
                }
                Debug(DebugTarget.LAST_AUTOSPLITTER_COMMAND, $"{Enum.GetName(typeof(Command), command)}");
                string msg;
                switch (command)
                {
                    case Command.START:
                        msg = "starttimer";
                        break;
                    case Command.SPLIT:
                        msg = "split";
                        break;
                    case Command.PAUSE:
                        msg = "pause";
                        break;
                    case Command.RESUME:
                        msg = "resume";
                        break;
                    case Command.RESET:
                        msg = "reset";
                        break;
                    case Command.INIT_GAMETIME:
                        msg = "initgametime";
                        break;
                    case Command.PAUSE_GAMETIME:
                        msg = "pausegametime";
                        break;
                    case Command.UNPAUSE_GAMETIME:
                        msg = "unpausegametime";
                        break;
                    default:
                        throw new Exception($"Missing command implementation {command}");
                }
                byte[] byData = Encoding.ASCII.GetBytes($"{msg}\r\n");
                _socket.Send(byData);
            }
        }
    }
}
