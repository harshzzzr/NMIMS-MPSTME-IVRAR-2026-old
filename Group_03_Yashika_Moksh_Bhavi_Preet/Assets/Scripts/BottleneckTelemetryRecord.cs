using System;
using System.Globalization;
using System.Text;

namespace C067.Coordination
{
    /// <summary>
    /// Clock source used for telemetry sampling.
    /// MonotonicRealtime: Continuous elapsed clock (realtimeSinceStartupAsDouble) for operational monitoring.
    /// SimulationTime: Scaled simulation clock (Time.timeAsDouble) for reproducible experiments.
    /// </summary>
    public enum TelemetryClockSource
    {
        MonotonicRealtime = 0,
        SimulationTime = 1
    }

    /// <summary>
    /// Discrete telemetry sample capturing evacuation bottleneck, doorway crossing,
    /// jam dynamics, smoke/visibility, and network coordination state at up to 90 Hz.
    /// Immutable record structure safe for thread-safe cross-thread passing.
    /// </summary>
    [System.Serializable]
    public struct BottleneckTelemetrySample
    {
        // 1. Time Sources & Sample Ordering
        public string timestampUtcIso;                  // ISO 8601 UTC timestamp (YYYY-MM-DDTHH:MM:SS.fffZ)
        public double timestampSec;                     // Timestamp in seconds matching the active clock source
        public double monotonicElapsedSec;              // Continuous monotonic elapsed time in seconds (Time.realtimeSinceStartupAsDouble)
        public double simulationTimeSec;                // Scaled simulation time in seconds (Time.timeAsDouble)
        public TelemetryClockSource clockSource;        // Clock source used for sampling interval evaluation
        public long sampleSequenceNumber;               // Monotonically increasing sequence number per trial

        // 2. Experiment & Spatial Identifiers
        public string sessionId;                        // High-level testing session ID
        public string runId;                            // Simulation run ID
        public string trialId;                          // Individual trial identifier
        public string scenarioId;                       // Experimental scenario or configuration ID
        public int floorId;                             // Floor level index (0=Ground, 1=Floor 1, ..., 4=Floor 4)
        public string doorwayId;                        // Unique doorway identifier ("Ground_MainExit", "Floor1_EastStair", etc.)

        // 3. Doorway Physical & Crowd Metrics
        public float doorwayWidthMeters;                // Usable doorway opening width W in meters (-1 if unavailable)
        public int doorwayOccupancy;                    // Number of agents currently inside the doorway measurement region
        public int agentsCrossingInterval;              // Number of valid agents crossing during this sampling interval N
        public int cumulativeCrossings;                 // Authoritative cumulative crossings through this doorway in trial
        public float instantaneousDischargeRate;        // Interval discharge rate J = N / Δt in agents/second
        public float rollingDischargeRate;              // Rolling time-window discharge rate in agents/second
        public float normalizedFlow;                    // Width-normalized specific flow j = N / (W × Δt) in agents/(m·s) (-1 if unavailable)
        public int queueLength;                         // Number of waiting agents currently in upstream queue zone
        public int queueSize                            // Backward-compatible alias for queueLength
        {
            get => queueLength;
            set => queueLength = value;
        }
        public float estimatedQueueLengthMeters;        // Physical upstream queue length in meters (-1 if unavailable)

        // 4. Doorway Jam Dynamics
        public bool isJammed;                           // Jam state flag (true if doorway meets persistent congestion criteria)
        public float jamDurationSec;                    // Elapsed duration of current active jam in seconds (0 if not jammed)
        public int maxQueueSizeDuringJam;               // Peak queue size reached during current active jam

        // 5. Environmental & Evacuation Macro State
        public float timeSinceEvacuationBeganSec;        // Time in seconds since evacuation alarm sounded (-1 if not started)
        public float smokeOrVisibilityLevel;            // Optical smoke concentration ρ ∈ [0, 1] or visibility (-1 if unavailable)
        public EvacuationState evacuationState;          // Relevant scenario macro state (Normal, Evacuating, Congested, etc.)
        public SimulationStatus simulationStatus;        // Runtime status (Running, Paused, Completed)

