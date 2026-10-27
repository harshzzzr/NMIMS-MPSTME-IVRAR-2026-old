using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace C067.Coordination
{
    public enum TrialLifecycleState
    {
        NotStarted = 0,
        Ready = 1,
        Running = 2,
        Assessment = 3,
        Completed = 4,
        Aborted = 5,
        Excluded = 6
    }

    /// <summary>
    /// Comprehensive, immutable record of an experimental evacuation trial's execution,
    /// timestamps, pre-evacuation delay, physical flow performance, and subjective workload/sickness assessments.
    /// Strictly maintains invariant culture formatting, RFC 4180 CSV compliance, and missing data conventions.
    /// </summary>
    [System.Serializable]
    public class TrialResult
    {
        [Header("Trial & Participant Identity")]
        public string trialId = "";
        public string participantId = "";
        public string scenarioId = "";
        public string configurationId = "";
        public string conditionLabel = "";
        public bool isPractice = false;
        public int assignedOrderIndex = 1;

        [Header("Lifecycle State & Status")]
        public TrialLifecycleState state = TrialLifecycleState.NotStarted;
        public bool isCensored = false;
        public string censoringReason = "";
        public bool isExcluded = false;
        public string exclusionReason = "";
        public bool adverseEvent = false;
        public string adverseEventNotes = "";
        public string notes = "";

        [Header("Physical Conditions")]
        public int startingFloor = 4;
        public string alertType = "VisualStrobeAndVoice";
        public string smokeCondition = "Moderate";
        public float smokeDensity = 0.35f;
        public float visibilityMeters = 8.5f;
        public int crowdDensity = 300;
        public string routeConfiguration = "BothStairwellsOpen";

        [Header("Simulation Monotonic Timestamps (Seconds)")]
        public double trialStartTime = -1.0;
        public double alertOnsetTimestamp = -1.0;
        public double evacuationStartTime = -1.0; // First qualifying movement
        public double firstDoorwayCrossingTime = -1.0;
        public double safeZoneArrivalTime = -1.0;
        public double trialCompletionTime = -1.0;

        [Header("Wall-Clock Timestamps (ISO 8601 UTC)")]
        public string wallClockStartUtc = "";
        public string wallClockEndUtc = "";

        [Header("Operational Durations (Seconds)")]
        [Tooltip("Pre-evacuation delay: First qualifying movement time minus alert onset time")]
        public double preEvacuationDelay = -1.0;
        public double movementDuration = -1.0;
        public double totalEvacuationDuration = -1.0;

        [Header("Evacuation Telemetry & Bottleneck Performance")]
        public int activeAgentsAtStart = 0;
        public int evacuatedAgentCount = 0;
        public int totalPedestrianCrossings = 0;
        public float meanDischargeRate = -1.0f;
        public float peakDischargeRate = -1.0f;
        public float doorwayJamDurationTotal = 0.0f;

        [Header("Human Factors Subjective Assessments")]
        public float rawNasaTlxScore = -1.0f;
        public float weightedNasaTlxScore = -1.0f;
        public float ssqTotalScore = -1.0f;
        public float ssqNauseaScore = -1.0f;
        public float ssqOculomotorScore = -1.0f;
        public float ssqDisorientationScore = -1.0f;

        /// <summary>
        /// Calculates final elapsed durations from recorded monotonic timestamps.
        /// Handles censoring correctly if movement or safe-zone arrival was not observed before trial termination.
        /// </summary>
        public void FinalizeDurations(double maxTrialDuration)
        {
            if (trialStartTime >= 0.0 && trialCompletionTime >= trialStartTime)
            {
                totalEvacuationDuration = Math.Round(trialCompletionTime - trialStartTime, 3);
            }

            if (alertOnsetTimestamp >= 0.0 && evacuationStartTime >= alertOnsetTimestamp)
            {
                preEvacuationDelay = Math.Round(evacuationStartTime - alertOnsetTimestamp, 3);
                isCensored = false;
            }
            else
            {
                // Observation is censored: participant did not begin movement before trial termination
                isCensored = true;
                if (string.IsNullOrEmpty(censoringReason))
                {
                    censoringReason = (state == TrialLifecycleState.Aborted)
                        ? "AbortedBeforeMovement"
                        : "TrialTimeoutNoMovementObserved";
                }
                preEvacuationDelay = -1.0; // Documented unobserved indicator
            }

            if (evacuationStartTime >= 0.0 && safeZoneArrivalTime >= evacuationStartTime)
            {
                movementDuration = Math.Round(safeZoneArrivalTime - evacuationStartTime, 3);
            }
            else
            {
                movementDuration = -1.0;
            }
        }

        public static string GetCsvHeader()
        {
            return "TrialId,ParticipantId,ScenarioId,ConfigurationId,ConditionLabel,IsPractice,AssignedOrder," +
                   "State,IsCensored,CensoringReason,IsExcluded,ExclusionReason,AdverseEvent,AdverseEventNotes,Notes," +
                   "StartingFloor,AlertType,SmokeCondition,SmokeDensity,VisibilityMeters,CrowdDensity,RouteConfiguration," +
                   "TrialStartTimeSec,AlertOnsetSec,FirstMovementSec,FirstCrossingSec,SafeZoneArrivalSec,CompletionSec," +
                   "WallClockStartUtc,WallClockEndUtc,PreEvacuationDelaySec,MovementDurationSec,TotalEvacuationDurationSec," +
                   "ActiveAgentsStart,EvacuatedAgents,TotalCrossings,MeanDischargeRate_AgentsPerSec,PeakDischargeRate_AgentsPerSec,JamDurationSec," +
                   "RawNasaTlxScore,WeightedNasaTlxScore,SsqTotalScore,SsqNausea,SsqOculomotor,SsqDisorientation";
        }

        public string ToCsvRow()
        {
            var sb = new StringBuilder(512);

            AppendField(sb, trialId);
            AppendField(sb, participantId);
            AppendField(sb, scenarioId);
            AppendField(sb, configurationId);
            AppendField(sb, conditionLabel);
            AppendField(sb, isPractice ? "true" : "false");
            AppendField(sb, assignedOrderIndex.ToString(CultureInfo.InvariantCulture));

            AppendField(sb, state.ToString());
            AppendField(sb, isCensored ? "true" : "false");
            AppendField(sb, censoringReason);
            AppendField(sb, isExcluded ? "true" : "false");
            AppendField(sb, exclusionReason);
            AppendField(sb, adverseEvent ? "true" : "false");
            AppendField(sb, adverseEventNotes);
            AppendField(sb, notes);

            AppendField(sb, startingFloor.ToString(CultureInfo.InvariantCulture));
            AppendField(sb, alertType);
            AppendField(sb, smokeCondition);
            AppendField(sb, smokeDensity.ToString("F3", CultureInfo.InvariantCulture));
            AppendField(sb, visibilityMeters.ToString("F2", CultureInfo.InvariantCulture));
            AppendField(sb, crowdDensity.ToString(CultureInfo.InvariantCulture));
            AppendField(sb, routeConfiguration);

            AppendTimeField(sb, trialStartTime);
            AppendTimeField(sb, alertOnsetTimestamp);
            AppendTimeField(sb, evacuationStartTime);
            AppendTimeField(sb, firstDoorwayCrossingTime);
            AppendTimeField(sb, safeZoneArrivalTime);
            AppendTimeField(sb, trialCompletionTime);

            AppendField(sb, wallClockStartUtc);
            AppendField(sb, wallClockEndUtc);

            AppendTimeField(sb, preEvacuationDelay);
            AppendTimeField(sb, movementDuration);
            AppendTimeField(sb, totalEvacuationDuration);

            AppendField(sb, activeAgentsAtStart.ToString(CultureInfo.InvariantCulture));
            AppendField(sb, evacuatedAgentCount.ToString(CultureInfo.InvariantCulture));
            AppendField(sb, totalPedestrianCrossings.ToString(CultureInfo.InvariantCulture));
            AppendOptionalFloatField(sb, meanDischargeRate);
            AppendOptionalFloatField(sb, peakDischargeRate);
            AppendField(sb, doorwayJamDurationTotal.ToString("F2", CultureInfo.InvariantCulture));

            AppendOptionalFloatField(sb, rawNasaTlxScore);
            AppendOptionalFloatField(sb, weightedNasaTlxScore);
            AppendOptionalFloatField(sb, ssqTotalScore);
            AppendOptionalFloatField(sb, ssqNauseaScore);
            AppendOptionalFloatField(sb, ssqOculomotorScore);
            AppendOptionalFloatField(sb, ssqDisorientationScore, isLast: true);

            return sb.ToString();
        }

        private static void AppendField(StringBuilder sb, string value, bool isLast = false)
        {
            if (string.IsNullOrEmpty(value))
            {
                if (!isLast) sb.Append(',');
                return;
            }

            bool containsSpecial = value.IndexOfAny(new char[] { ',', '"', '\r', '\n' }) >= 0;
            if (containsSpecial)
            {
                sb.Append('"');
                sb.Append(value.Replace("\"", "\"\""));
                sb.Append('"');
            }
            else
            {
                sb.Append(value);
            }

            if (!isLast) sb.Append(',');
        }

        private static void AppendTimeField(StringBuilder sb, double seconds, bool isLast = false)
        {
            if (seconds < 0.0)
            {
                if (!isLast) sb.Append(',');
                return;
            }
            sb.Append(seconds.ToString("F3", CultureInfo.InvariantCulture));
            if (!isLast) sb.Append(',');
        }

        private static void AppendOptionalFloatField(StringBuilder sb, float value, bool isLast = false)
        {
            if (value < 0.0f)
            {
                if (!isLast) sb.Append(',');
                return;
            }
            sb.Append(value.ToString("F2", CultureInfo.InvariantCulture));
            if (!isLast) sb.Append(',');
        }
    }
}
