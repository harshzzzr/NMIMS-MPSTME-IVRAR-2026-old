using System;
using System.IO;
using UnityEngine;

namespace C067.Coordination
{
    /// <summary>
    /// High-frequency telemetry logger for evacuation bottleneck and doorway flow dynamics.
    /// Operates at a configurable target sampling rate (default 90 Hz) using an accumulator mechanism
    /// decoupled from the rendering frame rate.
    /// Supports dual clock sources: MonotonicRealtime (for operational monitoring) and SimulationTime
    /// (for reproducible experiments).
    /// Emits structured, typed samples to a thread-safe, bounded, non-blocking background CSV writer.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class BottleneckTelemetryLogger : MonoBehaviour
    {
        public static BottleneckTelemetryLogger Instance { get; private set; }

        // =========================================================================
        // 2.1 & 2.2 INSPECTOR CONFIGURATION & SETTINGS
        // =========================================================================

        [Header("Sampling System (2.1)")]
        [Tooltip("Target telemetry sampling rate in Hertz (strictly 90 Hz by default)")]
        [Range(10f, 200f)]
        public float targetSamplingRate = 90.0f;

        [Tooltip("Clock source for interval sampling: MonotonicRealtime (wall-clock) vs SimulationTime (scaled simulation)")]
        public TelemetryClockSource clockSource = TelemetryClockSource.MonotonicRealtime;

        [Tooltip("Maximum samples allowed to be emitted in a single frame to prevent spiral-of-death")]
        [Range(1, 20)]
        public int maxSamplesPerFrame = 10;

        [Header("Experiment & Spatial Identifiers (2.2)")]
        [Tooltip("High-level test session identifier")]
        public string sessionId = "HOSTEL_EVAC_01";

        [Tooltip("Participant identifier")]
        public string participantId = "P001";

        [Tooltip("Specific simulation run identifier")]
        public string runId = "RUN_001";

        [Tooltip("Individual trial identifier")]
        public string trialId = "TRIAL_A";

        [Tooltip("Experimental scenario or architectural configuration ID")]
        public string scenarioId = "STANDARD_NIGHT_EVAC";

        [Tooltip("Detailed configuration ID")]
        public string configurationId = "CFG_STANDARD";

        [Tooltip("Hostel floor level index (0 = Ground, 1 = Floor 1, ..., 4 = Floor 4)")]
        public int floorId = 0;

        [Tooltip("Unique doorway identifier")]
        public string doorwayId = "Ground_MainExit";

        [Header("Monitored Doorway & Jam Components")]
        [Tooltip("Doorway crossing tracker being monitored")]
        public DoorwayCrossingTracker doorwayTracker;

        [Tooltip("Jam detector associated with the monitored doorway")]
        public DoorwayJamDetector jamDetector;

        [Header("Storage & Buffered Writing (2.4)")]
        [Tooltip("Custom output directory. If empty, defaults to {Application.persistentDataPath}/TelemetryLogs")]
        public string outputDirectory = string.Empty;

        [Tooltip("Filename template. Supported tokens: {session}, {run}, {trial}, {scenario}, {floor}, {door}")]
        public string filenameTemplate = "evac_telemetry_{session}_{run}_{trial}_{door}";

        [Tooltip("Maximum capacity of bounded producer-consumer queue before dropping samples")]
        [Range(1000, 200000)]
        public int writerQueueCapacity = 50000;

        [Tooltip("Interval in seconds between background file write flushes")]
        [Range(0.005f, 5.0f)]
        public float fileFlushIntervalSeconds = 0.05f;

        [Tooltip("Sliding time window in seconds for rolling doorway discharge rate")]
        [Range(0.5f, 10.0f)]
        public float rollingDischargeWindowSeconds = 2.0f;

        [Tooltip("Enable detailed per-agent level crossing records")]
        public bool enableDetailedAgentLogging = false;

        [Tooltip("Master switch to enable or disable telemetry collection")]
        public bool loggingEnabled = true;

        [Tooltip("Automatically start logging when Start() executes")]
        public bool autoStartLogging = true;

        // Backward-compatibility alias for buffer capacity
        public int bufferCapacity
        {
            get => writerQueueCapacity;
            set => writerQueueCapacity = value;
        }

        // =========================================================================
        // RUNTIME STATE & DIAGNOSTICS (READ-ONLY)
        // =========================================================================

        [Header("Runtime State & Diagnostics (Read-Only)")]
        [SerializeField] private bool _isLoggingActive = false;
        [SerializeField] private long _sampleSequenceNumber = 0;
        [SerializeField] private int _missedCadenceTicks = 0;
        [SerializeField] private int _droppedSamplesCount = 0;
        [SerializeField] private string _activeCsvFilePath = string.Empty;
        [SerializeField] private float _actualSamplingCadenceHz = 0f;

        private TelemetryCsvWriter _csvWriter;
        private double _samplingInterval;
        private double _timeAccumulator;
        private double _lastFrameClockTime;
        private double _evacuationStartTime = -1.0;

        // Cadence diagnostics
        private int _samplesThisSecond = 0;
        private float _secondTimer = 0f;

        // Evacuation & Simulation state caches
        private EvacuationState _evacuationState = EvacuationState.Normal;
        private SimulationStatus _simulationStatus = SimulationStatus.Running;

        // Public Properties for External & Test Inspection
        public bool IsLoggingActive => _isLoggingActive;
        public long SampleSequenceNumber => _sampleSequenceNumber;
        public int MissedCadenceTicks => _missedCadenceTicks;
        public int DroppedSamplesCount => _csvWriter != null ? _csvWriter.DroppedSamplesCount : _droppedSamplesCount;
        public string ActiveCsvFilePath => _activeCsvFilePath;
        public float ActualSamplingCadenceHz => _actualSamplingCadenceHz;
        public double SamplingInterval => targetSamplingRate > 0f ? (1.0 / (double)targetSamplingRate) : (1.0 / 90.0);

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            UpdateSamplingInterval();
        }

