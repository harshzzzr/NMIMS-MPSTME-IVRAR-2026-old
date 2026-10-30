using System.Collections;
using UnityEngine;

namespace IVRAR.Group02.SensoryStress
{
    /// <summary>Per-target trial metadata and one-shot engagement/miss reporting.</summary>
    public sealed class TargetTrialMetadata : MonoBehaviour
    {
        public string TrialId { get; private set; }
        public bool IsHostile { get; private set; }
        public float DistanceMeters { get; private set; }
        public float PresentationAngle { get; private set; }
        public int AnchorIndex { get; set; } = -1;
        private ReactionLatencyTelemetryLogger logger;
        private double spawnTime;
        private bool reported;

        public void Initialize(string trialId, bool isHostile, float distanceMeters, float presentationAngle,
            ReactionLatencyTelemetryLogger telemetryLogger, double presentedAt)
        {
            TrialId = trialId;
            IsHostile = isHostile;
            DistanceMeters = distanceMeters;
            PresentationAngle = presentationAngle;
            logger = telemetryLogger;
            spawnTime = presentedAt;
        }

        public void RegisterDecision(bool engaged)
        {
            if (reported) return;
            reported = true;
            float latencyMs = Mathf.Max(0f, (float)((Time.realtimeSinceStartupAsDouble - spawnTime) * 1000.0));
            if (logger != null)
                logger.RecordEngagementEvent(TrialId, IsHostile, engaged, latencyMs, DistanceMeters, PresentationAngle);
            
            var director = FindObjectOfType<Group02SessionDirector>();
            if (director != null)
                director.HandleTargetDecision(this, engaged, latencyMs);

            var targetManager = FindObjectOfType<SensoryStressTargetManager>();
            if (targetManager != null)
                targetManager.OnTargetRemoved(AnchorIndex, gameObject);

            Destroy(gameObject);
        }

        public void StartExposureTimeout(float durationSeconds) => StartCoroutine(Timeout(durationSeconds));

        private IEnumerator Timeout(float durationSeconds)
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, durationSeconds));
            RegisterDecision(false);
        }
    }
}