        // 6. Network Coordination & Command Telemetry
        public string networkRole;                      // Active network node role ("Authority", "Client", "Standalone")
        public float networkLatencyMs;                  // Round-trip latency RTT in milliseconds (-1 if unavailable)
        public bool networkConnected;                   // Active network connection state
        public float packetLossEstimatePercent;         // Estimated packet loss percentage [0, 100] (-1 if unavailable)
        public float commandDispatchLatencySec;         // Latency between command issue and acknowledgment in seconds (-1 if unavailable)
        public double commandDispatchTime;              // Monotonic timestamp when last command was dispatched
        public double commandAckTime;                   // Monotonic timestamp when last command was acknowledged
        public int ackTimeoutCount;                     // Cumulative network acknowledgement timeouts
        public string transportStats;                   // Transport status and socket health string
        public string lastCommandId;                    // Identifier of the active or most recent command (null/empty if none)
        public uint lastCommandSequence;                // Sequence number of the active or most recent command
        public CommandExecutionStatus commandStatus;     // Status of the active or most recent command

        // 7. Diagnostics & Buffer Health
        public int droppedSamplesCumulative;            // Total samples dropped due to bounded buffer backpressure

        /// <summary>
        /// Documented stable CSV header row with explicit column semantics and units.
        /// Fully compatible with Section 2.3 schema requirements and automated test assertions.
        /// </summary>
        public static readonly string CsvHeader =
            "TimestampUtcIso," +
            "TimestampSec," +
            "MonotonicElapsedSec," +
            "SimulationTimeSec," +
            "ClockType," +
            "SampleSequenceNumber," +
            "SessionId," +
            "RunId," +
            "TrialId," +
            "ScenarioId," +
            "FloorId," +
            "DoorwayId," +
            "DoorwayWidthMeters," +
            "DoorwayOccupancy," +
            "AgentsCrossingInterval," +
            "CumulativeCrossings," +
            "InstantaneousDischargeRate_AgentsPerSec," +
            "RollingDischargeRate_AgentsPerSec," +
            "NormalizedFlow_AgentsPerMeterSec," +
            "QueueLength," +
            "EstimatedQueueLength_m," +
            "IsJammed," +
            "JamDurationSec," +
            "MaxQueueSizeDuringJam," +
            "TimeSinceEvacuationBeganSec," +
            "SmokeDensity," +
            "EvacuationState," +
            "SimulationStatus," +
            "NetworkRole," +
            "NetworkLatencyMs," +
            "NetworkConnected," +
            "PacketLossEstimatePercent," +
            "CommandDispatchLatencySec," +
            "CommandDispatchTimeSec," +
            "CommandAckTimeSec," +
            "AckTimeoutCount," +
            "TransportStats," +
            "LastCommandId," +
            "LastCommandSequence," +
            "CommandStatus," +
            "DroppedSamplesCumulative";

