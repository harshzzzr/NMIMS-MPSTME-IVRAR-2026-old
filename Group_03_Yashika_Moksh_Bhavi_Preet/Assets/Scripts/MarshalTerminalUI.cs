using System;
using UnityEngine;

namespace C067.Coordination
{
    /// <summary>
    /// Interactive interface for Floor Marshal terminals (e.g., Floor 1 to Floor 4 Marshals).
    /// Receives commands targeted to the marshal's assigned floor, displays command details,
    /// allows manual or automated receipt acknowledgement, and reports local execution status.
    /// </summary>
    public class MarshalTerminalUI : MonoBehaviour
    {
        [Header("Terminal Identity")]
        public string marshalTerminalId = "Marshal_Floor_02";
        public int assignedFloor = 2; // Floor this marshal is assigned to supervise
        public string assignedZone = "All";

        [Header("Display Settings")]
        public bool showInterface = false;
        public KeyCode toggleKey = KeyCode.F4;
        public Rect windowRect = new Rect(460, 45, 380, 480);
        public bool autoAcknowledge = false; // Set true for simulated automated client acknowledgement

        [Header("Last Received Command (Read-Only)")]
        [SerializeField] private string _currentCommandId = string.Empty;
        [SerializeField] private uint _currentSequenceNumber = 0;
        [SerializeField] private EvacuationCommandType _currentCommandType;
        [SerializeField] private CommandExecutionStatus _currentStatus = CommandExecutionStatus.Pending;
        [SerializeField] private bool _isAcknowledged = false;
        [SerializeField] private string _rejectionOrErrorInfo = string.Empty;

        private EvacuationCommand _activeReceivedCommand = null;
        private Vector2 _scrollPos;

        private void Start()
        {
            if (EmergencyCommandNetwork.Instance != null)
            {
                EmergencyCommandNetwork.Instance.OnCommandAccepted += HandleCommandReceived;
            }
        }

