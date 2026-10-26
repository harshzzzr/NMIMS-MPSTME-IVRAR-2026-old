using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using SmartCampus.Interaction;
using SmartCampus.Navigation;
using SmartCampus.UI;

namespace SmartCampus.Core
{
    public class SmartCampusManager : MonoBehaviour
    {
        [Header("Primary Subsystems")]
        [SerializeField] private CampusArchitectureBuilder architectureBuilder;
        [SerializeField] private CampusGraph campusGraph;
        [SerializeField] private RouteManager routeManager;
        [SerializeField] private RouteArrowManager arrowManager;
        [SerializeField] private PlayerLocationTracker locationTracker;
        [SerializeField] private DestinationMenuUI destinationMenu;
        [SerializeField] private NavigationKioskTerminal kioskTerminal;
        [SerializeField] private CompactGuidancePanel guidancePanel;

        [Header("Player & XR Setup")]
        [SerializeField] private GameObject playerRig;
        [SerializeField] private VRPlayerController playerController;

        private void Awake()
        {
            EnsureDependencies();
            ConfigureEventSystem();
            SetupPlayerLocomotionAndPointers();
        }

        private void Start()
        {
            EnsureDependencies();
            BuildGraphData();

            // Menu starts closed until interacted with at terminal or menu button
            if (destinationMenu != null)
            {
                destinationMenu.gameObject.SetActive(false);
            }
        }

        public void EnsureDependencies()
        {
            if (architectureBuilder == null) architectureBuilder = GetComponentInChildren<CampusArchitectureBuilder>() ?? FindAnyObjectByType<CampusArchitectureBuilder>();
            if (campusGraph == null) campusGraph = GetComponentInChildren<CampusGraph>() ?? FindAnyObjectByType<CampusGraph>();
            if (routeManager == null) routeManager = GetComponentInChildren<RouteManager>() ?? FindAnyObjectByType<RouteManager>();
            if (arrowManager == null) arrowManager = GetComponentInChildren<RouteArrowManager>() ?? FindAnyObjectByType<RouteArrowManager>();
            if (locationTracker == null) locationTracker = GetComponentInChildren<PlayerLocationTracker>() ?? FindAnyObjectByType<PlayerLocationTracker>();
            if (destinationMenu == null) destinationMenu = GetComponentInChildren<DestinationMenuUI>(true) ?? FindAnyObjectByType<DestinationMenuUI>(FindObjectsInactive.Include);
            if (kioskTerminal == null) kioskTerminal = GetComponentInChildren<NavigationKioskTerminal>() ?? FindAnyObjectByType<NavigationKioskTerminal>();
            if (guidancePanel == null) guidancePanel = GetComponentInChildren<CompactGuidancePanel>(true) ?? FindAnyObjectByType<CompactGuidancePanel>(FindObjectsInactive.Include);
        }

        public void ConfigureEventSystem()
        {
            var es = FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<EventSystem>();
            }

            // Ensure StandaloneInputModule (legacy) is NOT used; ensure InputSystemUIInputModule is used
            var legacyModule = es.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                if (Application.isPlaying) Destroy(legacyModule);
                else DestroyImmediate(legacyModule);
            }

#if ENABLE_INPUT_SYSTEM
            var inputSystemModule = es.GetComponent<InputSystemUIInputModule>();
            if (inputSystemModule == null)
            {
                inputSystemModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#endif
        }

