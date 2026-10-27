using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace C067.Coordination
{
    public enum MeasurementSource
    {
        HumanParticipant_VR = 0,
        AutonomousAgent_Simulated = 1
    }

    [System.Serializable]
    public class PreEvacuationRecord
    {
        public string participantId = "P001";
        public string trialId = "TR_001";
        public int floorId = 4;
        public string conditionId = "";
        public string smokeCondition = "";
        public string crowdCondition = "";
        public string alertType = "";

        public MeasurementSource measurementSource = MeasurementSource.HumanParticipant_VR;

        // Monotonic simulation timestamps in seconds
        public double alertOnsetTimestamp = -1.0;
        public double firstMovementTimestamp = -1.0;
        public double firstCrossingTimestamp = -1.0;
        public double safeZoneArrivalTimestamp = -1.0;
        public double trialCompletionTimestamp = -1.0;

        // Operational pre-evacuation delay: firstMovement - alertOnset
        public double preEvacuationDelaySeconds = -1.0;
        public double totalEvacuationSeconds = -1.0;

        // Censoring & Exclusion status
        public bool isCensored = false;
        public string censoringReason = "";
        public bool isCompleted = false;
        public bool isAborted = false;
        public bool isExcluded = false;
        public string exclusionReason = "";

        public string wallClockUtc = "";
    }

    [System.Serializable]
    public class PreEvacuationDistributionResult
    {
        public int totalObservations = 0;
        public int validObservations = 0;
        public int censoredObservations = 0;
        public int excludedObservations = 0;

        public double mean = -1.0;
        public double median = -1.0;
        public double standardDeviation = -1.0;
        public double variance = -1.0;
        public double min = -1.0;
        public double max = -1.0;
        public double percentile25 = -1.0;
        public double percentile50 = -1.0;
        public double percentile75 = -1.0;
        public double percentile90 = -1.0;
        public double interquartileRange = -1.0;
        public double skewness = 0.0;

        public List<HistogramBin> histogramBins = new List<HistogramBin>();
        public List<EcdfPoint> ecdfPoints = new List<EcdfPoint>();
        public Dictionary<string, SubgroupStats> subgroupComparisons = new Dictionary<string, SubgroupStats>();
    }

    [System.Serializable]
    public struct HistogramBin
    {
        public double lowerBound;
        public double upperBound;
        public int count;
        public double relativeFrequency;
    }

    [System.Serializable]
    public struct EcdfPoint
    {
        public double delaySeconds;
        public double cumulativeProbability;
    }

    [System.Serializable]
    public class SubgroupStats
    {
        public string subgroupName;
        public int n;
        public double mean;
        public double median;
        public double standardDeviation;
        public double iqr;
    }

    /// <summary>
    /// Tracks human participant and simulated crowd agent pre-evacuation delay times:
    /// T_pre = first qualifying evacuation movement time - emergency alert onset time.
    /// Filters head-tracking noise and jitter, handles censoring, supports empirical distribution analysis,
    /// and distinguishes human VR participants from autonomous simulation agents.
    /// </summary>
    public class PreEvacuationDelayTracker : MonoBehaviour
    {
        public static PreEvacuationDelayTracker Instance { get; private set; }

        [Header("Movement Qualification Thresholds")]
        [Tooltip("Minimum horizontal displacement in meters required to qualify as purposeful evacuation movement (filters tracking jitter)")]
        public float movementDistanceThreshold = 0.40f;

        [Tooltip("Minimum sustained horizontal speed in m/s required to qualify")]
        public float sustainedSpeedThreshold = 0.25f;

        [Tooltip("Minimum duration in seconds that movement must be sustained to filter head turns")]
        public float sustainedMovementDuration = 0.35f;

        [Header("Subject Tracking Reference")]
        public Transform playerTrackingTarget;

        [Header("Runtime State")]
        public bool isTrackingActive = false;
        public bool alertHasSounded = false;
        public bool firstMovementDetected = false;

        private PreEvacuationRecord _currentRecord;
        private Vector3 _positionAtAlert;
        private float _sustainedTimer = 0f;
        private List<PreEvacuationRecord> _historicalRecords = new List<PreEvacuationRecord>();

        public event Action<PreEvacuationRecord> OnFirstQualifyingMovement;
        public event Action<PreEvacuationRecord> OnPreEvacuationDelayFinalized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (playerTrackingTarget == null)
            {
                var loc = FindAnyObjectByType<VRHostelLocomotion>();
                if (loc != null) playerTrackingTarget = loc.transform;
                else if (Camera.main != null) playerTrackingTarget = Camera.main.transform;
            }
        }

        private void Update()
        {
            if (!isTrackingActive || !alertHasSounded || firstMovementDetected) return;

            EvaluatePlayerMovement(Time.deltaTime);
        }

        /// <summary>
        /// Begins tracking a new trial for pre-evacuation delay analysis.
        /// </summary>
        public void StartTrialTracking(string participantId, string trialId, int floorId, string conditionId,
            string smoke, string crowd, string alert, MeasurementSource source = MeasurementSource.HumanParticipant_VR)
        {
            _currentRecord = new PreEvacuationRecord
            {
                participantId = participantId,
                trialId = trialId,
                floorId = floorId,
                conditionId = conditionId,
                smokeCondition = smoke,
                crowdCondition = crowd,
                alertType = alert,
                measurementSource = source,
                wallClockUtc = DateTime.UtcNow.ToString("o")
            };

            alertHasSounded = false;
            firstMovementDetected = false;
            _sustainedTimer = 0f;
            isTrackingActive = true;
        }

        /// <summary>
        /// Records the authoritative timestamp when the emergency alert (voice, strobe, siren) begins.
        /// </summary>
        public void RecordAlertOnset(double monotonicSeconds = -1.0)
        {
            if (!isTrackingActive || _currentRecord == null) return;

            double t = (monotonicSeconds >= 0.0) ? monotonicSeconds : Time.timeAsDouble;
            _currentRecord.alertOnsetTimestamp = t;
            alertHasSounded = true;
            firstMovementDetected = false;
            _sustainedTimer = 0f;

            if (playerTrackingTarget != null)
            {
                _positionAtAlert = playerTrackingTarget.position;
            }
            else
            {
                _positionAtAlert = Vector3.zero;
            }

            Debug.Log($"[PreEvacuation] Emergency alert issued at t={t:F3}s.");
        }

        private void EvaluatePlayerMovement(float dt)
        {
            if (playerTrackingTarget == null) return;

            Vector3 currentPos = playerTrackingTarget.position;
            Vector2 alertPos2D = new Vector2(_positionAtAlert.x, _positionAtAlert.z);
            Vector2 currentPos2D = new Vector2(currentPos.x, currentPos.z);

            float displacement = Vector2.Distance(alertPos2D, currentPos2D);

            if (displacement >= movementDistanceThreshold)
            {
                _sustainedTimer += dt;
                if (_sustainedTimer >= sustainedMovementDuration)
                {
                    ConfirmFirstQualifyingMovement(Time.timeAsDouble);
                }
            }
            else
            {
                _sustainedTimer = Mathf.Max(0f, _sustainedTimer - dt * 0.5f);
            }
        }

        /// <summary>
        /// Authoritatively confirms the first qualifying evacuation movement.
        /// Computes T_pre = firstMovement - alertOnset.
        /// </summary>
        public void ConfirmFirstQualifyingMovement(double monotonicSeconds = -1.0)
        {
            if (!isTrackingActive || !alertHasSounded || firstMovementDetected || _currentRecord == null) return;

            double t = (monotonicSeconds >= 0.0) ? monotonicSeconds : Time.timeAsDouble;
            _currentRecord.firstMovementTimestamp = t;
            firstMovementDetected = true;

            if (_currentRecord.alertOnsetTimestamp >= 0.0)
            {
                _currentRecord.preEvacuationDelaySeconds = Math.Round(t - _currentRecord.alertOnsetTimestamp, 3);
                _currentRecord.isCensored = false;
            }
            else
            {
                _currentRecord.isCensored = true;
                _currentRecord.censoringReason = "MovementBeforeAlert";
            }

            Debug.Log($"[PreEvacuation] First qualifying movement confirmed at t={t:F3}s. T_pre = {_currentRecord.preEvacuationDelaySeconds:F3}s.");
            OnFirstQualifyingMovement?.Invoke(_currentRecord);
        }

        public void RecordFirstDoorwayCrossing(double monotonicSeconds = -1.0)
        {
            if (_currentRecord == null) return;
            _currentRecord.firstCrossingTimestamp = (monotonicSeconds >= 0.0) ? monotonicSeconds : Time.timeAsDouble;
        }

        public void RecordSafeZoneArrival(double monotonicSeconds = -1.0)
        {
            if (_currentRecord == null) return;
            _currentRecord.safeZoneArrivalTimestamp = (monotonicSeconds >= 0.0) ? monotonicSeconds : Time.timeAsDouble;
        }

        public PreEvacuationRecord CompleteTrial(bool success = true, bool aborted = false, string abortReason = "")
        {
            if (_currentRecord == null) return null;

            double t = Time.timeAsDouble;
            _currentRecord.trialCompletionTimestamp = t;
            _currentRecord.isCompleted = success;
            _currentRecord.isAborted = aborted;

            if (aborted)
            {
                _currentRecord.censoringReason = string.IsNullOrEmpty(abortReason) ? "TrialAborted" : abortReason;
            }

            if (!firstMovementDetected)
            {
                _currentRecord.isCensored = true;
                if (string.IsNullOrEmpty(_currentRecord.censoringReason))
                {
                    _currentRecord.censoringReason = "TrialEndedNoMovementObserved";
                }
                _currentRecord.preEvacuationDelaySeconds = -1.0;
            }

            if (_currentRecord.alertOnsetTimestamp >= 0.0)
            {
                _currentRecord.totalEvacuationSeconds = Math.Round(t - _currentRecord.alertOnsetTimestamp, 3);
            }

            isTrackingActive = false;
            _historicalRecords.Add(_currentRecord);

            OnPreEvacuationDelayFinalized?.Invoke(_currentRecord);
            return _currentRecord;
        }

        public List<PreEvacuationRecord> GetAllRecords()
        {
            return new List<PreEvacuationRecord>(_historicalRecords);
        }

        /// <summary>
        /// Performs descriptive statistical distribution analysis on valid pre-evacuation delay records.
        /// Computes percentiles (25th, 50th, 75th, 90th), IQR, skewness, histogram bins, and ECDF.
        /// Explicitly documents non-normal skewness characteristic of evacuation decision times.
        /// </summary>
        public static PreEvacuationDistributionResult AnalyzeDistribution(IEnumerable<PreEvacuationRecord> records, int binCount = 10)
        {
            var res = new PreEvacuationDistributionResult();
            if (records == null) return res;

            var validDelays = new List<double>();
            var allRecords = records.ToList();
            res.totalObservations = allRecords.Count;

            foreach (var r in allRecords)
            {
                if (r.isExcluded)
                {
                    res.excludedObservations++;
                }
                else if (r.isCensored || r.preEvacuationDelaySeconds < 0.0)
                {
                    res.censoredObservations++;
                }
                else
                {
                    validDelays.Add(r.preEvacuationDelaySeconds);
                }
            }

            res.validObservations = validDelays.Count;
            if (validDelays.Count == 0) return res;

            validDelays.Sort();

            res.min = validDelays[0];
            res.max = validDelays[validDelays.Count - 1];

            // Mean
            double sum = validDelays.Sum();
            res.mean = Math.Round(sum / validDelays.Count, 3);

            // Variance & Standard Deviation
            double sumSqDiff = validDelays.Sum(d => (d - res.mean) * (d - res.mean));
            res.variance = (validDelays.Count > 1) ? Math.Round(sumSqDiff / (validDelays.Count - 1), 4) : 0.0;
            res.standardDeviation = Math.Round(Math.Sqrt(res.variance), 3);

            // Percentiles via linear rank interpolation
            res.percentile25 = Math.Round(GetPercentile(validDelays, 0.25), 3);
            res.percentile50 = Math.Round(GetPercentile(validDelays, 0.50), 3);
            res.median = res.percentile50;
            res.percentile75 = Math.Round(GetPercentile(validDelays, 0.75), 3);
            res.percentile90 = Math.Round(GetPercentile(validDelays, 0.90), 3);
            res.interquartileRange = Math.Round(res.percentile75 - res.percentile25, 3);

            // Skewness (Fisher-Pearson coefficient of skewness)
            if (validDelays.Count >= 3 && res.standardDeviation > 0.0001)
            {
                int n = validDelays.Count;
                double m3 = validDelays.Sum(d => Math.Pow(d - res.mean, 3)) / n;
                double s3 = Math.Pow(res.standardDeviation, 3);
                res.skewness = Math.Round((n * Math.Sqrt(n - 1) / (n - 2)) * (m3 / s3), 3);
            }

            // Empirical Cumulative Distribution Function (ECDF)
            for (int i = 0; i < validDelays.Count; i++)
            {
                res.ecdfPoints.Add(new EcdfPoint
                {
                    delaySeconds = validDelays[i],
                    cumulativeProbability = Math.Round((i + 1.0) / validDelays.Count, 4)
                });
            }

            // Histogram Bins
            double binWidth = Math.Max(0.5, (res.max - res.min) / binCount);
            for (int b = 0; b < binCount; b++)
            {
                double low = res.min + b * binWidth;
                double high = (b == binCount - 1) ? res.max + 0.001 : low + binWidth;
                int count = validDelays.Count(d => d >= low && d < high);

                res.histogramBins.Add(new HistogramBin
                {
                    lowerBound = Math.Round(low, 2),
                    upperBound = Math.Round(high, 2),
                    count = count,
                    relativeFrequency = Math.Round((double)count / validDelays.Count, 4)
                });
            }

            // Subgroup comparisons: by alertType
            var alertGroups = allRecords.Where(r => !r.isExcluded && !r.isCensored && r.preEvacuationDelaySeconds >= 0.0)
                .GroupBy(r => string.IsNullOrEmpty(r.alertType) ? "Unknown" : r.alertType);

            foreach (var g in alertGroups)
            {
                var gDelays = g.Select(r => r.preEvacuationDelaySeconds).OrderBy(v => v).ToList();
                if (gDelays.Count > 0)
                {
                    double gMean = gDelays.Average();
                    double gVar = (gDelays.Count > 1) ? gDelays.Sum(v => (v - gMean) * (v - gMean)) / (gDelays.Count - 1) : 0.0;
                    double gSd = Math.Sqrt(gVar);
                    double gMed = GetPercentile(gDelays, 0.50);
                    double gP25 = GetPercentile(gDelays, 0.25);
                    double gP75 = GetPercentile(gDelays, 0.75);

                    res.subgroupComparisons[$"Alert_{g.Key}"] = new SubgroupStats
                    {
                        subgroupName = $"Alert: {g.Key}",
                        n = gDelays.Count,
                        mean = Math.Round(gMean, 2),
                        median = Math.Round(gMed, 2),
                        standardDeviation = Math.Round(gSd, 2),
                        iqr = Math.Round(gP75 - gP25, 2)
                    };
                }
            }

            return res;
        }

        private static double GetPercentile(List<double> sortedValues, double percentile)
        {
            if (sortedValues == null || sortedValues.Count == 0) return 0.0;
            if (sortedValues.Count == 1) return sortedValues[0];

            double rank = percentile * (sortedValues.Count - 1);
            int lowIdx = (int)Math.Floor(rank);
            int highIdx = (int)Math.Ceiling(rank);
            double frac = rank - lowIdx;

            return sortedValues[lowIdx] + frac * (sortedValues[highIdx] - sortedValues[lowIdx]);
        }
    }
}
