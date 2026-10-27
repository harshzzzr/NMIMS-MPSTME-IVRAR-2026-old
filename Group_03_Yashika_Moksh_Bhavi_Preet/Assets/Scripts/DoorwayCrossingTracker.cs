using System;
using System.Collections.Generic;
using UnityEngine;

namespace C067.Coordination
{
    /// <summary>
    /// Orientation axis or custom mode of the doorway crossing plane.
    /// Supports cardinal directions as well as arbitrary 3D orientations.
    /// </summary>
    public enum DoorwayOrientation
    {
        NorthSouth_EgressSouth = 0, // Agents evacuate by moving south (-Z)
        NorthSouth_EgressNorth = 1, // Agents evacuate by moving north (+Z)
        EastWest_EgressEast = 2,    // Agents evacuate by moving east (+X)
        EastWest_EgressWest = 3,    // Agents evacuate by moving west (-X)
        CustomNormal = 4,           // Arbitrary normal vector defined in inspector
        TransformForward = 5        // Uses transform.forward of this GameObject
    }

    /// <summary>
    /// Tracks pedestrian agent crossings, occupancy, queue length, and flow dynamics through an architectural doorway.
    /// Phase 3 compliant: Robust against arbitrary doorway angles, lingering agents, reverse movement,
    /// simultaneous crossings, agent destruction, and scene resets.
    /// Calculates authoritative discharge rate J = N / Δt and width-normalized flow j = N / (W × Δt).
    /// </summary>
    [SelectionBase]
    public class DoorwayCrossingTracker : MonoBehaviour
    {
        [Header("Doorway Identity")]
        [Tooltip("Stable unique doorway identifier (e.g., 'Ground_MainExit', 'Floor1_EastStair')")]
        public string doorwayId = "Ground_MainExit";

        [Tooltip("Hostel floor level index (0 = Ground, 1 = Floor 1, ..., 4 = Floor 4)")]
        public int floorId = 0;

        [Header("Measurement Line & Plane Geometry")]
        [Tooltip("Effective usable clearance width W in meters (NBC 2016 / NFPA 101)")]
        [Min(0.1f)]
        public float doorwayWidthMeters = 2.4f;

        [Tooltip("Orientation and egress crossing direction mode")]
        public DoorwayOrientation orientation = DoorwayOrientation.NorthSouth_EgressSouth;

        [Tooltip("Offset of measurement line origin relative to this transform")]
        public Vector3 lineCenterOffset = Vector3.zero;

        [Tooltip("Custom downstream crossing vector when orientation is CustomNormal")]
        public Vector3 customCrossingNormal = Vector3.forward;

        [Tooltip("Transform custom normal by object rotation if true")]
        public bool useLocalRotationForNormal = true;

        [Tooltip("Threshold depth of doorway portal volume in meters")]
        public float portalDepthMeters = 1.0f;

        [Tooltip("Upstream approach queue detection distance in meters")]
        public float queueZoneDepthMeters = 3.5f;

        [Tooltip("Lateral tolerance added to doorway width for crossing detection")]
        public float lateralToleranceMeters = 0.25f;

        [Tooltip("Vertical tolerance above/below doorway center for crossing detection")]
        public float verticalToleranceMeters = 2.5f;

        [Header("Discharge Calculation Settings")]
        [Tooltip("Sliding time window in seconds for rolling discharge rate")]
        [Range(0.5f, 10.0f)]
        public float rollingWindowSeconds = 2.0f;

        // Authoritative Counters
        [Header("Runtime Telemetry (Read-Only)")]
        [SerializeField] private int _cumulativeCrossingsSession = 0;
        [SerializeField] private int _cumulativeCrossingsTrial = 0;
        [SerializeField] private int _crossingsInCurrentInterval = 0;
        [SerializeField] private int _currentOccupancy = 0;
        [SerializeField] private int _currentQueueLength = 0;
        [SerializeField] private float _instantaneousDischargeRate = 0f;
        [SerializeField] private float _rollingDischargeRate = 0f;
        [SerializeField] private float _normalizedFlow = 0f;

        // Per-Agent State Tracking for Continuous Crossing Detection
        private struct AgentCrossingState
        {
            public float lastSignedDistance;
            public double lastSeenTime;
            public bool wasUpstream;
            public bool hasCrossed;
        }

