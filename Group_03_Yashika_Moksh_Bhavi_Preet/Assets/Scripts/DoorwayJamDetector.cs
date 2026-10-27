using System;
using UnityEngine;

namespace C067.Coordination
{
    /// <summary>
    /// Evaluates crowd congestion dynamics to reliably detect and track physical bottleneck jams at doorways.
    /// Incorporates configurable persistence timers and Schmitt-trigger hysteresis to eliminate flicker.
    /// Handles empty doorways safely and tracks jam duration, crossings, and peak queue size.
    /// </summary>
    [RequireComponent(typeof(DoorwayCrossingTracker))]
    public class DoorwayJamDetector : MonoBehaviour
    {
        [Header("Jam Trigger Thresholds")]
        [Tooltip("Minimum upstream queue length to satisfy jam condition")]
        public int queueLengthThreshold = 5;

        [Tooltip("Minimum doorway occupancy to satisfy jam condition")]
        public int occupancyThreshold = 3;

        [Tooltip("Discharge rate (agents/sec) below which congestion is considered stalled")]
        public float lowDischargeThreshold = 0.5f;

        [Header("Temporal Persistence & Hysteresis")]
        [Tooltip("Time in seconds that jam criteria must continuously persist before declaring a jam")]
        [Min(0.1f)]
        public float jamPersistenceDuration = 1.5f;

        [Tooltip("Time in seconds that cleared criteria must continuously persist before resolving a jam")]
        [Min(0.1f)]
        public float recoveryPersistenceDuration = 1.0f;

        [Tooltip("Hysteresis multiplier applied to queue threshold for recovery (e.g. 0.6 = queue must drop to 60% of threshold)")]
        [Range(0.2f, 0.9f)]
        public float hysteresisRatio = 0.6f;

        [Header("Runtime State (Read-Only)")]
        [SerializeField] private bool _isJammed = false;
        [SerializeField] private float _currentJamDuration = 0f;
        [SerializeField] private float _totalJamDurationCumulative = 0f;
        [SerializeField] private int _maxQueueSizeDuringJam = 0;
        [SerializeField] private int _crossingsDuringCurrentJam = 0;
        [SerializeField] private float _jamOnsetTimer = 0f;
        [SerializeField] private float _recoveryTimer = 0f;

        private DoorwayCrossingTracker _tracker;
        private double _jamStartMonotonicTime = -1;
        private string _jamStartUtcIso = string.Empty;
        private int _crossingsAtJamStart = 0;
        private bool _isSimulationPaused = false;

        // Public Properties
        public bool IsJammed => _isJammed;
        public float CurrentJamDuration => _currentJamDuration;
        public float TotalJamDurationCumulative => _totalJamDurationCumulative;
        public int MaxQueueSizeDuringJam => _maxQueueSizeDuringJam;
        public int CrossingsDuringCurrentJam => _crossingsDuringCurrentJam;
        public string JamStartUtcIso => _jamStartUtcIso;
        public string DoorwayId => _tracker != null ? _tracker.DoorwayId : string.Empty;
        public int FloorId => _tracker != null ? _tracker.FloorId : 0;

        // Events
        public event Action<string, int, float> OnJamDeclared; // doorwayId, floorId, timestamp
        public event Action<string, int, float, float> OnJamResolved; // doorwayId, floorId, duration, maxQueue

        private void Awake()
        {
            _tracker = GetComponent<DoorwayCrossingTracker>();
        }

        private void Update()
        {
            if (_isSimulationPaused) return;

            EvaluateJamState(Time.deltaTime);
        }

