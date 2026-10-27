using System;
using System.Collections.Generic;
using UnityEngine;

namespace C067.Coordination
{
    public enum EmergencyAlertType
    {
        VisualStrobeOnly = 0,
        VoiceAlarmOnly = 1,
        VisualStrobeAndVoice = 2,
        SirenOnly = 3,
        SilentControl = 4
    }

    public enum SmokeCondition
    {
        None = 0,
        Light = 1,
        Moderate = 2,
        Dense = 3
    }

    public enum CrowdDensityCondition
    {
        Sparse = 50,
        Moderate = 150,
        HighDensity = 300
    }

    public enum RouteConfiguration
    {
        BothStairwellsOpen = 0,
        EastStairwellOnly = 1,
        WestStairwellOnly = 2,
        GroundFoyerConstrained = 3
    }

    /// <summary>
    /// Configurable experimental trial specification for the C067 human factors and crowd evacuation framework.
    /// Supports practice trials, counterbalancing, randomized order reproducibility, and independent condition parameters.
    /// </summary>
    [System.Serializable]
    public class TrialConfiguration
    {
        [Header("Trial & Participant Identity")]
        [Tooltip("Unique trial identifier (e.g. TR_001)")]
        public string trialId = "TR_001";

        [Tooltip("Anonymous participant identifier (e.g. P001, P_ANON_12)")]
        public string participantId = "P001";

        [Tooltip("Scenario environment identifier")]
        public string scenarioId = "Hostel_5Story_NBC2016";

        [Tooltip("Configuration variation identifier")]
        public string configurationId = "CFG_BASE";

        [Tooltip("Human-readable experimental condition label")]
        public string conditionLabel = "Dense_Smoke_VoiceAlarm";

        [Tooltip("Designation indicating this is an orientation/practice trial excluded from primary analysis")]
        public bool isPractice = false;

        [Header("Physical Conditions & Starting Position")]
        [Tooltip("Hostel story where the participant/agents spawn (0=Ground, 1=Floor1, ..., 4=Floor4)")]
        [Range(0, 4)]
        public int startingFloor = 4;

        [Tooltip("World coordinate starting position")]
        public Vector3 startPosition = new Vector3(0f, 12.025f, 0f);

        [Tooltip("Doorway usable width in meters")]
        public float doorwayWidthMeters = 2.4f;

        [Header("Hazard & Environmental Conditions")]
        [Tooltip("Categorical smoke condition")]
        public SmokeCondition smokeCondition = SmokeCondition.Moderate;

        [Tooltip("Volumetric smoke density rho in [0.0, 1.0]")]
        [Range(0f, 1f)]
        public float smokeDensity = 0.35f;

        [Tooltip("Beer-Lambert optical visibility in meters")]
        public float visibilityMeters = 8.5f;

        [Header("Crowd Simulation Parameters")]
        [Tooltip("Crowd density condition")]
        public CrowdDensityCondition crowdDensity = CrowdDensityCondition.HighDensity;

        [Tooltip("Exact number of autonomous crowd agents to instantiate")]
        public int agentCount = 300;

        [Tooltip("Available egress route and doorway configuration")]
        public RouteConfiguration routeConfiguration = RouteConfiguration.BothStairwellsOpen;

        [Header("Emergency Alarm Protocol")]
        [Tooltip("Emergency alarm alert modality")]
        public EmergencyAlertType alertType = EmergencyAlertType.VisualStrobeAndVoice;

        [Tooltip("Delay in seconds from trial start until the emergency alert is issued")]
        public float alertOnsetDelaySeconds = 5.0f;

        [Tooltip("Maximum allowed trial duration in seconds before automatic timeout/censoring")]
        public float maxTrialDurationSeconds = 300.0f;

        [Header("Experimental Design & Reproducibility")]
        [Tooltip("Pseudorandom number generator seed used to generate this trial")]
        public int randomSeed = 42;

        [Tooltip("Assigned presentation order in the experimental sequence (1-indexed)")]
        public int assignedOrderIndex = 1;

        public TrialConfiguration Clone()
        {
            return (TrialConfiguration)this.MemberwiseClone();
        }

        /// <summary>
        /// Generates a standardized evaluation suite of 50 simulated trials (default specification).
        /// Balances smoke density, alarm modalities, crowd density, and starting floors across the 50 trials.
        /// </summary>
        public static List<TrialConfiguration> GenerateStandard50TrialSuite(string participantId = "P001", int seed = 42, bool includePractice = true, int count = 50)
        {
            return GenerateDefaultSuite(count, seed, participantId, includePractice);
        }