        private void Start()
        {
            AutoResolveReferences();

            if (autoStartLogging && loggingEnabled)
            {
                StartLogging();
            }
        }

        private void OnValidate()
        {
            UpdateSamplingInterval();

            if (doorwayTracker != null && rollingDischargeWindowSeconds > 0.1f)
            {
                doorwayTracker.rollingWindowSeconds = rollingDischargeWindowSeconds;
            }
        }

        private void Update()
        {
            if (!_isLoggingActive || !loggingEnabled) return;

            // Handle paused simulation clock
            if (clockSource == TelemetryClockSource.SimulationTime && Time.timeScale <= 0f)
            {
                _lastFrameClockTime = Time.timeAsDouble;
                return;
            }

            double currentClockTime = GetCurrentClockTime();
            double dt = currentClockTime - _lastFrameClockTime;
            _lastFrameClockTime = currentClockTime;

            // Simulation reset or negative delta-t protection
            if (dt < 0.0)
            {
                _timeAccumulator = 0.0;
                return;
            }

            // Clamp delta-t to prevent uncontrolled catch-up burst after system pause, scene hitch, or debugger stop
            if (dt > 0.25)
            {
                int skippedTicks = (int)((dt - 0.25) / _samplingInterval);
                _missedCadenceTicks += skippedTicks;
                dt = 0.25;
            }

            _timeAccumulator += dt;

            int samplesProducedThisFrame = 0;
            while (_timeAccumulator >= _samplingInterval)
            {
                _timeAccumulator -= _samplingInterval;

                if (samplesProducedThisFrame < maxSamplesPerFrame)
                {
                    double sampleClockTimestamp = currentClockTime - _timeAccumulator;
                    SampleTelemetry(sampleClockTimestamp);
                    samplesProducedThisFrame++;
                    _samplesThisSecond++;
                }
                else
                {
                    // Frame rate is running too slowly to keep pace with 90 Hz target.
                    // Drop remaining accumulated ticks to prevent spiral-of-death burst.
                    int excessTicks = (int)(_timeAccumulator / _samplingInterval);
                    _missedCadenceTicks += (excessTicks + 1);
                    _timeAccumulator = 0.0;
                    break;
                }
            }

            // Update Cadence Diagnostic Counter every 1.0s
            float deltaSecond = (clockSource == TelemetryClockSource.MonotonicRealtime) ? Time.unscaledDeltaTime : Time.deltaTime;
            _secondTimer += deltaSecond;
            if (_secondTimer >= 1.0f)
            {
                _actualSamplingCadenceHz = _samplesThisSecond / _secondTimer;
                _samplesThisSecond = 0;
                _secondTimer = 0f;
            }
        }