        private void Update()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (toggleKey == KeyCode.F4 && kb.f4Key.wasPressedThisFrame) showInterface = !showInterface;
                else if (toggleKey == KeyCode.F2 && kb.f2Key.wasPressedThisFrame) showInterface = !showInterface;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(toggleKey))
            {
                showInterface = !showInterface;
            }
#endif
        }

        private void HandleCommandReceived(EvacuationCommand cmd)
        {
            if (cmd == null) return;

            // Filter: must be addressed to all floors (-1) or this marshal's assigned floor
            if (cmd.targetFloor != -1 && cmd.targetFloor != assignedFloor)
            {
                return; // Not addressed to this floor
            }

            _activeReceivedCommand = cmd;
            _currentCommandId = cmd.commandId;
            _currentSequenceNumber = cmd.sequenceNumber;
            _currentCommandType = cmd.type;
            _currentStatus = cmd.status;
            _isAcknowledged = (cmd.status == CommandExecutionStatus.Acknowledged || cmd.status == CommandExecutionStatus.Executing || cmd.status == CommandExecutionStatus.Completed);
            _rejectionOrErrorInfo = cmd.rejectionDetails;

            if (autoAcknowledge && !_isAcknowledged)
            {
                AcknowledgeReceipt();
            }
        }

        public void AcknowledgeReceipt()
        {
            if (_activeReceivedCommand == null) return;

            var ack = new CommandAcknowledgement
            {
                commandId = _activeReceivedCommand.commandId,
                sequenceNumber = _activeReceivedCommand.sequenceNumber,
                responderId = marshalTerminalId,
                responderRole = WardenRole.FloorMarshal,
                status = CommandExecutionStatus.Acknowledged,
                ackMonotonicTime = Time.realtimeSinceStartupAsDouble,
                notes = $"Acknowledged by {marshalTerminalId} on Floor {assignedFloor}"
            };

            if (EmergencyCommandNetwork.Instance != null)
            {
                EmergencyCommandNetwork.Instance.ReceiveAcknowledgement(ack);
                _isAcknowledged = true;
                _currentStatus = CommandExecutionStatus.Acknowledged;
                Debug.Log($"[{marshalTerminalId}] Acknowledged command {_activeReceivedCommand.type} (Seq: #{_activeReceivedCommand.sequenceNumber}).");
            }
        }

        private void OnGUI()
        {
            // Subtle, compact toggle button in upper left corner
            if (GUI.Button(new Rect(175, 10, 165, 26), showInterface ? "Hide Marshal [F4]" : "📋 Marshal HUD [F4]"))
            {
                showInterface = !showInterface;
            }

            if (!showInterface) return;

            GUI.backgroundColor = new Color(0.18f, 0.22f, 0.20f, 0.95f);
            windowRect = GUI.Window(9002, windowRect, DrawMarshalWindow, $"📋 MARSHAL TERMINAL: FLOOR {assignedFloor} (C067)");
        }

        private void DrawMarshalWindow(int windowId)
        {
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);

            // 1. Terminal Identity Box
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"<b>ID:</b> {marshalTerminalId} | <b>Assigned Floor:</b> Floor {assignedFloor}", RichTextStyle());
            GUILayout.Label($"Auto-Ack: {(autoAcknowledge ? "ENABLED (Instant)" : "DISABLED (Manual)")}");
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // 2. Active Command Display
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("<b>Active Incoming Command:</b>", RichTextStyle());

            if (_activeReceivedCommand != null)
            {
                GUILayout.Label($"Command: <b>{_currentCommandType}</b>", RichTextStyle());
                GUILayout.Label($"Sequence Number: <b>#{_currentSequenceNumber}</b>", RichTextStyle());
                GUILayout.Label($"Command ID: {_currentCommandId}");
                GUILayout.Label($"Target Floor: {(_activeReceivedCommand.targetFloor == -1 ? "All Floors" : $"Floor {_activeReceivedCommand.targetFloor}")}");
                GUILayout.Label($"Target Zone: {_activeReceivedCommand.targetZone}");

                if (!string.IsNullOrEmpty(_activeReceivedCommand.targetDoorwayId))
                {
                    GUILayout.Label($"Target Door: {_activeReceivedCommand.targetDoorwayId}");
                }

                GUILayout.Space(6);
                GUILayout.Label($"Current Status: <b>{_activeReceivedCommand.status}</b>", RichTextStyle());

                if (!string.IsNullOrEmpty(_rejectionOrErrorInfo))
                {
                    GUI.contentColor = Color.yellow;
                    GUILayout.Label($"Note/Error: {_rejectionOrErrorInfo}");
                    GUI.contentColor = Color.white;
                }

                GUILayout.Space(10);

                // Manual Acknowledge Button
                if (!_isAcknowledged)
                {
                    GUI.backgroundColor = new Color(0.1f, 0.7f, 0.3f, 1f);
                    if (GUILayout.Button("✅ ACKNOWLEDGE RECEIPT", GUILayout.Height(36)))
                    {
                        AcknowledgeReceipt();
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.contentColor = Color.green;
                    GUILayout.Label("✔ Receipt Acknowledged to Head Warden.");
                    GUI.contentColor = Color.white;
                }
            }
            else
            {
                GUILayout.Label("No active commands addressed to this floor.\nStanding by for Head Warden directives.");
            }
            GUILayout.EndVertical();

            GUILayout.Space(8);

            // 3. Quick Marshal Status Request Button
            if (GUILayout.Button("📡 Request Floor Evacuation Status", GUILayout.Height(30)))
            {
                if (EmergencyCommandNetwork.Instance != null)
                {
                    EmergencyCommandNetwork.Instance.IssueCommand(
                        EvacuationCommandType.RequestEvacuationStatus,
                        assignedFloor,
                        assignedZone
                    );
                }
            }

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        private static GUIStyle RichTextStyle()
        {
            var s = new GUIStyle(GUI.skin.label);
            s.richText = true;
            return s;
        }

        private void OnDestroy()
        {
            if (EmergencyCommandNetwork.Instance != null)
            {
                EmergencyCommandNetwork.Instance.OnCommandAccepted -= HandleCommandReceived;
            }
        }
    }
}