        public void SetupPlayerLocomotionAndPointers()
        {
            if (playerRig == null)
            {
                var rig = GameObject.Find("[BuildingBlock] Camera Rig") ?? GameObject.Find("OVRCameraRig");
                if (rig != null) playerRig = rig;
                else if (Camera.main != null) playerRig = Camera.main.transform.root.gameObject;
            }

            if (playerRig != null)
            {
                // Ensure CharacterController
                var cc = playerRig.GetComponent<CharacterController>();
                if (cc == null)
                {
                    cc = playerRig.AddComponent<CharacterController>();
                    cc.radius = 0.3f;
                    cc.height = 1.75f;
                    cc.center = new Vector3(0, 0.88f, 0);
                    cc.stepOffset = 0.25f;
                    cc.slopeLimit = 45f;
                }

                // Ensure VRPlayerController
                if (playerController == null)
                {
                    playerController = playerRig.GetComponent<VRPlayerController>();
                    if (playerController == null)
                    {
                        playerController = playerRig.AddComponent<VRPlayerController>();
                    }
                }

                // Ensure XRRayPointer on right hand controller / camera
                var rightHand = GameObject.Find("RightHandAnchor") ?? GameObject.Find("RightControllerAnchor");
                Transform pointerParent = (rightHand != null) ? rightHand.transform : Camera.main != null ? Camera.main.transform : playerRig.transform;

                var pointer = pointerParent.GetComponentInChildren<XRRayPointer>();
                if (pointer == null)
                {
                    var pointerObj = new GameObject("XRRayPointer");
                    pointerObj.transform.SetParent(pointerParent, false);
                    pointer = pointerObj.AddComponent<XRRayPointer>();
                }
            }
        }