        /// <summary>
        /// Captures a discrete 90 Hz telemetry sample and queues it for asynchronous CSV export.
        /// Thread-safe and allocation-free.
        /// </summary>
        public void SampleTelemetry(double sampleClockTime)
        {
            _sampleSequenceNumber++;

            // 1. Resolve Doorway Metrics
            int occupancy = 0;
            int queueLen = 0;
            float queueLenMeters = -1f;
            int intervalCrossings = 0;
            int cumulativeCrossings = 0;
            float instDischarge = 0f;
            float rollingDischarge = 0f;
            float width = 2.4f;
            float normFlow = 0f;
            int floor = floorId;
            string door = string.IsNullOrEmpty(doorwayId) ? "Door" : doorwayId;

            if (doorwayTracker != null)
            {
                doorwayTracker.HarvestSamplingInterval(sampleClockTime, out intervalCrossings, out instDischarge, out normFlow);
                occupancy = doorwayTracker.CurrentOccupancy;
                queueLen = doorwayTracker.CurrentQueueLength;
                queueLenMeters = doorwayTracker.EstimatedQueueLengthMeters;
                cumulativeCrossings = doorwayTracker.CumulativeCrossingsTrial;
                rollingDischarge = doorwayTracker.RollingDischargeRate;
                width = doorwayTracker.DoorwayWidthMeters;
                floor = doorwayTracker.FloorId;
                door = doorwayTracker.DoorwayId;
            }

            // 2. Resolve Jam Metrics
            bool isJammed = false;
            float jamDuration = 0f;
            int maxQDuringJam = 0;

            if (jamDetector != null)
            {
                isJammed = jamDetector.IsJammed;
                jamDuration = jamDetector.CurrentJamDuration;
                maxQDuringJam = jamDetector.MaxQueueSizeDuringJam;
            }

            // 3. Resolve Environmental & Evacuation Macro State
            float smokeDensity = -1f;
            if (EvacuationEgressManager.Instance != null)
            {
                smokeDensity = EvacuationEgressManager.Instance.SmokeDensity;
            }

            float timeSinceEvacBegan = -1f;
            if (_evacuationStartTime > 0.0)
            {
                timeSinceEvacBegan = Mathf.Max(0f, (float)(GetCurrentClockTime() - _evacuationStartTime));
            }

            // 4. Resolve Emergency Command & Network Metrics
            string cmdId = string.Empty;
            uint cmdSeq = 0;
            double cmdDispatch = 0;
            double cmdAck = 0;
            float cmdDispatchLatency = -1f;
            CommandExecutionStatus cmdStatus = CommandExecutionStatus.Pending;
            float rttMs = -1f;
            bool connected = true;
            int ackTimeouts = 0;
            string transportStats = "Standalone_OK";
            string netRole = "Standalone";
            float packetLoss = -1f;

            if (EmergencyCommandNetwork.Instance != null)
            {
                var net = EmergencyCommandNetwork.Instance;
                netRole = (net.localRole == WardenRole.HeadWarden || net.forceOfflineMode) ? "Authority" : "Client";

                var lastCmd = net.LastExecutedCommand;
                if (lastCmd != null)
                {
                    cmdId = lastCmd.commandId;
                    cmdSeq = lastCmd.sequenceNumber;
                    cmdDispatch = lastCmd.issueMonotonicTime;
                    cmdAck = lastCmd.acknowledgementTime;
                    cmdStatus = lastCmd.status;
                    if (cmdAck > 0.0 && cmdDispatch > 0.0)
                    {
                        cmdDispatchLatency = (float)(cmdAck - cmdDispatch);
                    }
                }

                var metrics = net.GetMetricsSnapshot();
                rttMs = metrics.roundTripLatencyMs;
                connected = metrics.isConnected;
                ackTimeouts = metrics.timeoutCountTotal;
                transportStats = metrics.transportStatus;

                int totalCmds = metrics.acceptedCommandsTotal + metrics.rejectedCommandsTotal + metrics.timeoutCountTotal;
                if (totalCmds > 0)
                {
                    packetLoss = (metrics.timeoutCountTotal / (float)totalCmds) * 100f;
                }
            }

            // 5. Construct Structured Telemetry Sample
            var sample = new BottleneckTelemetrySample
            {
                timestampUtcIso = DateTime.UtcNow.ToString("o"),
                timestampSec = sampleClockTime,
                monotonicElapsedSec = Time.realtimeSinceStartupAsDouble,
                simulationTimeSec = Time.timeAsDouble,
                clockSource = clockSource,
                sampleSequenceNumber = _sampleSequenceNumber,

                sessionId = sessionId,
                runId = runId,
                trialId = trialId,
                scenarioId = scenarioId,
                floorId = floor,
                doorwayId = door,

                doorwayWidthMeters = width,
                doorwayOccupancy = occupancy,
                agentsCrossingInterval = intervalCrossings,
                cumulativeCrossings = cumulativeCrossings,
                instantaneousDischargeRate = instDischarge,
                rollingDischargeRate = rollingDischarge,
                normalizedFlow = normFlow,
                queueLength = queueLen,
                estimatedQueueLengthMeters = queueLenMeters,

                isJammed = isJammed,
                jamDurationSec = jamDuration,
                maxQueueSizeDuringJam = maxQDuringJam,

                timeSinceEvacuationBeganSec = timeSinceEvacBegan,
                smokeOrVisibilityLevel = smokeDensity,
                evacuationState = _evacuationState,
                simulationStatus = _simulationStatus,

                networkRole = netRole,
                networkLatencyMs = rttMs,
                networkConnected = connected,
                packetLossEstimatePercent = packetLoss,
                commandDispatchLatencySec = cmdDispatchLatency,
                commandDispatchTime = cmdDispatch,
                commandAckTime = cmdAck,
                ackTimeoutCount = ackTimeouts,
                transportStats = transportStats,
                lastCommandId = cmdId,
                lastCommandSequence = cmdSeq,
                commandStatus = cmdStatus,

                droppedSamplesCumulative = _csvWriter != null ? _csvWriter.DroppedSamplesCount : 0
            };

            // 6. Enqueue Sample in Non-Blocking Writer
            if (_csvWriter != null)
            {
                _csvWriter.EnqueueSample(sample);
            }
        }