        /// <summary>
        /// Core evaluation step executed each frame.
        /// </summary>
        public void EvaluateJamState(float dt)
        {
            if (_tracker == null) return;

            int queue = _tracker.CurrentQueueLength;
            int occupancy = _tracker.CurrentOccupancy;
            float discharge = _tracker.RollingDischargeRate;

            // SAFETY CRITERION: Empty doorway is NEVER jammed
            if (queue == 0 && occupancy == 0)
            {
                if (_isJammed)
                {
                    ResolveJam();
                }
                _jamOnsetTimer = 0f;
                _recoveryTimer = 0f;
                return;
            }

            // Jam Onset Condition:
            // High upstream queue OR high occupancy, AND stalled discharge
            bool meetsOnsetConditions = (queue >= queueLengthThreshold || occupancy >= occupancyThreshold)
                                        && (discharge <= lowDischargeThreshold);

            if (!_isJammed)
            {
                // Evaluating for Jam Onset
                if (meetsOnsetConditions)
                {
                    _jamOnsetTimer += dt;
                    if (_jamOnsetTimer >= jamPersistenceDuration)
                    {
                        DeclareJam();
                    }
                }
                else
                {
                    _jamOnsetTimer = Mathf.Max(0f, _jamOnsetTimer - dt * 1.5f);
                }
            }
            else
            {
                // Jam is currently active: track duration, peak queue, and crossings
                _currentJamDuration += dt;
                _totalJamDurationCumulative += dt;
                _maxQueueSizeDuringJam = Mathf.Max(_maxQueueSizeDuringJam, queue);
                _crossingsDuringCurrentJam = _tracker.CumulativeCrossingsTrial - _crossingsAtJamStart;

                // Recovery Condition with Hysteresis:
                // Queue must drop below relaxed threshold (queue < threshold * hysteresisRatio)
                // OR sustained healthy discharge achieved
                int recoveryQueueThreshold = Mathf.Max(1, Mathf.RoundToInt(queueLengthThreshold * hysteresisRatio));
                bool meetsRecoveryConditions = (queue < recoveryQueueThreshold && occupancy < occupancyThreshold)
                                               || (discharge > lowDischargeThreshold * 1.5f);

                if (meetsRecoveryConditions)
                {
                    _recoveryTimer += dt;
                    if (_recoveryTimer >= recoveryPersistenceDuration)
                    {
                        ResolveJam();
                    }
                }
                else
                {
                    _recoveryTimer = Mathf.Max(0f, _recoveryTimer - dt * 2.0f);
                }
            }
        }

        private void DeclareJam()
        {
            _isJammed = true;
            _currentJamDuration = 0f;
            _maxQueueSizeDuringJam = _tracker.CurrentQueueLength;
            _jamStartMonotonicTime = Time.realtimeSinceStartupAsDouble;
            _jamStartUtcIso = DateTime.UtcNow.ToString("o");
            _crossingsAtJamStart = _tracker.CumulativeCrossingsTrial;
            _crossingsDuringCurrentJam = 0;
            _recoveryTimer = 0f;

            OnJamDeclared?.Invoke(_tracker.DoorwayId, _tracker.FloorId, (float)_jamStartMonotonicTime);
        }

        private void ResolveJam()
        {
            float duration = _currentJamDuration;
            int maxQ = _maxQueueSizeDuringJam;

            _isJammed = false;
            _currentJamDuration = 0f;
            _jamOnsetTimer = 0f;
            _recoveryTimer = 0f;

            OnJamResolved?.Invoke(_tracker.DoorwayId, _tracker.FloorId, duration, maxQ);
        }

        /// <summary>
        /// Sets pause state (e.g. simulation pause) to avoid accumulating false jam duration.
        /// </summary>
        public void SetSimulationPaused(bool paused)
        {
            _isSimulationPaused = paused;
        }

        /// <summary>
        /// Resets detector for a new trial.
        /// </summary>
        public void ResetTrial()
        {
            _isJammed = false;
            _currentJamDuration = 0f;
            _maxQueueSizeDuringJam = 0;
            _crossingsDuringCurrentJam = 0;
            _jamOnsetTimer = 0f;
            _recoveryTimer = 0f;
            _jamStartMonotonicTime = -1;
            _jamStartUtcIso = string.Empty;
        }

        /// <summary>
        /// Resets all data including cumulative jam totals.
        /// </summary>
        public void ResetSession()
        {
            ResetTrial();
            _totalJamDurationCumulative = 0f;
        }
    }
}
