using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace IVRAR.Group02.SensoryStress
{
    /// <summary>Trial event and head-motion proxy logger. Workload fields are exploratory proxies, not clinical measures.</summary>
    public sealed class ReactionLatencyTelemetryLogger : MonoBehaviour
    {
        [Header("Telemetry Configuration")]
        public string exportFileName = "reaction_latency_trial_log.csv";
        public string assessmentFileName = "post_trial_assessments.csv";
        [Tooltip("Pseudonymous code only; never use a direct identifier.")]
        public string participantCode = "PENDING_CODE";
        public string sessionId = "PENDING_SESSION";
        public string conditionLabel = "PENDING_CONDITION";
        [Tooltip("Keep unverified until an approved genuine participant session has been collected.")]
        public string dataStatus = "unverified";
        [Tooltip("Optional XR camera/head transform. Camera.main is used when empty.")]
        public Transform headTransform;
        [Range(60, 120)] public int workloadSampleRateHz = 90;
        [Min(1)] public int workloadWindowSamples = 90;

        private readonly StringBuilder csvBuffer = new StringBuilder(8192);
        private readonly StringBuilder assessmentBuffer = new StringBuilder(2048);
        private readonly float[] yawDeltas = new float[120];
        private int sampleCount;
        private int sampleCursor;
        private float sampleAccumulator;
        private Quaternion previousHeadRotation;
        private bool hasPreviousHeadRotation;
        private float currentYawVariance;
        private string currentTrialId = "";
        private float currentTargetDistance;
        private float currentPresentationAngle;

        private int totalHits, totalMisses, totalFalseAlarms, totalCorrectRejections;

        private void Awake()
        {
            csvBuffer.AppendLine("TimestampUtc,ParticipantID,SessionID,Condition,DataStatus,TrialID,TargetType,TraineeAction,LatencyMs,Hit,Miss,FalseAlarm,CorrectRejection,DistanceMeters,PresentationAngleDeg,HeadYawJitterVarianceDeg2,DPrime,BetaCriterion");
            assessmentBuffer.AppendLine("TimestampUtc,ParticipantID,SessionID,TrialID,NasaTlxMental,NasaTlxPhysical,NasaTlxTemporal,NasaTlxPerformance,NasaTlxEffort,NasaTlxFrustration,SagatRecallCorrect,SagatRecallTotal");
            if (headTransform == null && Camera.main != null) headTransform = Camera.main.transform;
            if (headTransform != null) { previousHeadRotation = headTransform.rotation; hasPreviousHeadRotation = true; }
        }

        private void Update()
        {
            if (headTransform == null) return;
            float interval = 1f / Mathf.Clamp(workloadSampleRateHz, 60, 120);
            sampleAccumulator += Time.unscaledDeltaTime;
            while (sampleAccumulator >= interval)
            {
                sampleAccumulator -= interval;
                Quaternion rotation = headTransform.rotation;
                if (hasPreviousHeadRotation)
                {
                    yawDeltas[sampleCursor] = Mathf.DeltaAngle(previousHeadRotation.eulerAngles.y, rotation.eulerAngles.y);
                    sampleCursor = (sampleCursor + 1) % Mathf.Min(workloadWindowSamples, yawDeltas.Length);
                    if (sampleCount < Mathf.Min(workloadWindowSamples, yawDeltas.Length)) sampleCount++;
                    currentYawVariance = CalculateYawVariance();
                }
                previousHeadRotation = rotation;
                hasPreviousHeadRotation = true;
            }
        }

        private float CalculateYawVariance()
        {
            if (sampleCount < 2) return 0f;
            float mean = 0f;
            for (int i = 0; i < sampleCount; i++) mean += yawDeltas[i];
            mean /= sampleCount;
            float sum = 0f;
            for (int i = 0; i < sampleCount; i++) { float d = yawDeltas[i] - mean; sum += d * d; }
            return sum / (sampleCount - 1);
        }

        public void RegisterTargetPresentation(string trialId, bool isHostile, float distanceMeters, float presentationAngleDeg)
        {
            currentTrialId = trialId ?? "";
            currentTargetDistance = distanceMeters;
            currentPresentationAngle = presentationAngleDeg;
        }

        public (double dPrime, double betaCriterion) ComputeSignalDetectionMetrics()
        {
            int signalN = totalHits + totalMisses;
            int noiseN = totalFalseAlarms + totalCorrectRejections;
            if (signalN == 0 || noiseN == 0) return (double.NaN, double.NaN);

            // Log-linear correction (Hautus): add 0.5 to each cell and 1 to each class total.
            double hitRate = (totalHits + 0.5) / (signalN + 1.0);
            double falseAlarmRate = (totalFalseAlarms + 0.5) / (noiseN + 1.0);
            double zHit = InverseStandardNormal(hitRate);
            double zFa = InverseStandardNormal(falseAlarmRate);
            double dPrime = zHit - zFa;
            double criterion = -0.5 * (zHit + zFa);
            return (dPrime, Math.Exp(-dPrime * criterion));
        }

        // Acklam rational approximation; input is kept strictly inside (0,1).
        private static double InverseStandardNormal(double p)
        {
            p = Math.Max(1e-12, Math.Min(1.0 - 1e-12, p));
            double[] a = { -39.6968302866538, 220.946098424521, -275.928510446969, 138.357751867269, -30.6647980661472, 2.50662827745924 };
            double[] b = { -54.4760987982241, 161.585836858041, -155.698979859887, 66.8013118877197, -13.2806815528857 };
            double[] c = { -0.00778489400243029, -0.322396458041136, -2.40075827716184, -2.54973253934373, 4.37466414146497, 2.93816398269878 };
            double[] d = { 0.00778469570904146, 0.32246712907004, 2.445134137143, 3.75440866190742 };
            if (p < 0.02425)
            {
                double q = Math.Sqrt(-2.0 * Math.Log(p));
                return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                       ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1.0);
            }
            if (p > 1.0 - 0.02425)
            {
                double q = Math.Sqrt(-2.0 * Math.Log(1.0 - p));
                return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                        ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1.0);
            }
            double r = p - 0.5; double s = r * r;
            return (((((a[0] * s + a[1]) * s + a[2]) * s + a[3]) * s + a[4]) * s + a[5]) * r /
                   (((((b[0] * s + b[1]) * s + b[2]) * s + b[3]) * s + b[4]) * s + 1.0);
        }

        public void RecordEngagementEvent(string trialId, bool isHostile, bool traineeEngaged, float latencyMs)
        {
            RecordEngagementEvent(trialId, isHostile, traineeEngaged, latencyMs, currentTargetDistance, currentPresentationAngle);
        }

        public void RecordEngagementEvent(string trialId, bool isHostile, bool traineeEngaged, float latencyMs, float distanceMeters, float presentationAngleDeg)
        {
            if (isHostile && traineeEngaged) totalHits++;
            else if (isHostile) totalMisses++;
            else if (traineeEngaged) totalFalseAlarms++;
            else totalCorrectRejections++;

            var (dPrime, beta) = ComputeSignalDetectionMetrics();
            bool hit = isHostile && traineeEngaged, miss = isHostile && !traineeEngaged;
            bool falseAlarm = !isHostile && traineeEngaged, correctRejection = !isHostile && !traineeEngaged;
            string safeId = Csv(trialId ?? currentTrialId);
            csvBuffer.Append(DateTime.UtcNow.ToString("O")).Append(',').Append(Csv(participantCode)).Append(',')
                .Append(Csv(sessionId)).Append(',').Append(Csv(conditionLabel)).Append(',').Append(Csv(dataStatus)).Append(',')
                .Append(safeId).Append(',')
                .Append(isHostile ? "Hostile" : "Civilian").Append(',').Append(traineeEngaged ? "Engaged" : "Ignored").Append(',')
                .Append(latencyMs.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(hit ? 1 : 0).Append(',').Append(miss ? 1 : 0).Append(',').Append(falseAlarm ? 1 : 0).Append(',').Append(correctRejection ? 1 : 0).Append(',')
                .Append(distanceMeters.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(presentationAngleDeg.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(currentYawVariance.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(double.IsNaN(dPrime) ? "" : dPrime.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).Append(',')
                .Append(double.IsNaN(beta) ? "" : beta.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)).AppendLine();
        }

        public void RecordAssessment(string participantCode, string sessionId, string trialId, float[] nasaTlxSubscales, int sagatCorrect, int sagatTotal)
        {
            assessmentBuffer.Append(DateTime.UtcNow.ToString("O")).Append(',').Append(Csv(participantCode)).Append(',')
                .Append(Csv(sessionId)).Append(',').Append(Csv(trialId));
            for (int i = 0; i < 6; i++)
                assessmentBuffer.Append(',').Append(nasaTlxSubscales != null && nasaTlxSubscales.Length > i && !float.IsNaN(nasaTlxSubscales[i])
                    ? nasaTlxSubscales[i].ToString("F1", System.Globalization.CultureInfo.InvariantCulture) : "");
            assessmentBuffer.Append(',')
                .Append(sagatCorrect).Append(',').Append(sagatTotal).AppendLine();
        }

        public void RecordAssessment(string participantCode, string sessionId, string trialId, float nasaTlxMental, float nasaTlxTemporal, int sagatCorrect, int sagatTotal)
        {
            RecordAssessment(participantCode, sessionId, trialId,
                new[] { nasaTlxMental, float.NaN, nasaTlxTemporal, float.NaN, float.NaN, float.NaN }, sagatCorrect, sagatTotal);
        }

        private static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";

        public void ExportTelemetryToDisk()
        {
            string outPath = Path.Combine(Application.dataPath, "..", "telemetry", exportFileName);
            string directory = Path.GetDirectoryName(outPath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            string trialPayload = csvBuffer.ToString();
            if (File.Exists(outPath)) trialPayload = RemoveFirstLine(trialPayload);
            File.AppendAllText(outPath, trialPayload);
            string assessmentPath = Path.Combine(directory, assessmentFileName);
            string assessmentPayload = assessmentBuffer.ToString();
            if (File.Exists(assessmentPath)) assessmentPayload = RemoveFirstLine(assessmentPayload);
            File.AppendAllText(assessmentPath, assessmentPayload);
            csvBuffer.Length = 0;
            csvBuffer.AppendLine("TimestampUtc,ParticipantID,SessionID,Condition,DataStatus,TrialID,TargetType,TraineeAction,LatencyMs,Hit,Miss,FalseAlarm,CorrectRejection,DistanceMeters,PresentationAngleDeg,HeadYawJitterVarianceDeg2,DPrime,BetaCriterion");
            assessmentBuffer.Length = 0;
            assessmentBuffer.AppendLine("TimestampUtc,ParticipantID,SessionID,TrialID,NasaTlxMental,NasaTlxPhysical,NasaTlxTemporal,NasaTlxPerformance,NasaTlxEffort,NasaTlxFrustration,SagatRecallCorrect,SagatRecallTotal");
            Debug.Log("Telemetry exported to: " + outPath);
        }

        private static string RemoveFirstLine(string value)
        {
            int end = value.IndexOf('\n');
            return end >= 0 ? value.Substring(end + 1) : "";
        }
    }
}