        /// <summary>
        /// Begins high-speed telemetry recording, creating a new unique CSV log file.
        /// </summary>
        public void StartLogging()
        {
            if (_isLoggingActive) return;

            UpdateSamplingInterval();

            string folder = string.IsNullOrEmpty(outputDirectory)
                ? Path.Combine(Application.persistentDataPath, "TelemetryLogs")
                : outputDirectory;

            string baseName = ResolveFilenameFromTemplate();

            _csvWriter = new TelemetryCsvWriter(folder, baseName, writerQueueCapacity, fileFlushIntervalSeconds);
            _activeCsvFilePath = _csvWriter.FilePath;
            _timeAccumulator = 0.0;
            _lastFrameClockTime = GetCurrentClockTime();
            _sampleSequenceNumber = 0;
            _missedCadenceTicks = 0;
            _isLoggingActive = true;

            Debug.Log($"[BottleneckTelemetryLogger] Started 90 Hz telemetry logging -> {_activeCsvFilePath} (Clock: {clockSource})");
        }

        /// <summary>
        /// Gracefully flushes and closes active CSV log file.
        /// </summary>
        public void StopLogging()
        {
            if (!_isLoggingActive) return;

            _isLoggingActive = false;
            if (_csvWriter != null)
            {
                _csvWriter.FlushAndClose();
                _droppedSamplesCount = _csvWriter.DroppedSamplesCount;
                _csvWriter = null;
            }

            Debug.Log($"[BottleneckTelemetryLogger] Stopped telemetry logging. Sequences logged: {_sampleSequenceNumber}, Missed ticks: {_missedCadenceTicks}");
        }