        private readonly Dictionary<int, AgentCrossingState> _trackedAgents = new Dictionary<int, AgentCrossingState>();
        private readonly HashSet<int> _agentsInsidePortal = new HashSet<int>();
        private readonly HashSet<int> _agentsInQueueZone = new HashSet<int>();
        private readonly Dictionary<int, float> _agentEntryTimes = new Dictionary<int, float>();

        // Sliding window of crossing timestamps for rolling discharge rate
        private readonly Queue<float> _rollingCrossingTimestamps = new Queue<float>();

        // Delta-t interval accounting
        private double _lastIntervalResetMonotonicTime = 0;
        private double _lastCleanupTime = 0;
        private const double STALE_AGENT_TIMEOUT_SEC = 30.0;

        // Public Properties
        public string DoorwayId => doorwayId;
        public int FloorId => floorId;
        public float DoorwayWidthMeters => doorwayWidthMeters;
        public int CumulativeCrossingsSession => _cumulativeCrossingsSession;
        public int CumulativeCrossingsTrial => _cumulativeCrossingsTrial;
        public int CrossingsInCurrentInterval => _crossingsInCurrentInterval;
        public int CurrentOccupancy => _currentOccupancy;
        public int CurrentQueueLength => _currentQueueLength;
        public float InstantaneousDischargeRate => _instantaneousDischargeRate;
        public float RollingDischargeRate => _rollingDischargeRate;
        public float NormalizedFlow => _normalizedFlow;

        /// <summary>
        /// Estimates physical queue length in meters based on crowd count, effective lanes, and longitudinal body depth.
        /// Returns -1 if doorway width is unavailable.
        /// </summary>
        public float EstimatedQueueLengthMeters
        {
            get
            {
                if (_currentQueueLength <= 0) return 0f;
                if (doorwayWidthMeters <= 0.01f || float.IsNaN(doorwayWidthMeters) || float.IsInfinity(doorwayWidthMeters)) return -1f;

                int effectiveLanes = Mathf.Max(1, Mathf.FloorToInt(doorwayWidthMeters / 0.65f));
                float depthMeters = (_currentQueueLength / (float)effectiveLanes) * 0.65f;
                return depthMeters;
            }
        }

        // Events
        public event Action<int, string> OnAgentCrossed; // agentId, doorwayId

        private void Awake()
        {
            _lastIntervalResetMonotonicTime = Time.realtimeSinceStartupAsDouble;
        }

        private void Update()
        {
            UpdateRollingDischarge(Time.time);

            // Periodically prune stale agent records to prevent memory leak
            if (Time.timeAsDouble - _lastCleanupTime > 5.0)
            {
                _lastCleanupTime = Time.timeAsDouble;
                PruneStaleAgents();
            }
        }

        /// <summary>
        /// Registers a verified crossing from crowd navigation or trigger system.
        /// Strictly authoritative: increments session and trial counters exactly once per valid passage.
        /// </summary>
        public bool RecordCrossing(int agentId, float timestamp = -1f)
        {
            if (timestamp < 0f) timestamp = Time.time;

            _cumulativeCrossingsSession++;
            _cumulativeCrossingsTrial++;
            _crossingsInCurrentInterval++;

            _rollingCrossingTimestamps.Enqueue(timestamp);
            UpdateRollingDischarge(timestamp);

            OnAgentCrossed?.Invoke(agentId, doorwayId);
            return true;
        }