        public void BuildGraphData()
        {
            if (campusGraph == null) campusGraph = FindAnyObjectByType<CampusGraph>();
            if (campusGraph == null) return;

            campusGraph.ClearGraph();

            // 1. Nodes
            // Outdoor Plaza & Ground Lobby
            campusGraph.AddNode(new CampusNode("node_entrance", "Campus Arrival Plaza", new Vector3(0, 0, -18f), CampusNodeType.Entrance, DestinationCategory.All, true, 0, "Outdoor arrival plaza by grand campus arch."));
            campusGraph.AddNode(new CampusNode("node_kiosk", "Arrival Wayfinding Terminal", new Vector3(3, 0, -5f), CampusNodeType.Reception, DestinationCategory.Amenities, true, 0, "Interactive information and navigation terminal."));
            campusGraph.AddNode(new CampusNode("node_lobby_reception", "Lobby & Reception Desk", new Vector3(0, 0, 6f), CampusNodeType.Reception, DestinationCategory.Amenities, true, 0, "Main reception desk, visitor sign-in, and waiting lounge."));

            // Central Spine Corridors
            campusGraph.AddNode(new CampusNode("node_central_junction", "Central Crossway Junction", new Vector3(0, 0, 24f), CampusNodeType.Junction, DestinationCategory.All, true, 0, "Main intersection connecting Academic Wing, Library, and Terrace."));
            campusGraph.AddNode(new CampusNode("node_north_hallway", "North Terrace Hallway", new Vector3(0, 0, 40f), CampusNodeType.Corridor, DestinationCategory.All, true, 0, "North hallway leading to stairs and ramp."));

            // West Wing (Academic) - Corridor, Doorways, and Interior Rooms
            campusGraph.AddNode(new CampusNode("node_academic_hallway", "Academic Wing Corridor", new Vector3(-6.5f, 0, 24f), CampusNodeType.Corridor, DestinationCategory.Academic, true, 0, "West corridor access for classrooms and labs."));
            
            campusGraph.AddNode(new CampusNode("node_corridor_class101", "Corridor outside Room 101", new Vector3(-6.5f, 0, 18f), CampusNodeType.Corridor, DestinationCategory.Academic, true, 0, "Corridor junction outside Classroom 101 entrance."));
            campusGraph.AddNode(new CampusNode("node_door_class101", "Classroom 101 Doorway", new Vector3(-10.0f, 0, 18f), CampusNodeType.Corridor, DestinationCategory.Academic, true, 0, "Designated entrance doorway for Classroom 101."));
            campusGraph.AddNode(new CampusNode("node_class101", "Classroom 101 (Lecture Hall A)", new Vector3(-15f, 0, 18f), CampusNodeType.Classroom, DestinationCategory.Academic, true, 0, "Undergraduate lecture hall with tiered seating and whiteboard."));

            campusGraph.AddNode(new CampusNode("node_corridor_class102", "Corridor outside Room 102", new Vector3(-6.5f, 0, 30f), CampusNodeType.Corridor, DestinationCategory.Academic, true, 0, "Corridor junction outside Classroom 102 entrance."));
            campusGraph.AddNode(new CampusNode("node_door_class102", "Classroom 102 Doorway", new Vector3(-10.0f, 0, 30f), CampusNodeType.Corridor, DestinationCategory.Academic, true, 0, "Designated entrance doorway for Classroom 102."));
            campusGraph.AddNode(new CampusNode("node_class102", "Classroom 102 (Seminar Room B)", new Vector3(-15f, 0, 30f), CampusNodeType.Classroom, DestinationCategory.Academic, true, 0, "Collaborative seminar room and workshop space."));

            campusGraph.AddNode(new CampusNode("node_corridor_lab204", "Corridor outside Lab 204", new Vector3(-6.5f, 0, 44f), CampusNodeType.Corridor, DestinationCategory.Labs, true, 0, "Corridor junction outside Computer Lab 204 entrance."));
            campusGraph.AddNode(new CampusNode("node_door_lab204", "Computer Lab 204 Doorway", new Vector3(-10.0f, 0, 44f), CampusNodeType.Corridor, DestinationCategory.Labs, true, 0, "Designated entrance doorway for Computer Lab 204."));
            campusGraph.AddNode(new CampusNode("node_lab204", "Computer Lab 204 (Advanced Computing)", new Vector3(-15f, 0, 44f), CampusNodeType.ComputerLab, DestinationCategory.Labs, true, 0, "High-performance computing lab with workstations and server racks."));

            // Southwest Service Bypass
            campusGraph.AddNode(new CampusNode("node_bypass_west", "Southwest Service Bypass", new Vector3(-6.5f, 0, 12f), CampusNodeType.Corridor, DestinationCategory.All, true, 0, "Alternate walkway connecting Lobby to West Academic Wing."));

            // East Wing (Library & Amenities) - Corridor, Doorways, and Interior Rooms
            campusGraph.AddNode(new CampusNode("node_east_junction", "East Wing Corridor", new Vector3(6.5f, 0, 24f), CampusNodeType.Corridor, DestinationCategory.All, true, 0, "East corridor access for Library and Canteen."));
            
            campusGraph.AddNode(new CampusNode("node_corridor_canteen", "Corridor outside Canteen", new Vector3(6.5f, 0, 19f), CampusNodeType.Corridor, DestinationCategory.Amenities, true, 0, "Corridor junction outside Campus Bistro & Canteen entrance."));
            campusGraph.AddNode(new CampusNode("node_door_canteen", "Campus Bistro Doorway", new Vector3(10.0f, 0, 19f), CampusNodeType.Corridor, DestinationCategory.Amenities, true, 0, "Designated entrance doorway for Campus Bistro & Canteen."));
            campusGraph.AddNode(new CampusNode("node_canteen", "Campus Bistro & Canteen", new Vector3(15f, 0, 19f), CampusNodeType.Canteen, DestinationCategory.Amenities, true, 0, "Student dining hall, coffee bar, and food service counter."));

            campusGraph.AddNode(new CampusNode("node_corridor_library", "Corridor outside Library", new Vector3(6.5f, 0, 39f), CampusNodeType.Corridor, DestinationCategory.Library, true, 0, "Corridor junction outside Central Library entrance."));
            campusGraph.AddNode(new CampusNode("node_door_library", "Central Library Doorway", new Vector3(10.0f, 0, 39f), CampusNodeType.Corridor, DestinationCategory.Library, true, 0, "Designated entrance doorway for Campus Central Library."));
            campusGraph.AddNode(new CampusNode("node_library", "Campus Central Library", new Vector3(15f, 0, 39f), CampusNodeType.Library, DestinationCategory.Library, true, 0, "Quiet study zone, research collections, and study carrels."));

            // Southeast Service Bypass
            campusGraph.AddNode(new CampusNode("node_bypass_east", "Southeast Service Bypass", new Vector3(6.5f, 0, 12f), CampusNodeType.Corridor, DestinationCategory.All, true, 0, "Alternate walkway connecting Lobby to East Wing."));

            // Elevated Mezzanine Terrace & Vertical Transitions
            campusGraph.AddNode(new CampusNode("node_stairs_bottom", "Terrace Stairs [Steps]", new Vector3(2.5f, 0, 42f), CampusNodeType.Stairs, DestinationCategory.Amenities, false, 0, "Stairwell to elevated observation terrace (not wheelchair accessible)."));
            campusGraph.AddNode(new CampusNode("node_ramp_bottom", "Terrace Accessible Ramp", new Vector3(-2.5f, 0, 42f), CampusNodeType.Ramp, DestinationCategory.Amenities, true, 0, "Gentle ADA-compliant ramp to terrace (fully step-free)."));
            campusGraph.AddNode(new CampusNode("node_terrace_mezzanine", "Observation Terrace & Study Deck", new Vector3(0, 1.8f, 52f), CampusNodeType.Terrace, DestinationCategory.Amenities, true, 1, "Elevated outdoor study deck overlooking campus."));

            // 2. Edges (Strictly through Walkable Open Spaces & Doorways)
            // Plaza & Lobby
            campusGraph.AddEdge(new CampusEdge("node_entrance", "node_kiosk", 13.3f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_kiosk", "node_lobby_reception", 11.4f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_entrance", "node_lobby_reception", 24.0f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_lobby_reception", "node_central_junction", 18.0f, true, true));

            // West Academic Wing: Spine -> Junction -> Corridor -> Doorway -> Room Interior
            campusGraph.AddEdge(new CampusEdge("node_central_junction", "node_academic_hallway", 6.5f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_academic_hallway", "node_corridor_class101", 6.0f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_corridor_class101", "node_door_class101", 3.5f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_door_class101", "node_class101", 5.0f, true, true));

            campusGraph.AddEdge(new CampusEdge("node_academic_hallway", "node_corridor_class102", 6.0f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_corridor_class102", "node_door_class102", 3.5f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_door_class102", "node_class102", 5.0f, true, true));

            campusGraph.AddEdge(new CampusEdge("node_corridor_class102", "node_corridor_lab204", 14.0f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_corridor_lab204", "node_door_lab204", 3.5f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_door_lab204", "node_lab204", 5.0f, true, true));

            // Southwest Bypass (Lobby -> Bypass -> Corridor outside Room 101)
            campusGraph.AddEdge(new CampusEdge("node_lobby_reception", "node_bypass_west", 8.8f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_bypass_west", "node_corridor_class101", 6.0f, true, true));

            // East Wing: Spine -> Junction -> Corridor -> Doorway -> Room Interior
            campusGraph.AddEdge(new CampusEdge("node_central_junction", "node_east_junction", 6.5f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_east_junction", "node_corridor_canteen", 5.0f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_corridor_canteen", "node_door_canteen", 3.5f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_door_canteen", "node_canteen", 5.0f, true, true));

            campusGraph.AddEdge(new CampusEdge("node_east_junction", "node_corridor_library", 15.0f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_corridor_library", "node_door_library", 3.5f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_door_library", "node_library", 5.0f, true, true));

            // Southeast Bypass (Lobby -> Bypass -> Corridor outside Canteen)
            campusGraph.AddEdge(new CampusEdge("node_lobby_reception", "node_bypass_east", 8.8f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_bypass_east", "node_corridor_canteen", 7.0f, true, true));

            // North Hallway & Terrace
            campusGraph.AddEdge(new CampusEdge("node_central_junction", "node_north_hallway", 16.0f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_north_hallway", "node_stairs_bottom", 3.2f, true, true));
            campusGraph.AddEdge(new CampusEdge("node_north_hallway", "node_ramp_bottom", 3.2f, true, true));

            // Stairs to Terrace (isAccessible = false: Stair-only!)
            campusGraph.AddEdge(new CampusEdge("node_stairs_bottom", "node_terrace_mezzanine", 10.4f, true, false));

            // Ramp to Terrace (isAccessible = true: Step-Free!)
            campusGraph.AddEdge(new CampusEdge("node_ramp_bottom", "node_terrace_mezzanine", 10.4f, true, true));

            campusGraph.RebuildLookup();
            Debug.Log($"[SmartCampusManager] Campus Navigation Graph initialized with {campusGraph.Nodes.Count} nodes and {campusGraph.Edges.Count} edges.");
        }
    }
}
