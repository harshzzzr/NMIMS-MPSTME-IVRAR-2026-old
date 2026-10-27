using System;
using UnityEngine;

namespace C067.Coordination
{
    /// <summary>
    /// Evacuation operational states for the 5-story hostel simulation.
    /// </summary>
    public enum EvacuationState
    {
        Normal = 0,
        EvacuationOrdered = 1,
        EvacuationInProgress = 2,
        FloorCleared = 3,
        EvacuationComplete = 4,
        Paused = 5
    }

    /// <summary>
    /// Current runtime status of the crowd and physics simulation.
    /// </summary>
    public enum SimulationStatus
    {
        Idle = 0,
        Running = 1,
        Paused = 2,
        Terminated = 3,
        Resetting = 4
    }

    /// <summary>
    /// Authorized roles in the hostel emergency management structure.
    /// </summary>
    public enum WardenRole
    {
        Unassigned = 0,
        HeadWarden = 1,
        FloorMarshal = 2,
        SafetyOfficer = 3
    }

    /// <summary>
    /// Emergency command verbs supported across warden terminals and crowd navigation.
    /// </summary>
    public enum EvacuationCommandType
    {
        StartEvacuation = 0,
        EvacuateFloor = 1,
        EvacuateZone = 2,
        RedirectRoute = 3,
        OpenDoorway = 4,
        CloseDoorway = 5,
        RequestEvacuationStatus = 6,
        CancelCommand = 7,
        SupersedeCommand = 8
    }

    /// <summary>
    /// Discrete states in the lifecycle of an emergency network command.
    /// </summary>
    public enum CommandExecutionStatus
    {
        Pending = 0,
        Accepted = 1,
        Rejected = 2,
        Acknowledged = 3,
        Executing = 4,
        Completed = 5,
        Failed = 6,
        Expired = 7,
        Cancelled = 8
    }

    /// <summary>
    /// Explicit failure and rejection reasons for auditability and diagnostic feedback.
    /// </summary>
    public enum RejectionReason
    {
        None = 0,
        Unauthorized = 1,
        InvalidTarget = 2,
        Expired = 3,
        DuplicateCommand = 4,
        Superseded = 5,
        InvalidParameters = 6,
        SystemBusy = 7,
        DoorNotSupported = 8,
        ExecutionError = 9
    }

    /// <summary>
    /// Fully qualified emergency command payload exchanged between wardens and marshals.
    /// </summary>
    [System.Serializable]
    public class EvacuationCommand
    {
        public string commandId;            // Globally unique identifier (GUID)
        public uint sequenceNumber;         // Server-assigned monotonic sequence counter
        public string senderId;             // Terminal / client identifier of issuer
        public WardenRole senderRole;       // Authorized role of sender
        public EvacuationCommandType type;  // Command verb
        public int targetFloor;             // Target floor (-1 for all floors, 0=Ground, 1..4=Upper floors)
        public string targetZone;           // Target zone or wing ("EastWing", "WestWing", "CentralCorridor", "All")
        public string targetDoorwayId;      // Target doorway if applicable ("MainEntrance_DoubleDoor", etc.)
        public int targetStairwell;         // 0 = East Stairwell, 1 = West Stairwell, -1 = Unspecified
        public double issueTimeUtc;         // UTC timestamp (Unix seconds)
        public double issueMonotonicTime;   // Monotonic local elapsed time (seconds)
        public double expiryMonotonicTime;  // Expiration deadline in monotonic seconds
        public string parametersJson;       // Optional JSON or parameter string
        public CommandExecutionStatus status;
        public RejectionReason rejectionReason;
        public string rejectionDetails;
        public double acknowledgementTime;  // Time acknowledged by receiving terminal
        public double executionStartTime;   // Time simulation execution started
        public double completionTime;       // Time simulation execution completed
        public int retryCount;              // Current retry iteration count

        public EvacuationCommand()
        {
            commandId = Guid.NewGuid().ToString("N");
            sequenceNumber = 0;
            senderId = "LocalWarden";
            senderRole = WardenRole.HeadWarden;
            type = EvacuationCommandType.StartEvacuation;
            targetFloor = -1;
            targetZone = "All";
            targetDoorwayId = string.Empty;
            targetStairwell = -1;
            issueTimeUtc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            issueMonotonicTime = 0;
            expiryMonotonicTime = 0;
            parametersJson = string.Empty;
            status = CommandExecutionStatus.Pending;
            rejectionReason = RejectionReason.None;
            rejectionDetails = string.Empty;
            acknowledgementTime = -1;
            executionStartTime = -1;
            completionTime = -1;
            retryCount = 0;
        }

        public EvacuationCommand Clone()
        {
            return (EvacuationCommand)MemberwiseClone();
        }
    }

    /// <summary>
    /// Explicit acknowledgement returned by receiver (e.g. floor marshal terminal).
    /// </summary>
    [System.Serializable]
    public struct CommandAcknowledgement
    {
        public string commandId;
        public uint sequenceNumber;
        public string responderId;
        public WardenRole responderRole;
        public CommandExecutionStatus status;
        public double ackMonotonicTime;
        public string notes;
    }

    /// <summary>
    /// Real-time networking and coordination metrics snapshot.
    /// </summary>
    [System.Serializable]
    public struct CoordinationMetrics
    {
        public float roundTripLatencyMs;
        public float dispatchToReceiptLatencyMs;
        public float receiptToExecutionLatencyMs;
        public float acknowledgementDelayMs;
        public int pendingCommandsCount;
        public int retryCountTotal;
        public int timeoutCountTotal;
        public int acceptedCommandsTotal;
        public int rejectedCommandsTotal;
        public int completedCommandsTotal;
        public bool isConnected;
        public string transportStatus;
    }
}
