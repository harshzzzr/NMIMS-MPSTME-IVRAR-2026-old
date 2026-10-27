using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace C067.Coordination
{
    /// <summary>
    /// Utility to generate deterministic synthetic evacuation telemetry datasets for demonstration,
    /// pipeline validation, and automated offline testing.
    /// Strictly and clearly labeled as SYNTHETIC DEMONSTRATION DATA.
    /// </summary>
    public static class SyntheticTelemetryGenerator
    {
        public const string SYNTHETIC_HEADER_NOTICE = "# NOTICE: SYNTHETIC DEMONSTRATION DATASET - NOT REAL EVACUATION MEASUREMENTS";

        /// <summary>
        /// Generates a realistic synthetic CSV dataset exhibiting queue formation, bottleneck jamming,
        /// hysteresis recovery, and emergency command redirection at 90 Hz.
        /// </summary>
        public static string GenerateSyntheticCsvContent(int sampleCount = 180)
        {
            var sb = new StringBuilder();
            sb.AppendLine(SYNTHETIC_HEADER_NOTICE);
            sb.AppendLine(BottleneckTelemetrySample.CsvHeader);

            DateTime baseUtc = new DateTime(2026, 10, 22, 10, 0, 0, DateTimeKind.Utc);
            double baseMonotonic = 100.0;
            double dt = 1.0 / 90.0; // 90 Hz sampling interval (~0.0111s)

            int cumulativeCrossings = 0;
            bool isJammed = false;
            float jamDuration = 0f;
            int maxQueueDuringJam = 0;

            for (int i = 0; i < sampleCount; i++)
            {
                double monoTime = baseMonotonic + i * dt;
                DateTime sampleUtc = baseUtc.AddSeconds(i * dt);
                long seq = i + 1;

                // Phase progression:
                // 0..40: Free-flow evacuation
                // 41..90: Queue building up, discharge slowing, jam declared around tick 60
                // 91..130: Congestion / jam sustained, command issued to redirect
                // 131..180: Queue draining, recovery with hysteresis, normal flow restored
                int queue;
                int occupancy;
                int crossingsThisInterval = 0;

                if (i <= 40)
                {
                    // Free flow
                    queue = Mathf.Clamp(i / 10, 0, 3);
                    occupancy = 1;
                    if (i % 8 == 0) crossingsThisInterval = 1;
                }
                else if (i <= 90)
                {
                    // Congestion forming
                    queue = 4 + (i - 40) / 4;
                    occupancy = 3;
                    if (i % 25 == 0) crossingsThisInterval = 1; // Stalled discharge
                }
                else if (i <= 130)
                {
                    // Severe Jam
                    queue = 12 + (i % 3);
                    occupancy = 4;
                    if (i % 45 == 0) crossingsThisInterval = 1;
                }
                else
                {
                    // Draining after redirect command
                    queue = Mathf.Max(0, 14 - (i - 130) / 3);
                    occupancy = queue > 2 ? 2 : 1;
                    if (i % 12 == 0) crossingsThisInterval = 1;
                }

                cumulativeCrossings += crossingsThisInterval;

                // Jam Detection Logic Simulation
                if (queue >= 5 && occupancy >= 3 && !isJammed)
                {
                    if (i >= 60) // Persistence duration elapsed
                    {
                        isJammed = true;
                        jamDuration = 0f;
                        maxQueueDuringJam = queue;
                    }
                }
                else if (isJammed)
                {
                    jamDuration += (float)dt;
                    maxQueueDuringJam = Mathf.Max(maxQueueDuringJam, queue);

                    // Recovery condition with hysteresis
                    if (queue <= 3 && i >= 155)
                    {
                        isJammed = false;
                    }
                }

                float instDischarge = crossingsThisInterval / (float)dt;
                float doorwayWidth = 2.4f; // Main Entrance Double Door clearance (m)
                float normFlow = crossingsThisInterval / (doorwayWidth * (float)dt);
                float rollingDischarge = isJammed ? 0.35f : 1.85f;

                // Command event at tick 100
                string cmdId = i >= 100 ? "cmd_synth_redirect_01" : "";
                uint cmdSeq = i >= 100 ? 101u : 0u;
                double cmdDispatch = i >= 100 ? baseMonotonic + 100 * dt : 0;
                double cmdAck = i >= 105 ? cmdDispatch + 0.055 : 0;
                CommandExecutionStatus cmdStatus = i < 100 ? CommandExecutionStatus.Pending : (i < 105 ? CommandExecutionStatus.Accepted : CommandExecutionStatus.Completed);

                var sample = new BottleneckTelemetrySample
                {
                    timestampUtcIso = sampleUtc.ToString("o"),
                    monotonicElapsedSec = monoTime,
                    simulationTimeSec = monoTime,
                    sampleSequenceNumber = seq,
                    sessionId = "SYNTHETIC_SESSION_01",
                    runId = "SYNTH_RUN_01",
                    trialId = "SYNTH_TRIAL_01",
                    floorId = 0,
                    doorwayId = "Ground_MainExit_Synthetic",
                    evacuationState = i >= 170 ? EvacuationState.EvacuationComplete : EvacuationState.EvacuationInProgress,
                    simulationStatus = SimulationStatus.Running,
                    doorwayOccupancy = occupancy,
                    queueLength = queue,
                    agentsCrossingInterval = crossingsThisInterval,
                    cumulativeCrossings = cumulativeCrossings,
                    instantaneousDischargeRate = instDischarge,
                    rollingDischargeRate = rollingDischarge,
                    doorwayWidthMeters = doorwayWidth,
                    normalizedFlow = normFlow,
                    isJammed = isJammed,
                    jamDurationSec = isJammed ? jamDuration : 0f,
                    maxQueueSizeDuringJam = maxQueueDuringJam,
                    lastCommandId = cmdId,
                    lastCommandSequence = cmdSeq,
                    commandDispatchTime = cmdDispatch,
                    commandAckTime = cmdAck,
                    commandStatus = cmdStatus,
                    networkLatencyMs = 4.2f,
                    networkConnected = true,
                    ackTimeoutCount = 0,
                    transportStats = "Synthetic_Test_OK",
                    droppedSamplesCumulative = 0
                };

                sb.AppendLine(sample.ToCsvRow());
            }

            return sb.ToString();
        }

        /// <summary>
        /// Writes the synthetic dataset to a specified file path.
        /// </summary>
        public static string SaveSyntheticDatasetToFile(string destinationPath)
        {
            string content = GenerateSyntheticCsvContent(180);
            string dir = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(destinationPath, content, Encoding.UTF8);
            return destinationPath;
        }
    }
}