        public void SetEvacuationState(EvacuationState state)
        {
            _evacuationState = state;
            if (state == EvacuationState.EvacuationInProgress && _evacuationStartTime < 0.0)
            {
                _evacuationStartTime = GetCurrentClockTime();
            }
        }

        public void SetSimulationStatus(SimulationStatus status)
        {
            _simulationStatus = status;
        }

        public void ResetTrialMetrics()
        {
            _sampleSequenceNumber = 0;
            _missedCadenceTicks = 0;
            _timeAccumulator = 0.0;
            _lastFrameClockTime = GetCurrentClockTime();

            if (doorwayTracker != null)
            {
                doorwayTracker.ResetTrialData();
            }

            if (jamDetector != null)
            {
                jamDetector.ResetTrial();
            }
        }

        private double GetCurrentClockTime()
        {
            return (clockSource == TelemetryClockSource.MonotonicRealtime)
                ? Time.realtimeSinceStartupAsDouble
                : Time.timeAsDouble;
        }

        private void UpdateSamplingInterval()
        {
            float rate = Mathf.Clamp(targetSamplingRate, 1.0f, 500.0f);
            _samplingInterval = 1.0 / (double)rate;
        }

        private string ResolveFilenameFromTemplate()
        {
            string door = doorwayTracker != null ? doorwayTracker.DoorwayId : (string.IsNullOrEmpty(doorwayId) ? "Door" : doorwayId);
            string tmpl = string.IsNullOrEmpty(filenameTemplate) ? "evac_telemetry_{session}_{run}_{trial}_{door}" : filenameTemplate;

            return tmpl
                .Replace("{session}", sessionId ?? "Session")
                .Replace("{run}", runId ?? "Run")
                .Replace("{trial}", trialId ?? "Trial")
                .Replace("{scenario}", scenarioId ?? "Scenario")
                .Replace("{floor}", floorId.ToString())
                .Replace("{door}", door);
        }

        private void AutoResolveReferences()
        {
            if (doorwayTracker == null)
            {
                doorwayTracker = FindAnyObjectByType<DoorwayCrossingTracker>();
                if (doorwayTracker == null)
                {
                    Debug.LogWarning("[BottleneckTelemetryLogger] No DoorwayCrossingTracker assigned or found in scene. Continuing with default doorway metrics.");
                }
            }

            if (doorwayTracker != null && jamDetector == null)
            {
                jamDetector = doorwayTracker.GetComponent<DoorwayJamDetector>();
            }

            if (doorwayTracker != null && rollingDischargeWindowSeconds > 0.1f)
            {
                doorwayTracker.rollingWindowSeconds = rollingDischargeWindowSeconds;
            }
        }

        private void OnDisable()
        {
            StopLogging();
        }

        private void OnDestroy()
        {
            StopLogging();
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit()
        {
            StopLogging();
        }
    }
}