        /// <summary>
        /// Evaluates agent position relative to the measurement plane for arbitrary orientations.
        /// Detects forward crossings, rejects reverse movement, ignores lingering agents,
        /// and updates per-agent crossing state without memory allocations.
        /// </summary>
        public bool EvaluateAgentCrossing(int agentId, Vector3 worldPos, double timestamp = -1)
        {
            if (timestamp < 0) timestamp = Time.timeAsDouble;

            Vector3 lineCenter = GetLineCenterWorld();
            Vector3 egressNormal = GetDownstreamNormalWorld();
            Vector3 lateralNormal = Vector3.Cross(Vector3.up, egressNormal).normalized;
            if (lateralNormal == Vector3.zero) lateralNormal = Vector3.right;

            Vector3 delta = worldPos - lineCenter;
            float signedDist = Vector3.Dot(delta, egressNormal);
            float lateralDist = Mathf.Abs(Vector3.Dot(delta, lateralNormal));
            float verticalDist = Mathf.Abs(delta.y);

            float halfWidth = (doorwayWidthMeters * 0.5f) + lateralToleranceMeters;

            // Check if agent is within the lateral aperture and vertical clearance of the doorway
            bool inAperture = (lateralDist <= halfWidth) && (verticalDist <= verticalToleranceMeters);

            if (!inAperture)
            {
                // Agent has moved completely outside doorway corridor; prune if previously completed
                if (_trackedAgents.TryGetValue(agentId, out var existing) && existing.hasCrossed)
                {
                    _trackedAgents.Remove(agentId);
                }
                return false;
            }

            bool crossingRecorded = false;

            if (_trackedAgents.TryGetValue(agentId, out AgentCrossingState state))
            {
                // Agent was previously observed
                if (state.wasUpstream && signedDist <= 0f && !state.hasCrossed)
                {
                    // Transitioned from upstream (s > 0) to downstream (s <= 0): VALID FORWARD CROSSING!
                    RecordCrossing(agentId, (float)timestamp);
                    state.hasCrossed = true;
                    state.wasUpstream = false;
                    crossingRecorded = true;
                }
                else if (!state.wasUpstream && signedDist > 0f)
                {
                    // Reverse movement: agent backed up or retreated upstream
                    // Reset upstream state without recording a crossing
                    state.wasUpstream = true;
                    state.hasCrossed = false;
                }

                state.lastSignedDistance = signedDist;
                state.lastSeenTime = timestamp;
                _trackedAgents[agentId] = state;
            }
            else
            {
                // New agent observed near doorway
                state = new AgentCrossingState
                {
                    wasUpstream = (signedDist > 0f),
                    lastSignedDistance = signedDist,
                    lastSeenTime = timestamp,
                    hasCrossed = (signedDist <= 0f) // If first observed already downstream, do not count
                };
                _trackedAgents[agentId] = state;
            }

            return crossingRecorded;
        }

        /// <summary>
        /// Explicitly unregisters an agent upon despawn or destruction to prevent memory leaks.
        /// </summary>
        public void UnregisterAgent(int agentId)
        {
            _trackedAgents.Remove(agentId);
            _agentsInsidePortal.Remove(agentId);
            _agentsInQueueZone.Remove(agentId);
            _agentEntryTimes.Remove(agentId);
            _currentOccupancy = _agentsInsidePortal.Count;
        }

        /// <summary>
        /// Notifies the tracker that an agent has entered the physical doorway threshold.
        /// </summary>
        public void NotifyAgentEnteredPortal(int agentId)
        {
            if (_agentsInsidePortal.Add(agentId))
            {
                _currentOccupancy = _agentsInsidePortal.Count;
                _agentEntryTimes[agentId] = Time.time;
            }
        }

        /// <summary>
        /// Notifies the tracker that an agent has departed the physical doorway threshold.
        /// If egressed in forward downstream direction, counts as a valid crossing.
        /// If agent backed out or retreated upstream, does NOT count.
        /// </summary>
        public void NotifyAgentExitedPortal(int agentId, bool crossedDownstream)
        {
            if (_agentsInsidePortal.Remove(agentId))
            {
                _currentOccupancy = _agentsInsidePortal.Count;
                _agentEntryTimes.Remove(agentId);

                if (crossedDownstream)
                {
                    RecordCrossing(agentId);
                }
            }
        }

        /// <summary>
        /// Updates the current upstream queue length and doorway occupancy counts.
        /// </summary>
        public void UpdateQueueAndOccupancy(int queueCount, int occupancyCount)
        {
            _currentQueueLength = Mathf.Max(0, queueCount);
            _currentOccupancy = Mathf.Max(0, occupancyCount);
        }

