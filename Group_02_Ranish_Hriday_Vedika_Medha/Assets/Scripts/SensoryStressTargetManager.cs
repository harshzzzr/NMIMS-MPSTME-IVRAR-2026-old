using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace IVRAR.Group02.SensoryStress
{
    /// <summary>
    /// Configurable scenario timing, target presentation, low-light settings and sensory cues.
    /// Manages active concurrent targets (up to 10 active at once) across the 40m x 30m arena.
    /// </summary>
    public sealed class SensoryStressTargetManager : MonoBehaviour
    {
        [Header("Lighting")]
        [Range(0.5f, 5f)] public float ambientLux = 1.2f;
        public Light sceneDirectionalLight;
        public Light traineeFlashlight;
        [Min(0f)] public float fogExtinctionPerMeter = 0.025f;
        [Min(0.01f)] public float referenceFlashlightDistance = 5f;
        [Min(0.01f)] public float referenceIlluminanceLux = 3f;

        [Header("Sensory Cues (Unity gain is not calibrated acoustic SPL)")]
        public AudioSource stressAudioSource;
        public AudioClip gunshotClip;
        public AudioClip sirenAlarmClip;
        [Range(0f, 1f)] public float maximumCueGain = 0.25f;
        public Light peripheralStrobeLight;
        [Range(0, 6)] public int strobePulseCount = 2;
        [Range(1, 6)] public float strobeFrequencyHz = 6f;
        [Range(0f, 1f)] public float maximumStrobeIntensity = 0.2f;

        [Header("Target Spawning")]
        public GameObject hostileTargetPrefab;
        public GameObject civilianTargetPrefab;
        public Transform[] spawnAnchors;
        [Min(0.1f)] public float exposureDurationSeconds = 7.0f;
        [Min(0.1f)] public float meanInterTargetIntervalSeconds = 1.0f;
        [Range(0f, 1f)] public float hostileProbability = 0.80f;
        public int maxConcurrentTargets = 10;
        public int randomSeed = 2047;
        public ReactionLatencyTelemetryLogger telemetryLogger;

        private bool isScenarioActive;
        private System.Random random;
        private Coroutine scenarioCoroutine;
        private bool strobeRunning;
        private float originalFogDensity;
        private bool originalFogEnabled;
        private float originalFlashlightIntensity = 2.5f;
        private float originalStrobeIntensity;
        private float originalDirectionalIntensity;
        private float originalAmbientIntensity;
        private int nextTrialIndex;

        private readonly List<GameObject> activeTargetInstances = new List<GameObject>();
        private readonly HashSet<int> occupiedAnchorIndices = new HashSet<int>();

        public int ActiveTargetCount => activeTargetInstances.Count;
        public IReadOnlyList<GameObject> ActiveTargets => activeTargetInstances;

        private void Awake()
        {
            random = new System.Random(randomSeed);
            if (telemetryLogger == null) telemetryLogger = FindObjectOfType<ReactionLatencyTelemetryLogger>();
            originalFogDensity = RenderSettings.fogDensity;
            originalFogEnabled = RenderSettings.fog;
            originalAmbientIntensity = RenderSettings.ambientIntensity;
            if (sceneDirectionalLight != null) originalDirectionalIntensity = sceneDirectionalLight.intensity;
            if (traineeFlashlight != null)
            {
                originalFlashlightIntensity = traineeFlashlight.intensity > 0.5f ? traineeFlashlight.intensity : 2.5f;
                traineeFlashlight.intensity = originalFlashlightIntensity;
                traineeFlashlight.range = 45f;
                traineeFlashlight.spotAngle = 60f;
            }
            if (peripheralStrobeLight != null) { originalStrobeIntensity = peripheralStrobeLight.intensity; peripheralStrobeLight.enabled = false; }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientIntensity = Mathf.Clamp(ambientLux / 5f, 0.1f, 1f);
        }

        public void ApplySensoryStressorEnvelope(float intensityFactor)
        {
            ApplySensoryStressorEnvelope(intensityFactor, transform.position);
        }

        public void ApplySensoryStressorEnvelope(float intensityFactor, Vector3 illuminatedPoint)
        {
            float intensity = Mathf.Clamp01(intensityFactor);
            RenderSettings.fog = true;
            RenderSettings.fogDensity = Mathf.Clamp(fogExtinctionPerMeter * intensity, 0f, 0.05f);
            if (sceneDirectionalLight != null) sceneDirectionalLight.intensity = Mathf.Lerp(0.08f, 0.25f, intensity);

            if (traineeFlashlight != null)
            {
                float distance = Vector3.Distance(traineeFlashlight.transform.position, illuminatedPoint);
                Vector3 toTarget = distance > 0.001f ? (illuminatedPoint - traineeFlashlight.transform.position).normalized : traineeFlashlight.transform.forward;
                float cosTheta = Mathf.Max(0f, Vector3.Dot(traineeFlashlight.transform.forward, toTarget));
                float r = Mathf.Max(0.25f, distance);
                float illuminance = (referenceIlluminanceLux * Mathf.Pow(referenceFlashlightDistance, 2f) * cosTheta / (r * r)) *
                                     Mathf.Exp(-fogExtinctionPerMeter * r);
                traineeFlashlight.intensity = Mathf.Clamp(illuminance, 1.5f, originalFlashlightIntensity * 2f);
            }
            if (stressAudioSource != null && !stressAudioSource.isPlaying && random.NextDouble() < 0.20 * intensity)
            {
                AudioClip clip = random.NextDouble() < 0.5 ? gunshotClip : sirenAlarmClip;
                if (clip != null)
                {
                    stressAudioSource.spatialBlend = 1f;
                    stressAudioSource.volume = Mathf.Min(maximumCueGain, intensity * maximumCueGain);
                    stressAudioSource.pitch = Mathf.Lerp(0.95f, 1.05f, (float)random.NextDouble());
                    stressAudioSource.PlayOneShot(clip);
                }
            }
            if (peripheralStrobeLight != null && strobePulseCount > 0 && intensity > 0f && !strobeRunning)
                StartCoroutine(PeripheralStrobe(intensity));
        }

        private IEnumerator PeripheralStrobe(float intensity)
        {
            strobeRunning = true;
            int pulses = random.Next(1, Mathf.Min(strobePulseCount, 4) + 1);
            float halfPeriod = 0.5f / Mathf.Clamp(strobeFrequencyHz, 1f, 6f);
            yield return new WaitForSeconds((float)random.NextDouble() * halfPeriod);
            for (int i = 0; i < pulses; i++)
            {
                peripheralStrobeLight.intensity = Mathf.Min(maximumStrobeIntensity * intensity, 0.15f);
                peripheralStrobeLight.enabled = true;
                yield return new WaitForSeconds(halfPeriod);
                peripheralStrobeLight.enabled = false;
                yield return new WaitForSeconds(halfPeriod);
            }
            strobeRunning = false;
        }

        public void StartMultiTargetSession(int concurrentCount)
        {
            isScenarioActive = true;
            maxConcurrentTargets = Mathf.Clamp(concurrentCount, 1, spawnAnchors != null ? spawnAnchors.Length : 10);
            ClearAllTargets();

            // Populate arena with initial wave up to maxConcurrentTargets
            for (int i = 0; i < maxConcurrentTargets; i++)
            {
                // Ensure at least 1-2 civilians in the initial batch
                bool forceCiv = (i == 2 || i == 7);
                SpawnNextAvailableTarget(forceCiv ? false : (bool?)null);
            }
        }

        public GameObject SpawnNextAvailableTarget(bool? forceHostile = null)
        {
            if (spawnAnchors == null || spawnAnchors.Length == 0) return null;
            if (random == null) random = new System.Random(randomSeed);

            // Find all unoccupied anchor indices
            List<int> freeIndices = new List<int>();
            for (int i = 0; i < spawnAnchors.Length; i++)
            {
                if (!occupiedAnchorIndices.Contains(i) && spawnAnchors[i] != null)
                    freeIndices.Add(i);
            }

            if (freeIndices.Count == 0) return null; // all anchors occupied

            int chosenIndex = freeIndices[random.Next(freeIndices.Count)];
            Transform anchor = spawnAnchors[chosenIndex];

            bool hostile = forceHostile.HasValue ? forceHostile.Value : (random.NextDouble() < hostileProbability);
            GameObject prefab = hostile ? hostileTargetPrefab : civilianTargetPrefab;
            if (prefab == null) return null;

            GameObject target = Instantiate(prefab, anchor.position, anchor.rotation);
            string trialId = "trial-" + (++nextTrialIndex).ToString("D4");
            float distance = Vector3.Distance(transform.position, anchor.position);
            Vector3 relative = anchor.position - transform.position;
            float angle = Vector3.SignedAngle(transform.forward, relative, Vector3.up);

            if (telemetryLogger != null)
                telemetryLogger.RegisterTargetPresentation(trialId, hostile, distance, angle);

            TargetTrialMetadata metadata = target.GetComponent<TargetTrialMetadata>();
            if (metadata == null) metadata = target.AddComponent<TargetTrialMetadata>();
            metadata.Initialize(trialId, hostile, distance, angle, telemetryLogger, Time.realtimeSinceStartupAsDouble);
            metadata.AnchorIndex = chosenIndex;
            metadata.StartExposureTimeout(exposureDurationSeconds);

            // Overhead stylized arcade badge
            GameObject badgeObj = new GameObject("Arcade_Badge");
            badgeObj.transform.SetParent(target.transform, false);
            badgeObj.transform.localPosition = new Vector3(0f, 1.25f, 0f);
            if (Camera.main != null)
                badgeObj.transform.rotation = Camera.main.transform.rotation;
            else
                badgeObj.transform.localRotation = Quaternion.identity;

            TextMesh tmText = badgeObj.AddComponent<TextMesh>();
            tmText.text = hostile ? "[ HOSTILE ]" : "[ CIVILIAN ]";
            tmText.characterSize = 0.05f;
            tmText.fontSize = 48;
            tmText.anchor = TextAnchor.MiddleCenter;
            tmText.alignment = TextAlignment.Center;
            tmText.color = hostile ? new Color(1f, 0.2f, 0.2f) : new Color(0.2f, 0.8f, 1f);

            occupiedAnchorIndices.Add(chosenIndex);
            activeTargetInstances.Add(target);

            return target;
        }

        public void SpawnRandomizedTarget(int trialIndex)
        {
            SpawnNextAvailableTarget();
        }

        public void OnTargetRemoved(int anchorIndex, GameObject target)
        {
            if (anchorIndex >= 0)
                occupiedAnchorIndices.Remove(anchorIndex);

            activeTargetInstances.Remove(target);

            // If scenario is active and active count dropped below max, schedule replacement
            if (isScenarioActive && activeTargetInstances.Count < maxConcurrentTargets)
            {
                StartCoroutine(SpawnReplacementDelayed(0.4f));
            }
        }

        private IEnumerator SpawnReplacementDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (isScenarioActive && activeTargetInstances.Count < maxConcurrentTargets)
            {
                SpawnNextAvailableTarget();
            }
        }

        public void ClearAllTargets()
        {
            var existing = FindObjectsOfType<TargetTrialMetadata>();
            foreach (var t in existing)
            {
                if (t != null) Destroy(t.gameObject);
            }
            activeTargetInstances.Clear();
            occupiedAnchorIndices.Clear();
        }

        public void StartTrialSequence()
        {
            StartMultiTargetSession(maxConcurrentTargets);
        }

        public void TerminateTrialSequence()
        {
            isScenarioActive = false;
            if (scenarioCoroutine != null) StopCoroutine(scenarioCoroutine);
            StopAllCoroutines();
            ClearAllTargets();
            strobeRunning = false;
            if (peripheralStrobeLight != null) { peripheralStrobeLight.enabled = false; peripheralStrobeLight.intensity = originalStrobeIntensity; }
            RenderSettings.fogDensity = originalFogDensity;
            RenderSettings.fog = originalFogEnabled;
            RenderSettings.ambientIntensity = originalAmbientIntensity;
            if (traineeFlashlight != null) traineeFlashlight.intensity = originalFlashlightIntensity;
            if (sceneDirectionalLight != null) sceneDirectionalLight.intensity = originalDirectionalIntensity;
            if (telemetryLogger != null) telemetryLogger.ExportTelemetryToDisk();
        }
    }
}
