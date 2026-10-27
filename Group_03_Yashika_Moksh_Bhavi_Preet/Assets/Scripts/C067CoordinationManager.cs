using System;
using System.Collections.Generic;
using UnityEngine;

namespace C067.Coordination
{
    /// <summary>
    /// Master C067 coordinator component that wires doorway trackers, jam detectors,
    /// 90 Hz telemetry logging, emergency command networking, and terminal UI into
    /// the five-story hostel evacuation scene without modifying or breaking existing XR or crowd systems.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class C067CoordinationManager : MonoBehaviour
    {
        public static C067CoordinationManager Instance { get; private set; }

        [Header("Telemetry Configuration")]
        [Tooltip("Doorway tracker for the primary ground-floor egress portal")]
        public DoorwayCrossingTracker mainEntranceTracker;

        [Tooltip("Bottleneck telemetry logger operating at 90 Hz")]
        public BottleneckTelemetryLogger telemetryLogger;

        [Tooltip("Emergency command and coordination network system")]
        public EmergencyCommandNetwork commandNetwork;

        [Tooltip("Head warden command terminal desktop UI")]
        public WardenCommandTerminalUI wardenTerminalUI;

        [Tooltip("Floor marshal terminal desktop UI")]
        public MarshalTerminalUI marshalTerminalUI;

        [Header("Stairwell Portals")]
        public List<DoorwayCrossingTracker> stairwellTrackers = new List<DoorwayCrossingTracker>();

        private bool _isSubscribed = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (Instance == null && FindAnyObjectByType<C067CoordinationManager>() == null)
            {
                var go = new GameObject("C067_CoordinationManager");
                go.AddComponent<C067CoordinationManager>();
                Debug.Log("[C067] Automatically bootstrapped C067_CoordinationManager into scene.");
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

            InitializeComponents();
        }

        private void Start()
        {
            ConfigureSceneDoorways();
            SubscribeEvents();
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            UpdateDoorwayQueueAndOccupancy();
        }

        private void InitializeComponents()
        {
            // 1. Ensure EmergencyCommandNetwork exists
            if (commandNetwork == null)
            {
                commandNetwork = GetComponent<EmergencyCommandNetwork>();
                if (commandNetwork == null) commandNetwork = gameObject.AddComponent<EmergencyCommandNetwork>();
            }

            // 2. Ensure BottleneckTelemetryLogger exists
            if (telemetryLogger == null)
            {
                telemetryLogger = GetComponent<BottleneckTelemetryLogger>();
                if (telemetryLogger == null) telemetryLogger = gameObject.AddComponent<BottleneckTelemetryLogger>();
            }

            // 3. Ensure Warden Terminal UI exists
            if (wardenTerminalUI == null)
            {
                wardenTerminalUI = GetComponent<WardenCommandTerminalUI>();
                if (wardenTerminalUI == null) wardenTerminalUI = gameObject.AddComponent<WardenCommandTerminalUI>();
            }

            // 4. Ensure Marshal Terminal UI exists
            if (marshalTerminalUI == null)
            {
                marshalTerminalUI = GetComponent<MarshalTerminalUI>();
                if (marshalTerminalUI == null) marshalTerminalUI = gameObject.AddComponent<MarshalTerminalUI>();
            }

            // 5. Ensure PreEvacuationDelayTracker exists
            if (FindFirstObjectByType<PreEvacuationDelayTracker>() == null)
            {
                gameObject.AddComponent<PreEvacuationDelayTracker>();
            }

            // 6. Ensure NASA-TLX Assessment Controller exists
            if (FindFirstObjectByType<NASA_TLXAssessmentController>() == null)
            {
                gameObject.AddComponent<NASA_TLXAssessmentController>();
            }

            // 7. Ensure SSQ Assessment Controller exists
            if (FindFirstObjectByType<SSQAssessmentController>() == null)
            {
                gameObject.AddComponent<SSQAssessmentController>();
            }

            // 8. Ensure HumanFactorsTrialManager exists
            if (FindFirstObjectByType<HumanFactorsTrialManager>() == null)
            {
                gameObject.AddComponent<HumanFactorsTrialManager>();
            }
        }

        public void ConfigureSceneDoorways()
        {
            // 1. Locate Ground Floor Main Entrance Double Door (Z = -5.4m)
            GameObject mainDoorObj = GameObject.Find("MainEntrance_DoubleDoor");
            if (mainDoorObj == null)
            {
                var entranceDoor = FindAnyObjectByType<HostelEntranceDoor>();
                if (entranceDoor != null) mainDoorObj = entranceDoor.gameObject;
            }

            if (mainDoorObj != null)
            {
                mainEntranceTracker = mainDoorObj.GetComponent<DoorwayCrossingTracker>();
                if (mainEntranceTracker == null)
                {
                    mainEntranceTracker = mainDoorObj.AddComponent<DoorwayCrossingTracker>();
                    mainEntranceTracker.doorwayId = "Ground_MainEntrance_DoubleDoor";
                    mainEntranceTracker.floorId = 0;
                    mainEntranceTracker.doorwayWidthMeters = 2.4f;
                    mainEntranceTracker.orientation = DoorwayOrientation.NorthSouth_EgressSouth;
                    mainEntranceTracker.queueZoneDepthMeters = 3.5f;
                    mainEntranceTracker.portalDepthMeters = 1.0f;
                }

                var jamDetector = mainDoorObj.GetComponent<DoorwayJamDetector>();
                if (jamDetector == null)
                {
                    jamDetector = mainDoorObj.AddComponent<DoorwayJamDetector>();
                    jamDetector.queueLengthThreshold = 5;
                    jamDetector.occupancyThreshold = 3;
                    jamDetector.lowDischargeThreshold = 0.5f;
                    jamDetector.jamPersistenceDuration = 1.5f;
                    jamDetector.recoveryPersistenceDuration = 1.0f;
                    jamDetector.hysteresisRatio = 0.6f;
                }

                if (telemetryLogger != null)
                {
                    telemetryLogger.doorwayTracker = mainEntranceTracker;
                    telemetryLogger.jamDetector = jamDetector;
                }
            }

            // 2. Locate and configure Stairwell Archways on upper floors
            SetupStairwellPortal("EastStairwell_Portal_Floor1", new Vector3(7.5f, 3.025f, 0f), 1, "Floor1_EastStair");
            SetupStairwellPortal("WestStairwell_Portal_Floor1", new Vector3(-7.5f, 3.025f, 0f), 1, "Floor1_WestStair");
            SetupStairwellPortal("EastStairwell_Portal_Floor2", new Vector3(7.5f, 6.025f, 0f), 2, "Floor2_EastStair");
            SetupStairwellPortal("WestStairwell_Portal_Floor2", new Vector3(-7.5f, 6.025f, 0f), 2, "Floor2_WestStair");
            SetupStairwellPortal("EastStairwell_Portal_Floor3", new Vector3(7.5f, 9.025f, 0f), 3, "Floor3_EastStair");
            SetupStairwellPortal("WestStairwell_Portal_Floor3", new Vector3(-7.5f, 9.025f, 0f), 3, "Floor3_WestStair");
            SetupStairwellPortal("EastStairwell_Portal_Floor4", new Vector3(7.5f, 12.025f, 0f), 4, "Floor4_EastStair");
            SetupStairwellPortal("WestStairwell_Portal_Floor4", new Vector3(-7.5f, 12.025f, 0f), 4, "Floor4_WestStair");

            Debug.Log($"[C067CoordinationManager] Configured Main Entrance and {stairwellTrackers.Count} stairwell doorway trackers.");
        }

        private void SetupStairwellPortal(string objectName, Vector3 position, int floor, string doorwayId)
        {
            GameObject portalObj = GameObject.Find(objectName);
            if (portalObj == null)
            {
                portalObj = new GameObject(objectName);
                portalObj.transform.position = position;
                portalObj.transform.rotation = Quaternion.identity;
                portalObj.transform.SetParent(transform, true);
            }

            var tracker = portalObj.GetComponent<DoorwayCrossingTracker>();
            if (tracker == null)
            {
                tracker = portalObj.AddComponent<DoorwayCrossingTracker>();
                tracker.doorwayId = doorwayId;
                tracker.floorId = floor;
                tracker.doorwayWidthMeters = 1.4f; // Standard NBC stair flight clear width
                tracker.orientation = (position.x > 0f) ? DoorwayOrientation.EastWest_EgressEast : DoorwayOrientation.EastWest_EgressWest;
                tracker.queueZoneDepthMeters = 2.5f;
                tracker.portalDepthMeters = 0.8f;
            }

            var jam = portalObj.GetComponent<DoorwayJamDetector>();
            if (jam == null)
            {
                jam = portalObj.AddComponent<DoorwayJamDetector>();
                jam.queueLengthThreshold = 4;
                jam.occupancyThreshold = 2;
                jam.lowDischargeThreshold = 0.4f;
                jam.jamPersistenceDuration = 1.5f;
            }

            if (!stairwellTrackers.Contains(tracker))
            {
                stairwellTrackers.Add(tracker);
            }
        }

        private void SubscribeEvents()
        {
            if (_isSubscribed) return;

            if (EvacuationEgressManager.Instance != null)
            {
                EvacuationEgressManager.Instance.OnAgentEvacuatedThroughMainDoor += HandleAgentEvacuatedThroughMainDoor;
                _isSubscribed = true;
            }
        }

        private void UnsubscribeEvents()
        {
            if (!_isSubscribed) return;

            if (EvacuationEgressManager.Instance != null)
            {
                EvacuationEgressManager.Instance.OnAgentEvacuatedThroughMainDoor -= HandleAgentEvacuatedThroughMainDoor;
            }
            _isSubscribed = false;
        }

        private void HandleAgentEvacuatedThroughMainDoor(int agentId, Vector3 position)
        {
            if (mainEntranceTracker != null)
            {
                mainEntranceTracker.RecordCrossing(agentId);
            }
        }

        /// <summary>
        /// Continuously updates doorway queue length and occupancy by sampling crowd density in the approach bounds.
        /// </summary>
        private void UpdateDoorwayQueueAndOccupancy()
        {
            if (EvacuationEgressManager.Instance == null) return;

            // 1. Main Entrance Doorway at Ground Exit:
            // Door threshold center at (0, 0.125, -5.4). Usable width = 2.4m.
            // Queue zone: Z in [-5.3, -2.5], X in [-1.6, 1.6], Y in [0, 2.0]
            if (mainEntranceTracker != null)
            {
                Vector3 queueCenter = new Vector3(0f, 1.0f, -3.9f);
                Vector3 queueSize = new Vector3(3.2f, 2.0f, 2.8f);
                int queue = EvacuationEgressManager.Instance.GetAgentsInBoundsCount(queueCenter, queueSize);

                Vector3 portalCenter = new Vector3(0f, 1.0f, -5.4f);
                Vector3 portalSize = new Vector3(2.6f, 2.2f, 1.2f);
                int occ = EvacuationEgressManager.Instance.GetAgentsInBoundsCount(portalCenter, portalSize);

                mainEntranceTracker.UpdateQueueAndOccupancy(queue, occ);
            }

            // 2. Stairwell doorway trackers:
            foreach (var tracker in stairwellTrackers)
            {
                if (tracker == null) continue;

                Vector3 tPos = tracker.transform.position;
                Vector3 qCenter = tPos + ((tPos.x > 0f) ? Vector3.left : Vector3.right) * 1.5f;
                Vector3 qSize = new Vector3(2.5f, 2.0f, 2.0f);
                int queue = EvacuationEgressManager.Instance.GetAgentsInBoundsCount(qCenter, qSize);

                Vector3 pCenter = tPos;
                Vector3 pSize = new Vector3(1.5f, 2.0f, 1.5f);
                int occ = EvacuationEgressManager.Instance.GetAgentsInBoundsCount(pCenter, pSize);

                tracker.UpdateQueueAndOccupancy(queue, occ);
            }
        }

        /// <summary>
        /// Resets all doorway and telemetry data for a new experimental trial.
        /// </summary>
        public void ResetTrial()
        {
            if (mainEntranceTracker != null) mainEntranceTracker.ResetTrialData();
            foreach (var t in stairwellTrackers)
            {
                if (t != null) t.ResetTrialData();
            }

            if (commandNetwork != null) commandNetwork.ResetState();
            Debug.Log("[C067CoordinationManager] Completed clean trial data reset.");
        }
    }
}