        /// <summary>
        /// Called by the high-frequency telemetry sampler to extract interval metrics and reset interval accumulators.
        /// Uses actual elapsed monotonic time deltaT to compute discharge rate J and width-normalized flow j.
        /// Validates against zero/negative deltaT and invalid doorway width.
        /// </summary>
        public void HarvestSamplingInterval(double currentMonotonicTime, out int crossingsThisInterval, out float instDischarge, out float normFlow)
        {
            crossingsThisInterval = _crossingsInCurrentInterval;
            _crossingsInCurrentInterval = 0;

            double deltaT = currentMonotonicTime - _lastIntervalResetMonotonicTime;
            _lastIntervalResetMonotonicTime = currentMonotonicTime;

            float dt = (float)deltaT;

            // Strict validation: positive non-zero elapsed interval
            if (dt > 0.0001f)
            {
                // Discharge rate: J = N / Δt (agents/s)
                instDischarge = crossingsThisInterval / dt;

                // Width-normalized flow: j = N / (W × Δt) (agents/(m·s))
                if (doorwayWidthMeters > 0.01f && !float.IsNaN(doorwayWidthMeters) && !float.IsInfinity(doorwayWidthMeters))
                {
                    normFlow = crossingsThisInterval / (doorwayWidthMeters * dt);
                }
                else
                {
                    // Non-positive or invalid doorway width
                    normFlow = crossingsThisInterval > 0 ? -1f : 0f;
                }
            }
            else
            {
                instDischarge = 0f;
                normFlow = 0f;
            }

            _instantaneousDischargeRate = instDischarge;
            _normalizedFlow = normFlow;
        }

        /// <summary>
        /// Updates rolling discharge rate over the configured rolling time window.
        /// Purges timestamps outside the window.
        /// </summary>
        private void UpdateRollingDischarge(float currentTime)
        {
            float cutoffTime = currentTime - rollingWindowSeconds;
            while (_rollingCrossingTimestamps.Count > 0 && _rollingCrossingTimestamps.Peek() < cutoffTime)
            {
                _rollingCrossingTimestamps.Dequeue();
            }

            if (rollingWindowSeconds > 0.001f)
            {
                _rollingDischargeRate = _rollingCrossingTimestamps.Count / rollingWindowSeconds;
            }
            else
            {
                _rollingDischargeRate = 0f;
            }
        }

        /// <summary>
        /// Resets per-trial metrics while preserving session-level cumulative totals.
        /// </summary>
        public void ResetTrialData()
        {
            _cumulativeCrossingsTrial = 0;
            _crossingsInCurrentInterval = 0;
            _currentOccupancy = 0;
            _currentQueueLength = 0;
            _instantaneousDischargeRate = 0f;
            _rollingDischargeRate = 0f;
            _normalizedFlow = 0f;
            _agentsInsidePortal.Clear();
            _agentsInQueueZone.Clear();
            _agentEntryTimes.Clear();
            _trackedAgents.Clear();
            _rollingCrossingTimestamps.Clear();
            _lastIntervalResetMonotonicTime = Time.realtimeSinceStartupAsDouble;
        }

        /// <summary>
        /// Resets all data including session cumulative totals.
        /// </summary>
        public void ResetSessionData()
        {
            ResetTrialData();
            _cumulativeCrossingsSession = 0;
        }

        /// <summary>
        /// Resets the doorway tracker completely for a new test or evacuation restart.
        /// </summary>
        public void ResetDoorway()
        {
            ResetTrialData();
        }

        // =========================================================================
        // GEOMETRIC VECTORS & ORIENTATION RESOLUTION
        // =========================================================================

        public Vector3 GetLineCenterWorld()
        {
            return transform.TransformPoint(lineCenterOffset);
        }

        public Vector3 GetDownstreamNormalWorld()
        {
            switch (orientation)
            {
                case DoorwayOrientation.NorthSouth_EgressSouth:
                    return Vector3.back; // (0, 0, -1)
                case DoorwayOrientation.NorthSouth_EgressNorth:
                    return Vector3.forward; // (0, 0, 1)
                case DoorwayOrientation.EastWest_EgressEast:
                    return Vector3.right; // (1, 0, 0)
                case DoorwayOrientation.EastWest_EgressWest:
                    return Vector3.left; // (-1, 0, 0)
                case DoorwayOrientation.TransformForward:
                    return transform.forward;
                case DoorwayOrientation.CustomNormal:
                    Vector3 norm = customCrossingNormal.sqrMagnitude > 0.001f ? customCrossingNormal.normalized : Vector3.forward;
                    return useLocalRotationForNormal ? transform.TransformDirection(norm) : norm;
                default:
                    return transform.forward;
            }
        }

