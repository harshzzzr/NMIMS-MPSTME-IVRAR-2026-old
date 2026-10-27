using System;
using UnityEngine;

namespace C067.Coordination
{
    /// <summary>
    /// Interactive desktop and VR-compatible interface for the Head Warden.
    /// Allows issuing emergency evacuation commands, monitoring server acknowledgements,
    /// tracking monotonic sequence numbers, and inspecting real-time coordination metrics.
    /// </summary>
    public class WardenCommandTerminalUI : MonoBehaviour
    {
        [Header("Visibility & Position")]
        public bool showInterface = false;
        public KeyCode toggleKey = KeyCode.F3;
        public Rect windowRect = new Rect(20, 45, 420, 560);

        [Header("Command Parameters Selection")]
        private EvacuationCommandType _selectedCommand = EvacuationCommandType.StartEvacuation;
        private int _selectedFloor = -1; // -1 = All floors
        private int _selectedStairwell = 0; // 0 = East, 1 = West
        private string _selectedZone = "All";
        private string _selectedDoorway = "MainEntrance_DoubleDoor";

        private Vector2 _scrollPos;
        private string _statusFeedback = "Ready";

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (toggleKey == KeyCode.F3 && kb.f3Key.wasPressedThisFrame) showInterface = !showInterface;
                else if (toggleKey == KeyCode.F1 && kb.f1Key.wasPressedThisFrame) showInterface = !showInterface;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(toggleKey))
            {
                showInterface = !showInterface;
            }
#endif
        }

        private void OnGUI()
        {
            // Subtle, compact toggle button in upper left corner
            if (GUI.Button(new Rect(10, 10, 155, 26), showInterface ? "Hide Warden [F3]" : "🚨 Warden HUD [F3]"))
            {
                showInterface = !showInterface;
            }

            if (!showInterface) return;

            GUI.backgroundColor = new Color(0.12f, 0.16f, 0.24f, 0.95f);
            windowRect = GUI.Window(9001, windowRect, DrawWardenWindow, "🚨 HEAD WARDEN COMMAND TERMINAL (C067)");
        }

        private void DrawWardenWindow(int windowId)
        {
            var net = EmergencyCommandNetwork.Instance;
            if (net == null)
            {
                GUILayout.Label("Waiting for EmergencyCommandNetwork...", EditorStylesBold());
                GUI.DragWindow();
                return;
            }

            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            // 1. Terminal Role & Network Status Header
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"Terminal: {net.localTerminalId} | Role: {net.localRole}", EditorStylesBold());
            var metrics = net.GetMetricsSnapshot();
            GUILayout.Label($"Transport: {metrics.transportStatus} | RTT Latency: {metrics.roundTripLatencyMs:F1} ms");
            GUILayout.Label($"Server Sequence Counter: {net.ServerSequenceCounter}");
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // 2. Command Type Selection
            GUILayout.Label("<b>1. Select Emergency Command:</b>", RichTextStyle());
            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_selectedCommand == EvacuationCommandType.StartEvacuation, "Start Evac", "Button"))
                _selectedCommand = EvacuationCommandType.StartEvacuation;
            if (GUILayout.Toggle(_selectedCommand == EvacuationCommandType.EvacuateFloor, "Floor Evac", "Button"))
                _selectedCommand = EvacuationCommandType.EvacuateFloor;
            if (GUILayout.Toggle(_selectedCommand == EvacuationCommandType.RedirectRoute, "Redirect", "Button"))
                _selectedCommand = EvacuationCommandType.RedirectRoute;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Toggle(_selectedCommand == EvacuationCommandType.OpenDoorway, "Open Door", "Button"))
                _selectedCommand = EvacuationCommandType.OpenDoorway;
            if (GUILayout.Toggle(_selectedCommand == EvacuationCommandType.CloseDoorway, "Close Door", "Button"))
                _selectedCommand = EvacuationCommandType.CloseDoorway;
            if (GUILayout.Toggle(_selectedCommand == EvacuationCommandType.RequestEvacuationStatus, "Status Req", "Button"))
                _selectedCommand = EvacuationCommandType.RequestEvacuationStatus;
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            // 3. Target Parameters
            GUILayout.Label("<b>2. Target Parameters:</b>", RichTextStyle());

            // Target Floor Selection
            GUILayout.BeginHorizontal();
            GUILayout.Label("Floor:", GUILayout.Width(50));
            if (GUILayout.Toggle(_selectedFloor == -1, "All", "Button")) _selectedFloor = -1;
            if (GUILayout.Toggle(_selectedFloor == 0, "G", "Button")) _selectedFloor = 0;
            if (GUILayout.Toggle(_selectedFloor == 1, "1", "Button")) _selectedFloor = 1;
            if (GUILayout.Toggle(_selectedFloor == 2, "2", "Button")) _selectedFloor = 2;
            if (GUILayout.Toggle(_selectedFloor == 3, "3", "Button")) _selectedFloor = 3;
            if (GUILayout.Toggle(_selectedFloor == 4, "4", "Button")) _selectedFloor = 4;
            GUILayout.EndHorizontal();

            // Target Stairwell Selection (for RedirectRoute)
            if (_selectedCommand == EvacuationCommandType.RedirectRoute)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Stairwell:", GUILayout.Width(65));
                if (GUILayout.Toggle(_selectedStairwell == 0, "East (+X)", "Button")) _selectedStairwell = 0;
                if (GUILayout.Toggle(_selectedStairwell == 1, "West (-X)", "Button")) _selectedStairwell = 1;
                GUILayout.EndHorizontal();
            }

            // Target Doorway (for OpenDoorway/CloseDoorway)
            if (_selectedCommand == EvacuationCommandType.OpenDoorway || _selectedCommand == EvacuationCommandType.CloseDoorway)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Doorway:", GUILayout.Width(65));
                _selectedDoorway = GUILayout.TextField(_selectedDoorway);
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            // 4. Issue Command Button (Gold/Amber)
            GUI.backgroundColor = new Color(0.9f, 0.45f, 0.1f, 1f);
            if (GUILayout.Button($"🚨 ISSUE COMMAND: {_selectedCommand.ToString().ToUpper()}", GUILayout.Height(38)))
            {
                var cmd = net.IssueCommand(_selectedCommand, _selectedFloor, _selectedZone, _selectedDoorway, _selectedStairwell);
                if (cmd != null)
                {
                    _statusFeedback = $"Dispatched: {cmd.type} (Seq: {cmd.sequenceNumber}, Status: {cmd.status})";
                }
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(8);

            // 5. Active Command & Execution Status Monitor
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Command Status Monitor:</b>", RichTextStyle());
            var lastCmd = net.LastExecutedCommand;
            if (lastCmd != null)
            {
                GUILayout.Label($"Active ID: {lastCmd.commandId.Substring(0, 8)}... | Seq: #{lastCmd.sequenceNumber}");
                GUILayout.Label($"Type: {lastCmd.type} | Target Floor: {(lastCmd.targetFloor == -1 ? "All" : lastCmd.targetFloor.ToString())}");
                GUILayout.Label($"Status: <b>{lastCmd.status}</b>", RichTextStyle());

                if (lastCmd.rejectionReason != RejectionReason.None)
                {
                    GUI.contentColor = Color.red;
                    GUILayout.Label($"Rejection Reason: {lastCmd.rejectionReason} - {lastCmd.rejectionDetails}");
                    GUI.contentColor = Color.white;
                }

                if (lastCmd.acknowledgementTime > 0)
                {
                    float ackDelayMs = (float)((lastCmd.acknowledgementTime - lastCmd.issueMonotonicTime) * 1000.0);
                    GUILayout.Label($"Ack Timing: {ackDelayMs:F1} ms after dispatch");
                }
            }
            else
            {
                GUILayout.Label(_statusFeedback);
            }
            GUILayout.EndVertical();

            GUILayout.Space(6);

            // 6. Coordination Metrics Summary
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Coordination Metrics:</b>", RichTextStyle());
            GUILayout.Label($"Accepted: {metrics.acceptedCommandsTotal} | Rejected: {metrics.rejectedCommandsTotal} | Completed: {metrics.completedCommandsTotal}");
            GUILayout.Label($"Pending Queue: {metrics.pendingCommandsCount} | Retries: {metrics.retryCountTotal} | Timeouts: {metrics.timeoutCountTotal}");
            GUILayout.EndVertical();

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private static GUIStyle EditorStylesBold()
        {
            var s = new GUIStyle(GUI.skin.label);
            s.fontStyle = FontStyle.Bold;
            return s;
        }

        private static GUIStyle RichTextStyle()
        {
            var s = new GUIStyle(GUI.skin.label);
            s.richText = true;
            return s;
        }
    }
}
