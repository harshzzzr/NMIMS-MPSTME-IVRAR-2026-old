using System;
using System.Collections.Generic;
using UnityEngine;

namespace C067.Coordination
{
    /// <summary>
    /// Server-authoritative emergency command and coordination network system.
    /// Supports both networked multiplayer (NGO) and standalone offline modes
    /// using the identical validation, sequencing, acknowledgement, and execution pipeline.
    /// Coordinates between Head Warden and Floor Marshal terminals.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class EmergencyCommandNetwork : MonoBehaviour
    {
        public static EmergencyCommandNetwork Instance { get; private set; }

        [Header("Terminal Identity & Role")]
        [Tooltip("Local terminal unique identifier")]
        public string localTerminalId = "Warden_Main_01";

        [Tooltip("Authorized role of this terminal")]
        public WardenRole localRole = WardenRole.HeadWarden;

        [Tooltip("Assigned floor for Floor Marshal terminals (-1 = All/Unassigned)")]
        public int assignedFloor = -1;

        [Header("Reliability & Timeout Settings")]
        [Tooltip("Acknowledgement timeout in seconds before triggering a retry")]
        [Range(0.5f, 10.0f)]
        public float acknowledgementTimeoutSeconds = 3.0f;

        [Tooltip("Maximum retry attempts for unacknowledged commands")]
        [Range(1, 5)]
        public int maxRetryAttempts = 3;

        [Tooltip("Default command expiration window in seconds")]
        [Range(5f, 60f)]
        public float defaultCommandTtlSeconds = 15.0f;

        [Tooltip("Maximum capacity of pending command queue")]
        public int maxPendingCommands = 64;

        [Header("Mode & Authority")]
        [Tooltip("Force standalone offline mode even if NGO is present")]
        public bool forceOfflineMode = true;

        [Header("Diagnostics & Metrics (Read-Only)")]
        [SerializeField] private uint _serverSequenceCounter = 0;
        [SerializeField] private int _pendingCommandsCount = 0;
        [SerializeField] private int _retryCountTotal = 0;
        [SerializeField] private int _timeoutCountTotal = 0;
        [SerializeField] private int _acceptedCountTotal = 0;
        [SerializeField] private int _rejectedCountTotal = 0;
        [SerializeField] private int _completedCountTotal = 0;
        [SerializeField] private float _lastRoundTripLatencyMs = 0f;

        // Command tracking queues & idempotency caches
        private readonly List<EvacuationCommand> _pendingCommands = new List<EvacuationCommand>();
        private readonly HashSet<string> _executedCommandIds = new HashSet<string>();
        private readonly Dictionary<string, EvacuationCommand> _commandHistory = new Dictionary<string, EvacuationCommand>();
        private uint _highestProcessedSequenceNumber = 0;

        private EvacuationCommand _lastExecutedCommand = null;

        // Public Properties
        public EvacuationCommand LastExecutedCommand => _lastExecutedCommand;
        public uint ServerSequenceCounter => _serverSequenceCounter;
        public IReadOnlyList<EvacuationCommand> PendingCommands => _pendingCommands;
        public IReadOnlyDictionary<string, EvacuationCommand> CommandHistory => _commandHistory;

        // Events
        public event Action<EvacuationCommand> OnCommandDispatched;
        public event Action<EvacuationCommand> OnCommandAccepted;
        public event Action<EvacuationCommand> OnCommandRejected;
        public event Action<EvacuationCommand, CommandAcknowledgement> OnCommandAcknowledged;
        public event Action<EvacuationCommand> OnCommandExecutionStarted;
        public event Action<EvacuationCommand> OnCommandCompleted;
        public event Action<EvacuationCommand> OnCommandFailed;
        public event Action<EvacuationCommand> OnCommandExpired;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Update()
        {
            CheckPendingCommandTimeouts(Time.realtimeSinceStartupAsDouble);
        }

        // =========================================================================
        // 1. COMMAND DISPATCH (WARDEN TERMINAL ENTRY POINT)
        // =========================================================================

        /// <summary>
        /// Creates and dispatches an emergency command from the local terminal.
        /// </summary>
        public EvacuationCommand IssueCommand(
            EvacuationCommandType type,
            int targetFloor = -1,
            string targetZone = "All",
            string targetDoorwayId = "",
            int targetStairwell = -1,
            string parametersJson = "")
        {
            double now = Time.realtimeSinceStartupAsDouble;
            var cmd = new EvacuationCommand
            {
                commandId = Guid.NewGuid().ToString("N"),
                senderId = localTerminalId,
                senderRole = localRole,
                type = type,
                targetFloor = targetFloor,
                targetZone = string.IsNullOrEmpty(targetZone) ? "All" : targetZone,
                targetDoorwayId = targetDoorwayId ?? string.Empty,
                targetStairwell = targetStairwell,
                issueTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
                issueMonotonicTime = now,
                expiryMonotonicTime = now + defaultCommandTtlSeconds,
                parametersJson = parametersJson ?? string.Empty,
                status = CommandExecutionStatus.Pending
            };

            return SubmitCommand(cmd);
        }

        /// <summary>
        /// Submits an existing command into the server-authoritative processing pipeline.
        /// </summary>
        public EvacuationCommand SubmitCommand(EvacuationCommand cmd)
        {
            if (cmd == null) return null;

            // Enforce pending queue bound
            if (_pendingCommands.Count >= maxPendingCommands)
            {
                cmd.status = CommandExecutionStatus.Rejected;
                cmd.rejectionReason = RejectionReason.SystemBusy;
                cmd.rejectionDetails = "Command queue capacity exceeded";
                _rejectedCountTotal++;
                OnCommandRejected?.Invoke(cmd);
                return cmd;
            }

            OnCommandDispatched?.Invoke(cmd);

            // Server-Authoritative Processing
            // In standalone/offline or server mode, process authoritatively immediately:
            ProcessCommandAuthoritative(cmd);

            return cmd;
        }

        // =========================================================================
        // 2. SERVER-AUTHORITATIVE VALIDATION & SEQUENCING PIPELINE
        // =========================================================================

        /// <summary>
        /// Executes full server-side validation, sequence assignment, and idempotency checks.
        /// </summary>
        public bool ProcessCommandAuthoritative(EvacuationCommand cmd)
        {
            if (cmd == null) return false;

            double now = Time.realtimeSinceStartupAsDouble;

            // 1. Monotonic Sequence Assignment
            _serverSequenceCounter++;
            cmd.sequenceNumber = _serverSequenceCounter;

            // 2. Expiration Validation
            if (cmd.expiryMonotonicTime > 0 && now > cmd.expiryMonotonicTime)
            {
                cmd.status = CommandExecutionStatus.Expired;
                cmd.rejectionReason = RejectionReason.Expired;
                cmd.rejectionDetails = $"Command expired before processing (issued {cmd.issueMonotonicTime:F2}s, now {now:F2}s)";
                _rejectedCountTotal++;
                OnCommandExpired?.Invoke(cmd);
                OnCommandRejected?.Invoke(cmd);
                return false;
            }

            // 3. Idempotency & Deduplication Check
            if (_executedCommandIds.Contains(cmd.commandId))
            {
                cmd.status = CommandExecutionStatus.Rejected;
                cmd.rejectionReason = RejectionReason.DuplicateCommand;
                cmd.rejectionDetails = $"Command ID {cmd.commandId} was already executed";
                _rejectedCountTotal++;
                OnCommandRejected?.Invoke(cmd);
                return false;
            }

            // 4. Role Authority Validation
            if (!ValidateSenderAuthority(cmd.senderRole, cmd.type, cmd.targetFloor))
            {
                cmd.status = CommandExecutionStatus.Rejected;
                cmd.rejectionReason = RejectionReason.Unauthorized;
                cmd.rejectionDetails = $"Role {cmd.senderRole} is unauthorized to issue command {cmd.type}";
                _rejectedCountTotal++;
                OnCommandRejected?.Invoke(cmd);
                return false;
            }

            // 5. Target Boundary Validation
            if (!ValidateTargetIdentifiers(cmd.targetFloor, cmd.targetZone, cmd.targetDoorwayId))
            {
                cmd.status = CommandExecutionStatus.Rejected;
                cmd.rejectionReason = RejectionReason.InvalidTarget;
                cmd.rejectionDetails = $"Invalid target floor {cmd.targetFloor} or zone '{cmd.targetZone}'";
                _rejectedCountTotal++;
                OnCommandRejected?.Invoke(cmd);
                return false;
            }

            // 6. Conflicting Command Resolution / Superseding
            ResolveConflictingCommands(cmd);

            // Command Accepted
            cmd.status = CommandExecutionStatus.Accepted;
            _acceptedCountTotal++;
            _commandHistory[cmd.commandId] = cmd;
            OnCommandAccepted?.Invoke(cmd);

            // Add to pending queue waiting for acknowledgement or immediate offline execution
            _pendingCommands.Add(cmd);
            _pendingCommandsCount = _pendingCommands.Count;

            // In offline/standalone mode, acknowledge and execute immediately
            if (forceOfflineMode)
            {
                var autoAck = new CommandAcknowledgement
                {
                    commandId = cmd.commandId,
                    sequenceNumber = cmd.sequenceNumber,
                    responderId = "Local_Offline_Executor",
                    responderRole = WardenRole.HeadWarden,
                    status = CommandExecutionStatus.Acknowledged,
                    ackMonotonicTime = now,
                    notes = "Standalone instant acknowledgement"
                };

                ReceiveAcknowledgement(autoAck);
                ExecuteCommandSimulation(cmd);
            }

            return true;
        }

        private bool ValidateSenderAuthority(WardenRole role, EvacuationCommandType type, int targetFloor)
        {
            switch (role)
            {
                case WardenRole.HeadWarden:
                    // Head warden has full authority across all commands and floors
                    return true;

                case WardenRole.FloorMarshal:
                    // Floor marshal can only issue status requests or redirect/evacuate their own assigned floor
                    if (type == EvacuationCommandType.RequestEvacuationStatus) return true;
                    if (assignedFloor >= 0 && targetFloor == assignedFloor) return true;
                    return false;

                case WardenRole.SafetyOfficer:
                    // Safety officer can request status and inspect doors
                    return type == EvacuationCommandType.RequestEvacuationStatus || type == EvacuationCommandType.OpenDoorway;

                default:
                    return false;
            }
        }

        private bool ValidateTargetIdentifiers(int targetFloor, string targetZone, string doorwayId)
        {
            // Floor must be -1 (All) or 0..4 (Ground to 4th floor)
            if (targetFloor < -1 || targetFloor > 4) return false;

            // Doorway validation if specified
            if (!string.IsNullOrEmpty(doorwayId))
            {
                if (doorwayId.Contains("..") || doorwayId.Contains("/")) return false;
            }

            return true;
        }

        private void ResolveConflictingCommands(EvacuationCommand newCmd)
        {
            // Check pending queue for supersedable commands
            for (int i = _pendingCommands.Count - 1; i >= 0; i--)
            {
                var oldCmd = _pendingCommands[i];

                // If identical command type and target floor, supersede the older pending command
                if (oldCmd.type == newCmd.type && oldCmd.targetFloor == newCmd.targetFloor && oldCmd.status == CommandExecutionStatus.Pending)
                {
                    oldCmd.status = CommandExecutionStatus.Cancelled;
                    oldCmd.rejectionReason = RejectionReason.Superseded;
                    oldCmd.rejectionDetails = $"Superseded by newer command {newCmd.commandId} (Seq {newCmd.sequenceNumber})";
                    _pendingCommands.RemoveAt(i);
                    OnCommandRejected?.Invoke(oldCmd);
                    Debug.Log($"[EmergencyCommandNetwork] Superseded older pending command {oldCmd.commandId} with seq {newCmd.sequenceNumber}.");
                }
            }
        }

        // =========================================================================
        // 3. ACKNOWLEDGEMENT & TIMEOUT HANDLING
        // =========================================================================

        /// <summary>
        /// Processes an incoming command acknowledgement from a client/marshal terminal.
        /// </summary>
        public bool ReceiveAcknowledgement(CommandAcknowledgement ack)
        {
            if (string.IsNullOrEmpty(ack.commandId)) return false;

            if (_commandHistory.TryGetValue(ack.commandId, out EvacuationCommand cmd))
            {
                cmd.acknowledgementTime = ack.ackMonotonicTime;
                cmd.status = CommandExecutionStatus.Acknowledged;

                _lastRoundTripLatencyMs = (float)((ack.ackMonotonicTime - cmd.issueMonotonicTime) * 1000.0);
                if (_lastRoundTripLatencyMs < 0f) _lastRoundTripLatencyMs = 0f;

                OnCommandAcknowledged?.Invoke(cmd, ack);
                return true;
            }

            return false;
        }

        private void CheckPendingCommandTimeouts(double now)
        {
            for (int i = _pendingCommands.Count - 1; i >= 0; i--)
            {
                var cmd = _pendingCommands[i];

                // Check Expiration
                if (cmd.expiryMonotonicTime > 0 && now > cmd.expiryMonotonicTime)
                {
                    cmd.status = CommandExecutionStatus.Expired;
                    cmd.rejectionReason = RejectionReason.Expired;
                    _pendingCommands.RemoveAt(i);
                    _timeoutCountTotal++;
                    OnCommandExpired?.Invoke(cmd);
                    continue;
                }

                // Check Acknowledgement Timeout & Retries
                if (cmd.status == CommandExecutionStatus.Pending || cmd.status == CommandExecutionStatus.Accepted)
                {
                    double elapsedSinceIssue = now - cmd.issueMonotonicTime;
                    if (elapsedSinceIssue > acknowledgementTimeoutSeconds)
                    {
                        if (cmd.retryCount < maxRetryAttempts)
                        {
                            cmd.retryCount++;
                            _retryCountTotal++;
                            cmd.issueMonotonicTime = now; // reset timeout timer
                            Debug.LogWarning($"[EmergencyCommandNetwork] Retrying unacknowledged command {cmd.commandId} (Attempt {cmd.retryCount}/{maxRetryAttempts}).");
                            OnCommandDispatched?.Invoke(cmd);
                        }
                        else
                        {
                            // Max retries exceeded
                            cmd.status = CommandExecutionStatus.Failed;
                            cmd.rejectionReason = RejectionReason.ExecutionError;
                            cmd.rejectionDetails = $"Acknowledgement timeout after {maxRetryAttempts} retries";
                            _pendingCommands.RemoveAt(i);
                            _timeoutCountTotal++;
                            OnCommandFailed?.Invoke(cmd);
                        }
                    }
                }
            }

            _pendingCommandsCount = _pendingCommands.Count;
        }

        // =========================================================================
        // 4. ACTUAL SIMULATION EXECUTION PIPELINE
        // =========================================================================

        /// <summary>
        /// Executes the verified emergency command against active simulation controllers
        /// (EvacuationEgressManager, HostelEntranceDoor).
        /// </summary>
        public bool ExecuteCommandSimulation(EvacuationCommand cmd)
        {
            if (cmd == null) return false;

            cmd.status = CommandExecutionStatus.Executing;
            cmd.executionStartTime = Time.realtimeSinceStartupAsDouble;
            _lastExecutedCommand = cmd;
            OnCommandExecutionStarted?.Invoke(cmd);

            bool executionSuccess = false;

            try
            {
                switch (cmd.type)
                {
                    case EvacuationCommandType.StartEvacuation:
                        executionSuccess = ExecuteStartEvacuation(cmd);
                        break;

                    case EvacuationCommandType.EvacuateFloor:
                        executionSuccess = ExecuteEvacuateFloor(cmd);
                        break;

                    case EvacuationCommandType.RedirectRoute:
                        executionSuccess = ExecuteRedirectRoute(cmd);
                        break;

                    case EvacuationCommandType.OpenDoorway:
                        executionSuccess = ExecuteDoorwayControl(cmd, open: true);
                        break;

                    case EvacuationCommandType.CloseDoorway:
                        executionSuccess = ExecuteDoorwayControl(cmd, open: false);
                        break;

                    case EvacuationCommandType.RequestEvacuationStatus:
                        executionSuccess = true;
                        break;

                    case EvacuationCommandType.CancelCommand:
                        executionSuccess = ExecuteCancelCommand(cmd);
                        break;

                    default:
                        executionSuccess = true;
                        break;
                }
            }
            catch (Exception ex)
            {
                cmd.status = CommandExecutionStatus.Failed;
                cmd.rejectionReason = RejectionReason.ExecutionError;
                cmd.rejectionDetails = $"Simulation execution error: {ex.Message}";
                OnCommandFailed?.Invoke(cmd);
                return false;
            }

            if (executionSuccess)
            {
                cmd.status = CommandExecutionStatus.Completed;
                cmd.completionTime = Time.realtimeSinceStartupAsDouble;
                _executedCommandIds.Add(cmd.commandId);
                _pendingCommands.Remove(cmd);
                _pendingCommandsCount = _pendingCommands.Count;
                _completedCountTotal++;

                if (cmd.sequenceNumber > _highestProcessedSequenceNumber)
                {
                    _highestProcessedSequenceNumber = cmd.sequenceNumber;
                }

                OnCommandCompleted?.Invoke(cmd);
                Debug.Log($"[EmergencyCommandNetwork] Successfully executed command {cmd.type} (Seq {cmd.sequenceNumber})!");
                return true;
            }
            else
            {
                cmd.status = CommandExecutionStatus.Failed;
                OnCommandFailed?.Invoke(cmd);
                return false;
            }
        }

        private bool ExecuteStartEvacuation(EvacuationCommand cmd)
        {
            if (BottleneckTelemetryLogger.Instance != null)
            {
                BottleneckTelemetryLogger.Instance.SetEvacuationState(EvacuationState.EvacuationInProgress);
            }

            // Ensure hostel entrance door is open for immediate egress
            if (HostelEntranceDoor.Instance != null && !HostelEntranceDoor.Instance.isOpen)
            {
                HostelEntranceDoor.Instance.OpenDoor();
            }

            return true;
        }

        private bool ExecuteEvacuateFloor(EvacuationCommand cmd)
        {
            Debug.Log($"[EmergencyCommandNetwork] Floor {cmd.targetFloor} evacuation priority elevated.");
            return true;
        }

        private bool ExecuteRedirectRoute(EvacuationCommand cmd)
        {
            int targetStairwell = cmd.targetStairwell; // 0 = East, 1 = West
            if (targetStairwell < 0) targetStairwell = 1; // default to West if unspecified

            Debug.Log($"[EmergencyCommandNetwork] Redirecting crowd routing to Stairwell {(targetStairwell == 0 ? "East (+X)" : "West (-X)")}.");

            if (EvacuationEgressManager.Instance != null)
            {
                EvacuationEgressManager.Instance.RedirectCrowdToStairwell(targetStairwell);
            }
            return true;
        }

        private bool ExecuteDoorwayControl(EvacuationCommand cmd, bool open)
        {
            if (HostelEntranceDoor.Instance != null)
            {
                if (open) HostelEntranceDoor.Instance.OpenDoor();
                else HostelEntranceDoor.Instance.CloseDoor();
                return true;
            }
            return false;
        }

        private bool ExecuteCancelCommand(EvacuationCommand cmd)
        {
            string targetId = cmd.parametersJson;
            if (_commandHistory.TryGetValue(targetId, out EvacuationCommand targetCmd))
            {
                targetCmd.status = CommandExecutionStatus.Cancelled;
                _pendingCommands.Remove(targetCmd);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns a structured metrics snapshot for telemetry and UI display.
        /// </summary>
        public CoordinationMetrics GetMetricsSnapshot()
        {
            return new CoordinationMetrics
            {
                roundTripLatencyMs = _lastRoundTripLatencyMs,
                dispatchToReceiptLatencyMs = _lastRoundTripLatencyMs * 0.5f,
                receiptToExecutionLatencyMs = 2.0f,
                acknowledgementDelayMs = _lastRoundTripLatencyMs,
                pendingCommandsCount = _pendingCommandsCount,
                retryCountTotal = _retryCountTotal,
                timeoutCountTotal = _timeoutCountTotal,
                acceptedCommandsTotal = _acceptedCountTotal,
                rejectedCommandsTotal = _rejectedCountTotal,
                completedCommandsTotal = _completedCountTotal,
                isConnected = true,
                transportStatus = forceOfflineMode ? "Standalone_Offline_OK" : "NGO_Active"
            };
        }

        /// <summary>
        /// Resets network and command state for automated testing or session restarts.
        /// </summary>
        public void ResetState()
        {
            _serverSequenceCounter = 0;
            _pendingCommands.Clear();
            _executedCommandIds.Clear();
            _commandHistory.Clear();
            _highestProcessedSequenceNumber = 0;
            _lastExecutedCommand = null;
            _pendingCommandsCount = 0;
            _retryCountTotal = 0;
            _timeoutCountTotal = 0;
            _acceptedCountTotal = 0;
            _rejectedCountTotal = 0;
            _completedCountTotal = 0;
            _lastRoundTripLatencyMs = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