        /// <summary>
        /// Tests if a given world position is currently inside the upstream approach queue zone.
        /// Supports cardinal orientations as well as arbitrary angles.
        /// </summary>
        public bool IsPositionInQueueZone(Vector3 worldPos)
        {
            if (orientation == DoorwayOrientation.CustomNormal || orientation == DoorwayOrientation.TransformForward)
            {
                Vector3 center = GetLineCenterWorld();
                Vector3 egress = GetDownstreamNormalWorld();
                Vector3 lateral = Vector3.Cross(Vector3.up, egress).normalized;
                if (lateral == Vector3.zero) lateral = Vector3.right;

                Vector3 delta = worldPos - center;
                float distAlongEgress = Vector3.Dot(delta, egress);
                float distLateral = Mathf.Abs(Vector3.Dot(delta, lateral));

                // Upstream approach is in opposite direction of egress (distAlongEgress in [-queueZoneDepth, 0])
                float halfW = (doorwayWidthMeters * 0.5f) + 0.3f;
                return distLateral <= halfW && distAlongEgress < 0.0f && distAlongEgress >= -queueZoneDepthMeters;
            }

            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            float halfWidth = doorwayWidthMeters * 0.5f + 0.3f;

            switch (orientation)
            {
                case DoorwayOrientation.NorthSouth_EgressSouth:
                    return Mathf.Abs(localPos.x) <= halfWidth && localPos.z > 0.0f && localPos.z <= queueZoneDepthMeters;

                case DoorwayOrientation.NorthSouth_EgressNorth:
                    return Mathf.Abs(localPos.x) <= halfWidth && localPos.z < 0.0f && localPos.z >= -queueZoneDepthMeters;

                case DoorwayOrientation.EastWest_EgressEast:
                    return Mathf.Abs(localPos.z) <= halfWidth && localPos.x < 0.0f && localPos.x >= -queueZoneDepthMeters;

                case DoorwayOrientation.EastWest_EgressWest:
                    return Mathf.Abs(localPos.z) <= halfWidth && localPos.x > 0.0f && localPos.x <= queueZoneDepthMeters;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Tests if a given world position is currently inside the doorway portal threshold.
        /// </summary>
        public bool IsPositionInPortal(Vector3 worldPos)
        {
            Vector3 localPos = transform.InverseTransformPoint(worldPos);
            float halfW = doorwayWidthMeters * 0.5f;
            float halfD = portalDepthMeters * 0.5f;

            return Mathf.Abs(localPos.x) <= halfW && Mathf.Abs(localPos.z) <= halfD && Mathf.Abs(localPos.y) <= 1.8f;
        }

        private void PruneStaleAgents()
        {
            double now = Time.timeAsDouble;
            var staleKeys = new List<int>();

            foreach (var kvp in _trackedAgents)
            {
                if (now - kvp.Value.lastSeenTime > STALE_AGENT_TIMEOUT_SEC)
                {
                    staleKeys.Add(kvp.Key);
                }
            }

            for (int i = 0; i < staleKeys.Count; i++)
            {
                _trackedAgents.Remove(staleKeys[i]);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 center = GetLineCenterWorld();
            Vector3 egressNormal = GetDownstreamNormalWorld();
            Vector3 lateral = Vector3.Cross(Vector3.up, egressNormal).normalized;
            if (lateral == Vector3.zero) lateral = Vector3.right;

            // 1. Draw Doorway Measurement Line (Cyan)
            Gizmos.color = Color.cyan;
            Vector3 halfLateral = lateral * (doorwayWidthMeters * 0.5f);
            Gizmos.DrawLine(center - halfLateral, center + halfLateral);

            // 2. Draw Egress Normal Arrow (Green)
            Gizmos.color = Color.green;
            Gizmos.DrawRay(center, egressNormal * 1.5f);

            // 3. Draw Portal Threshold Volume (Translucent Green)
            Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.LookRotation(egressNormal, Vector3.up), Vector3.one);
            Gizmos.color = new Color(0f, 1f, 0.2f, 0.3f);
            Gizmos.DrawCube(Vector3.zero, new Vector3(doorwayWidthMeters, 2.0f, portalDepthMeters));

            // 4. Draw Upstream Queue Zone (Translucent Orange)
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.2f);
            Vector3 qCenter = new Vector3(0f, 0f, -queueZoneDepthMeters * 0.5f);
            Vector3 qSize = new Vector3(doorwayWidthMeters, 2.0f, queueZoneDepthMeters);
            Gizmos.DrawCube(qCenter, qSize);
        }
    }
}
