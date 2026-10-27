using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace C067.Coordination
{
    public enum TrialOrderingMethod
    {
        Sequential = 0,
        RandomizedSeed = 1,
        CounterbalancedLatinSquare = 2
    }

    /// <summary>
    /// Master Human Factors Trial Manager & Experimental Design Controller for C067.
    /// Manages experimental protocol lifecycle: NotStarted -> Ready -> Running -> Assessment -> Completed,
    /// coordinates 50 default simulated trials, enforces participant vs trial ID separation,
    /// drives PreEvacuationDelayTracker, NASA-TLX, and SSQ assessments, and logs authoritative results.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class HumanFactorsTrialManager : MonoBehaviour
    {
        public static HumanFactorsTrialManager Instance { get; private set; }

        [Header("Experimental Session Configuration")]
        [Tooltip("Anonymous participant identifier (e.g. P001)")]
        public string participantId = "P001";

        [Tooltip("Configurable number of evaluation trials (Default = 50 per specification)")]
        [Range(1, 100)]
        public int evaluationTrialCount = 50;

        [Tooltip("Include an initial orientation/practice trial excluded from primary analysis")]
        public bool includePracticeTrial = true;

        [Tooltip("Condition presentation ordering protocol")]
        public TrialOrderingMethod orderingMethod = TrialOrderingMethod.RandomizedSeed;

        [Tooltip("Random seed ensuring exact experimental reproducibility")]
        public int randomSeed = 42;

        [Tooltip("Relative or absolute output directory for session CSVs and JSONs")]
        public string outputDirectory = "Telemetry/HumanFactors";

        [Header("Trial State Machine")]
        [SerializeField] private TrialLifecycleState _currentState = TrialLifecycleState.NotStarted;
        [SerializeField] private int _currentTrialIndex = 0;
        public TrialLifecycleState CurrentState => _currentState;
        public int CurrentTrialIndex => _currentTrialIndex;

        [Header("Current Trial References")]
        public TrialConfiguration currentConfig;
        public TrialResult currentResult;

        [Header("Component Integrations")]
        public PreEvacuationDelayTracker preEvacTracker;
        public NASA_TLXAssessmentController tlxController;
        public SSQAssessmentController ssqController;
        public BottleneckTelemetryLogger telemetryLogger;
        public EvacuationEgressManager egressManager;
        public VRHostelLocomotion playerLocomotion;

        [Header("Session Tracking")]
        private ParticipantSession _activeSession;
        public ParticipantSession ActiveSession => _activeSession;

        private Coroutine _trialExecutionCoroutine;
        private double _stateTransitionTimestamp = 0.0;

        public event Action<TrialLifecycleState, double> OnTrialStateChanged;
        public event Action<TrialResult> OnTrialCompleted;
        public event Action<ParticipantSession> OnSessionCompleted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance == null && FindAnyObjectByType<HumanFactorsTrialManager>() == null)
            {
                var go = new GameObject("HumanFactors_TrialManager");
                go.AddComponent<HumanFactorsTrialManager>();
                Debug.Log("[HumanFactorsTrialManager] Automatically bootstrapped HumanFactorsTrialManager into scene.");
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            AutoWireComponents();
        }

        private void Start()
        {
            // Auto-initialize session if not already loaded
            if (_activeSession == null)
            {
                InitializeSession(participantId, evaluationTrialCount, randomSeed);
                SaveSessionProgress();
            }
        }

        private void AutoWireComponents()
        {
            if (preEvacTracker == null) preEvacTracker = FindAnyObjectByType<PreEvacuationDelayTracker>() ?? gameObject.AddComponent<PreEvacuationDelayTracker>();
            if (tlxController == null) tlxController = FindAnyObjectByType<NASA_TLXAssessmentController>() ?? gameObject.AddComponent<NASA_TLXAssessmentController>();
            if (ssqController == null) ssqController = FindAnyObjectByType<SSQAssessmentController>() ?? gameObject.AddComponent<SSQAssessmentController>();
            if (telemetryLogger == null) telemetryLogger = FindAnyObjectByType<BottleneckTelemetryLogger>();
            if (egressManager == null) egressManager = FindAnyObjectByType<EvacuationEgressManager>();
            if (playerLocomotion == null) playerLocomotion = FindAnyObjectByType<VRHostelLocomotion>();
        }

        /// <summary>
        /// Initializes a new experimental participant session with 50 default trials.
        /// </summary>
        public void InitializeSession(string pId, int trialCount = 50, int seed = 42)
        {
            participantId = string.IsNullOrEmpty(pId) ? "P001" : pId;
            evaluationTrialCount = trialCount;
            randomSeed = seed;

            _activeSession = new ParticipantSession(participantId, randomSeed);

            // Generate reproducible trial configurations
            var configs = TrialConfiguration.GenerateDefaultSuite(evaluationTrialCount, randomSeed, participantId, includePracticeTrial);

            // Apply ordering protocol
            if (orderingMethod == TrialOrderingMethod.RandomizedSeed)
            {
                ApplyRandomizedOrdering(configs, randomSeed, includePracticeTrial);
            }

            _activeSession.configurations = configs;
            _currentTrialIndex = 0;
            TransitionState(TrialLifecycleState.NotStarted);

            Debug.Log($"[HumanFactorsTrialManager] Initialized session for {participantId} with {configs.Count} trials (Default: 50 evaluation trials, Seed: {randomSeed}).");
        }

        private static void ApplyRandomizedOrdering(List<TrialConfiguration> configs, int seed, bool hasPractice)
        {
            int startIdx = hasPractice ? 1 : 0;
            int count = configs.Count - startIdx;
            if (count <= 1) return;

            var rng = new System.Random(seed);
            // Fisher-Yates shuffle
            for (int i = count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var temp = configs[startIdx + i];
                configs[startIdx + i] = configs[startIdx + j];
                configs[startIdx + j] = temp;
            }

            // Update assignedOrderIndex
            for (int i = 0; i < configs.Count; i++)
            {
                configs[i].assignedOrderIndex = i + (hasPractice ? 0 : 1);
            }
        }

        /// <summary>
        /// Transitions the state machine safely to the next lifecycle stage.
        /// </summary>
        private void TransitionState(TrialLifecycleState newState)
        {
            _currentState = newState;
            _stateTransitionTimestamp = Time.timeAsDouble;
            OnTrialStateChanged?.Invoke(_currentState, _stateTransitionTimestamp);
            Debug.Log($"[HumanFactorsTrialManager] State -> {newState} (t={_stateTransitionTimestamp:F3}s)");
        }

        /// <summary>
        /// Prepares the next trial in the session queue, placing the system in the Ready state.
        /// </summary>
        public bool PrepareNextTrial()
        {
            if (_activeSession == null || _activeSession.configurations.Count == 0)
            {
                Debug.LogError("[HumanFactorsTrialManager] Cannot prepare trial: No active session or configurations.");
                return false;
            }

            if (_currentTrialIndex >= _activeSession.configurations.Count)
            {
                Debug.Log("[HumanFactorsTrialManager] All trials in session have been completed!");
                CompleteSession();
                return false;
            }

            currentConfig = _activeSession.configurations[_currentTrialIndex];

            currentResult = new TrialResult
            {
                trialId = currentConfig.trialId,
                participantId = currentConfig.participantId,
                scenarioId = currentConfig.scenarioId,
                configurationId = currentConfig.configurationId,
                conditionLabel = currentConfig.conditionLabel,
                isPractice = currentConfig.isPractice,
                assignedOrderIndex = currentConfig.assignedOrderIndex,
                startingFloor = currentConfig.startingFloor,
                alertType = currentConfig.alertType.ToString(),
                smokeCondition = currentConfig.smokeCondition.ToString(),
                smokeDensity = currentConfig.smokeDensity,
                visibilityMeters = currentConfig.visibilityMeters,
                crowdDensity = currentConfig.agentCount,
                routeConfiguration = currentConfig.routeConfiguration.ToString(),
                state = TrialLifecycleState.Ready,
                wallClockStartUtc = DateTime.UtcNow.ToString("o")
            };

            // Configure environmental parameters
            ApplyTrialEnvironment(currentConfig);

            // Cleanly reset C067 coordination trackers and jam detectors
            if (C067CoordinationManager.Instance != null)
            {
                C067CoordinationManager.Instance.ResetTrial();
            }

            TransitionState(TrialLifecycleState.Ready);
            return true;
        }

        /// <summary>
        /// Starts the prepared trial: engages telemetry logging, schedules emergency alarm onset,
        /// and initializes pre-evacuation delay monitoring.
        /// </summary>
        public bool StartTrial()
        {
            if (_currentState != TrialLifecycleState.Ready || currentConfig == null || currentResult == null)
            {
                Debug.LogWarning("[HumanFactorsTrialManager] Cannot start trial: Must be in Ready state with prepared configuration.");
                return false;
            }

            TransitionState(TrialLifecycleState.Running);

            double now = Time.timeAsDouble;
            currentResult.trialStartTime = now;
            currentResult.state = TrialLifecycleState.Running;

            // Start pre-evacuation delay tracker
            if (preEvacTracker != null)
            {
                preEvacTracker.StartTrialTracking(
                    currentConfig.participantId,
                    currentConfig.trialId,
                    currentConfig.startingFloor,
                    currentConfig.configurationId,
                    currentConfig.smokeCondition.ToString(),
                    currentConfig.crowdDensity.ToString(),
                    currentConfig.alertType.ToString(),
                    MeasurementSource.HumanParticipant_VR
                );
            }

            // Start 90 Hz telemetry logging
            if (telemetryLogger != null)
            {
                telemetryLogger.sessionId = _activeSession.sessionId;
                telemetryLogger.participantId = currentConfig.participantId;
                telemetryLogger.trialId = currentConfig.trialId;
                telemetryLogger.scenarioId = currentConfig.scenarioId;
                telemetryLogger.configurationId = currentConfig.configurationId;
                telemetryLogger.floorId = currentConfig.startingFloor;
                telemetryLogger.StartLogging();
            }

            // Schedule emergency alert onset and timeout monitoring
            if (_trialExecutionCoroutine != null) StopCoroutine(_trialExecutionCoroutine);
            _trialExecutionCoroutine = StartCoroutine(ExecuteTrialLifecycle(currentConfig));

            return true;
        }

        private IEnumerator ExecuteTrialLifecycle(TrialConfiguration config)
        {
            // 1. Wait for configured alert onset delay
            yield return new WaitForSeconds(config.alertOnsetDelaySeconds);

            if (_currentState != TrialLifecycleState.Running) yield break;

            // 2. Issue Emergency Alarm Alert
            double alertTime = Time.timeAsDouble;
            currentResult.alertOnsetTimestamp = alertTime;

            if (preEvacTracker != null)
            {
                preEvacTracker.RecordAlertOnset(alertTime);
            }

            TriggerEnvironmentalAlert(config.alertType);

            // 3. Monitor for trial completion or timeout
            float elapsed = 0f;
            while (_currentState == TrialLifecycleState.Running && elapsed < config.maxTrialDurationSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (_currentState == TrialLifecycleState.Running)
            {
                // Timeout reached without completion -> Censored
                Debug.LogWarning($"[HumanFactorsTrialManager] Trial {config.trialId} timed out after {config.maxTrialDurationSeconds}s.");
                currentResult.isCensored = true;
                currentResult.censoringReason = "MaxDurationTimeout";
                ProceedToAssessment();
            }
        }

        /// <summary>
        /// Called when the participant or crowd arrives at the exterior safe assembly plaza.
        /// Transitions lifecycle to Assessment (NASA-TLX and SSQ).
        /// </summary>
        public void NotifySafeZoneReached()
        {
            if (_currentState != TrialLifecycleState.Running) return;

            double now = Time.timeAsDouble;
            currentResult.safeZoneArrivalTime = now;

            if (preEvacTracker != null)
            {
                preEvacTracker.RecordSafeZoneArrival(now);
            }

            ProceedToAssessment();
        }

        private void ProceedToAssessment()
        {
            TransitionState(TrialLifecycleState.Assessment);

            if (telemetryLogger != null && telemetryLogger.IsLoggingActive)
            {
                telemetryLogger.StopLogging();
            }

            // Finalize pre-evacuation tracking record
            if (preEvacTracker != null)
            {
                var preRec = preEvacTracker.CompleteTrial(
                    success: !currentResult.isCensored,
                    aborted: currentResult.state == TrialLifecycleState.Aborted,
                    abortReason: currentResult.censoringReason
                );

                if (preRec != null)
                {
                    currentResult.evacuationStartTime = preRec.firstMovementTimestamp;
                    currentResult.firstDoorwayCrossingTime = preRec.firstCrossingTimestamp;
                    currentResult.preEvacuationDelay = preRec.preEvacuationDelaySeconds;
                }
            }

            // Launch NASA-TLX Assessment
            if (tlxController != null)
            {
                tlxController.StartAssessment(currentConfig.participantId, currentConfig.trialId, (tlxRes) =>
                {
                    if (tlxRes != null)
                    {
                        currentResult.rawNasaTlxScore = tlxRes.rawTlxScore;
                        currentResult.weightedNasaTlxScore = tlxRes.weightedTlxScore;
                    }
                    CompleteCurrentTrial();
                });
            }
            else
            {
                CompleteCurrentTrial();
            }
        }

        /// <summary>
        /// Finalizes the current trial, records results, saves to session, and transitions to Completed.
        /// </summary>
        public void CompleteCurrentTrial()
        {
            if (currentResult == null) return;

            currentResult.trialCompletionTime = Time.timeAsDouble;
            currentResult.wallClockEndUtc = DateTime.UtcNow.ToString("o");
            currentResult.state = TrialLifecycleState.Completed;

            currentResult.FinalizeDurations(currentConfig != null ? currentConfig.maxTrialDurationSeconds : 300.0);

            // Record telemetry summary
            if (egressManager != null)
            {
                currentResult.activeAgentsAtStart = currentConfig != null ? currentConfig.agentCount : 300;
                currentResult.evacuatedAgentCount = egressManager.EvacuatedAgentCount;
            }

            if (C067CoordinationManager.Instance != null && C067CoordinationManager.Instance.mainEntranceTracker != null)
            {
                var tracker = C067CoordinationManager.Instance.mainEntranceTracker;
                currentResult.totalPedestrianCrossings = tracker.CumulativeCrossingsTrial;
                currentResult.meanDischargeRate = tracker.RollingDischargeRate;
                currentResult.peakDischargeRate = tracker.InstantaneousDischargeRate;
            }

            _activeSession.AddResult(currentResult);
            _currentTrialIndex++;

            TransitionState(TrialLifecycleState.Completed);
            OnTrialCompleted?.Invoke(currentResult);

            SaveSessionProgress();
        }

        /// <summary>
        /// Aborts the active trial safely, preserving timestamps and recording adverse event notes.
        /// </summary>
        public void AbortTrial(string reason = "OperatorAborted", bool isAdverseEvent = false, string adverseNotes = "")
        {
            if (_trialExecutionCoroutine != null) StopCoroutine(_trialExecutionCoroutine);

            if (currentResult != null)
            {
                currentResult.state = TrialLifecycleState.Aborted;
                currentResult.isCensored = true;
                currentResult.censoringReason = reason;
                currentResult.adverseEvent = isAdverseEvent;
                currentResult.adverseEventNotes = adverseNotes;
                currentResult.trialCompletionTime = Time.timeAsDouble;
                currentResult.wallClockEndUtc = DateTime.UtcNow.ToString("o");
                currentResult.FinalizeDurations(300.0);

                _activeSession.AddResult(currentResult);
            }

            if (telemetryLogger != null) telemetryLogger.StopLogging();
            if (preEvacTracker != null) preEvacTracker.CompleteTrial(success: false, aborted: true, abortReason: reason);

            TransitionState(TrialLifecycleState.Aborted);
            SaveSessionProgress();
            Debug.LogWarning($"[HumanFactorsTrialManager] Trial {currentConfig?.trialId} aborted. Reason: {reason}");
        }

        public void ExcludeTrial(string trialId, string reason)
        {
            if (_activeSession == null) return;
            var trial = _activeSession.results.Find(r => r.trialId == trialId);
            if (trial != null)
            {
                trial.isExcluded = true;
                trial.exclusionReason = reason;
                SaveSessionProgress();
                Debug.Log($"[HumanFactorsTrialManager] Trial {trialId} marked excluded. Reason: {reason}");
            }
        }

        private void CompleteSession()
        {
            if (_activeSession == null) return;

            _activeSession.sessionEndUtc = DateTime.UtcNow.ToString("o");
            SaveSessionProgress();

            // Run post-session SSQ assessment if available
            if (ssqController != null)
            {
                ssqController.StartAssessment(participantId, "SESSION_POST", SSQAssessmentTiming.PostSession, (ssqRec) =>
                {
                    _activeSession.postSessionSsq = ssqRec;
                    SaveSessionProgress();
                });
            }

            OnSessionCompleted?.Invoke(_activeSession);
            Debug.Log($"[HumanFactorsTrialManager] Session for {participantId} completed. Total recorded trials: {_activeSession.results.Count}");
        }

        private void SaveSessionProgress()
        {
            if (_activeSession == null) return;

            string basePath = Path.Combine(Application.persistentDataPath, outputDirectory);
            string csvPath = Path.Combine(basePath, $"{_activeSession.sessionId}_{participantId}_Trials.csv");
            string jsonPath = Path.Combine(basePath, $"{_activeSession.sessionId}_{participantId}_Session.json");

            _activeSession.ExportCsv(csvPath);
            _activeSession.SaveSessionJson(jsonPath);
        }

        private void ApplyTrialEnvironment(TrialConfiguration cfg)
        {
            if (egressManager != null)
            {
                egressManager.SmokeDensity = cfg.smokeDensity;
                egressManager.agentCount = cfg.agentCount;
            }

            if (playerLocomotion != null)
            {
                playerLocomotion.transform.position = cfg.startPosition;
            }
        }

        private void TriggerEnvironmentalAlert(EmergencyAlertType alertType)
        {
            Debug.Log($"[HumanFactorsTrialManager] Triggering emergency alert: {alertType}");
            // Communicates with scene strobe and fire hazards
            if (egressManager != null)
            {
                egressManager.strobeIntensityMax = (alertType == EmergencyAlertType.VisualStrobeOnly || alertType == EmergencyAlertType.VisualStrobeAndVoice) ? 5.0f : 0.0f;
            }
        }

        /// <summary>
        /// Automated evaluation executor: Runs all 50 evaluation trials in high-speed simulation mode.
        /// Enables automated verification and benchmark generation without requiring physical human manual walking.
        /// </summary>
        public void RunSimulatedBatchEvaluation(int count = 50, Action onFinished = null)
        {
            StartCoroutine(ExecuteBatchSimulation(count, onFinished));
        }

        private IEnumerator ExecuteBatchSimulation(int count, Action onFinished)
        {
            Debug.Log($"[HumanFactorsTrialManager] Starting automated batch simulation of {count} evaluation trials...");
            InitializeSession("P_SIM_BENCHMARK", count, 100);

            for (int i = 0; i < _activeSession.configurations.Count; i++)
            {
                if (!PrepareNextTrial()) break;

                StartTrial();

                // Wait 1.0 second simulation step
                yield return new WaitForSeconds(0.2f);

                // Simulate first movement
                if (preEvacTracker != null)
                {
                    preEvacTracker.ConfirmFirstQualifyingMovement(Time.timeAsDouble);
                }

                yield return new WaitForSeconds(0.3f);

                // Complete trial
                NotifySafeZoneReached();

                // If TLX is pending, submit simulated responses
                if (tlxController != null && tlxController.isAssessmentActive)
                {
                    tlxController.SetRating(0, 45f);
                    tlxController.SetRating(1, 55f);
                    tlxController.SetRating(2, 60f);
                    tlxController.SetRating(3, 30f);
                    tlxController.SetRating(4, 50f);
                    tlxController.SetRating(5, 40f);
                    tlxController.SubmitAssessment();
                }

                yield return null;
            }

            Debug.Log($"[HumanFactorsTrialManager] Batch simulation completed! {_activeSession.results.Count} trials processed.");
            onFinished?.Invoke();
        }
    }
}