        /// <summary>
        /// Formats this sample into a CSV row using invariant culture and RFC 4180 escaping.
        /// Strictly thread-safe: zero Unity engine API calls.
        /// Missing or unavailable measurements are formatted as empty fields ("").
        /// </summary>
        public string ToCsvRow()
        {
            var sb = new StringBuilder(512);

            // 1. Time Sources & Sample Ordering
            sb.Append(EscapeCsv(timestampUtcIso)).Append(',');
            sb.Append(timestampSec.ToString("F4", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(monotonicElapsedSec.ToString("F4", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(simulationTimeSec.ToString("F4", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(clockSource.ToString()).Append(',');
            sb.Append(sampleSequenceNumber.ToString(CultureInfo.InvariantCulture)).Append(',');

            // 2. Experiment & Spatial Identifiers
            sb.Append(EscapeCsv(sessionId)).Append(',');
            sb.Append(EscapeCsv(runId)).Append(',');
            sb.Append(EscapeCsv(trialId)).Append(',');
            sb.Append(EscapeCsv(scenarioId)).Append(',');
            sb.Append(floorId.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(doorwayId)).Append(',');

            // 3. Doorway Physical & Crowd Metrics
            if (doorwayWidthMeters > 0.001f && !float.IsNaN(doorwayWidthMeters) && !float.IsInfinity(doorwayWidthMeters))
                sb.Append(doorwayWidthMeters.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(',');

            sb.Append(doorwayOccupancy.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(agentsCrossingInterval.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(cumulativeCrossings.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(instantaneousDischargeRate.ToString("F3", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(rollingDischargeRate.ToString("F3", CultureInfo.InvariantCulture)).Append(',');

            if (normalizedFlow >= 0f && !float.IsNaN(normalizedFlow) && !float.IsInfinity(normalizedFlow))
                sb.Append(normalizedFlow.ToString("F3", CultureInfo.InvariantCulture));
            sb.Append(',');

            sb.Append(queueLength.ToString(CultureInfo.InvariantCulture)).Append(',');

            if (estimatedQueueLengthMeters >= 0f && !float.IsNaN(estimatedQueueLengthMeters) && !float.IsInfinity(estimatedQueueLengthMeters))
                sb.Append(estimatedQueueLengthMeters.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(',');

            // 4. Doorway Jam Dynamics
            sb.Append(isJammed ? "1" : "0").Append(',');
            sb.Append(jamDurationSec.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(maxQueueSizeDuringJam.ToString(CultureInfo.InvariantCulture)).Append(',');

            // 5. Environmental & Evacuation Macro State
            if (timeSinceEvacuationBeganSec >= 0f && !float.IsNaN(timeSinceEvacuationBeganSec) && !float.IsInfinity(timeSinceEvacuationBeganSec))
                sb.Append(timeSinceEvacuationBeganSec.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(',');

            if (smokeOrVisibilityLevel >= 0f && !float.IsNaN(smokeOrVisibilityLevel) && !float.IsInfinity(smokeOrVisibilityLevel))
                sb.Append(smokeOrVisibilityLevel.ToString("F3", CultureInfo.InvariantCulture));
            sb.Append(',');

            sb.Append(evacuationState.ToString()).Append(',');
            sb.Append(simulationStatus.ToString()).Append(',');

            // 6. Network Coordination & Command Telemetry
            sb.Append(EscapeCsv(networkRole ?? "Standalone")).Append(',');

            if (networkLatencyMs >= 0f && !float.IsNaN(networkLatencyMs) && !float.IsInfinity(networkLatencyMs))
                sb.Append(networkLatencyMs.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(',');

            sb.Append(networkConnected ? "1" : "0").Append(',');

            if (packetLossEstimatePercent >= 0f && !float.IsNaN(packetLossEstimatePercent) && !float.IsInfinity(packetLossEstimatePercent))
                sb.Append(packetLossEstimatePercent.ToString("F2", CultureInfo.InvariantCulture));
            sb.Append(',');

            if (commandDispatchLatencySec >= 0f && !float.IsNaN(commandDispatchLatencySec) && !float.IsInfinity(commandDispatchLatencySec))
                sb.Append(commandDispatchLatencySec.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(',');

            if (commandDispatchTime > 0.0 && !double.IsNaN(commandDispatchTime) && !double.IsInfinity(commandDispatchTime))
                sb.Append(commandDispatchTime.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(',');

            if (commandAckTime > 0.0 && !double.IsNaN(commandAckTime) && !double.IsInfinity(commandAckTime))
                sb.Append(commandAckTime.ToString("F4", CultureInfo.InvariantCulture));
            sb.Append(',');

            sb.Append(ackTimeoutCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(transportStats ?? string.Empty)).Append(',');

            sb.Append(EscapeCsv(lastCommandId ?? string.Empty)).Append(',');
            sb.Append(lastCommandSequence.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(commandStatus.ToString()).Append(',');

            // 7. Diagnostics & Buffer Health
            sb.Append(droppedSamplesCumulative.ToString(CultureInfo.InvariantCulture));

            return sb.ToString();
        }

        private static string EscapeCsv(string field)
        {
            if (string.IsNullOrEmpty(field)) return "\"\"";
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
            {
                return "\"" + field.Replace("\"", "\"\"") + "\"";
            }
            return field;
        }
    }
}