        public static List<TrialConfiguration> GenerateDefaultSuite(int count = 50, int seed = 42, string participantId = "P001", bool includePractice = true)
        {
            var list = new List<TrialConfiguration>();
            var rng = new System.Random(seed);

            int startIndex = 1;
            if (includePractice)
            {
                var practice = new TrialConfiguration
                {
                    trialId = $"TR_PRAC_{participantId}_01",
                    participantId = participantId,
                    scenarioId = "Hostel_5Story_NBC2016",
                    configurationId = "CFG_PRACTICE",
                    conditionLabel = "Practice_Orientation_Clear",
                    isPractice = true,
                    startingFloor = 4,
                    startPosition = new Vector3(0f, 12.025f, 0f),
                    smokeCondition = SmokeCondition.None,
                    smokeDensity = 0.0f,
                    visibilityMeters = 25.0f,
                    crowdDensity = CrowdDensityCondition.Sparse,
                    agentCount = 50,
                    routeConfiguration = RouteConfiguration.BothStairwellsOpen,
                    alertType = EmergencyAlertType.VisualStrobeAndVoice,
                    alertOnsetDelaySeconds = 3.0f,
                    maxTrialDurationSeconds = 180.0f,
                    randomSeed = seed,
                    assignedOrderIndex = 0
                };
                list.Add(practice);
            }

            // Factorial combination matrix:
            // Alarm Modalities: Voice, Strobe, Strobe+Voice
            // Smoke: None, Light, Moderate, Dense
            // Crowd: Sparse, Moderate, High
            // Starting floors: 4, 3, 2, 1
            var alerts = new EmergencyAlertType[]
            {
                EmergencyAlertType.VoiceAlarmOnly,
                EmergencyAlertType.VisualStrobeOnly,
                EmergencyAlertType.VisualStrobeAndVoice
            };

            var smokes = new (SmokeCondition cond, float density, float vis)[]
            {
                (SmokeCondition.None, 0.0f, 25.0f),
                (SmokeCondition.Light, 0.15f, 15.0f),
                (SmokeCondition.Moderate, 0.35f, 8.5f),
                (SmokeCondition.Dense, 0.65f, 3.5f)
            };

            var crowds = new (CrowdDensityCondition cond, int agents)[]
            {
                (CrowdDensityCondition.Sparse, 60),
                (CrowdDensityCondition.Moderate, 150),
                (CrowdDensityCondition.HighDensity, 300)
            };

            var floors = new int[] { 4, 3, 2, 1 };

            for (int i = 0; i < count; i++)
            {
                int alertIdx = i % alerts.Length;
                int smokeIdx = (i / alerts.Length) % smokes.Length;
                int crowdIdx = (i / (alerts.Length * smokes.Length)) % crowds.Length;
                int floorIdx = floors[i % floors.Length];

                var alert = alerts[alertIdx];
                var smoke = smokes[smokeIdx];
                var crowd = crowds[crowdIdx];

                float onsetDelay = 3.0f + (float)(rng.NextDouble() * 5.0); // 3.0s to 8.0s jitter

                var trial = new TrialConfiguration
                {
                    trialId = $"TR_{participantId}_{startIndex:D3}",
                    participantId = participantId,
                    scenarioId = "Hostel_5Story_NBC2016",
                    configurationId = $"CFG_F{floorIdx}_{smoke.cond}_{alert}",
                    conditionLabel = $"F{floorIdx}_{smoke.cond}_{crowd.cond}_{alert}",
                    isPractice = false,
                    startingFloor = floorIdx,
                    startPosition = new Vector3(0f, VRHostelLocomotion.FloorElevations[floorIdx], 0f),
                    smokeCondition = smoke.cond,
                    smokeDensity = smoke.density,
                    visibilityMeters = smoke.vis,
                    crowdDensity = crowd.cond,
                    agentCount = crowd.agents,
                    routeConfiguration = (i % 5 == 4) ? RouteConfiguration.EastStairwellOnly : RouteConfiguration.BothStairwellsOpen,
                    alertType = alert,
                    alertOnsetDelaySeconds = (float)Math.Round(onsetDelay, 2),
                    maxTrialDurationSeconds = 300.0f,
                    randomSeed = seed + i,
                    assignedOrderIndex = startIndex++
                };

                list.Add(trial);
            }

            return list;
        }
    }
}
