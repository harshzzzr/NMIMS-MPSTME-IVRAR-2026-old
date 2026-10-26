using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SmartCampus.Navigation;

namespace SmartCampus.UI
{
    public class DestinationMenuUI : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private RouteManager routeManager;
        [SerializeField] private CampusGraph campusGraph;
        [SerializeField] private Canvas canvas;

        [Header("UI Panels & Headers")]
        [SerializeField] private Text headerTitleText;
        [SerializeField] private Text statusBannerText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Toggle stepFreeToggle;

        [Header("Category Filter Buttons")]
        [SerializeField] private Button filterAllButton;
        [SerializeField] private Button filterAcademicButton;
        [SerializeField] private Button filterLabsButton;
        [SerializeField] private Button filterLibraryButton;
        [SerializeField] private Button filterAmenitiesButton;

        [Header("Destination List")]
        [SerializeField] private Transform destinationListContainer;
        [SerializeField] private GameObject destinationItemTemplate;

        [Header("Selected Destination Details View")]
        [SerializeField] private GameObject detailsPanel;
        [SerializeField] private Text selectedTitleText;
        [SerializeField] private Text selectedCategoryText;
        [SerializeField] private Text selectedFloorText;
        [SerializeField] private Text selectedDescriptionText;
        [SerializeField] private Text estimatedDistanceText;
        [SerializeField] private Text estimatedTimeText;
        [SerializeField] private Text estimatedDistanceTimeText;
        [SerializeField] private Button startNavigationButton;
        [SerializeField] private Button cancelNavigationButton;

        [Header("Simulation & Test Panel")]
        [SerializeField] private Toggle corridorClosureToggle;
        [SerializeField] private Toggle crowdCongestionToggle;

        private DestinationCategory currentFilter = DestinationCategory.All;
        private CampusNode selectedNode = null;
        private readonly List<GameObject> spawnedItems = new List<GameObject>();

        private void Awake()
        {
            if (canvas == null) canvas = GetComponent<Canvas>();
            if (routeManager == null) routeManager = FindAnyObjectByType<RouteManager>();
            if (campusGraph == null) campusGraph = FindAnyObjectByType<CampusGraph>();

            EnsureDetailsPanelLayout();
            WireButtons();
        }

        private void Start()
        {
            if (routeManager != null)
            {
                routeManager.OnRouteStarted += HandleRouteStarted;
                routeManager.OnRouteRecalculated += HandleRouteRecalculated;
                routeManager.OnRouteCancelled += HandleRouteCancelled;
                routeManager.OnDestinationReached += HandleDestinationReached;
            }

            if (stepFreeToggle != null)
            {
                stepFreeToggle.onValueChanged.AddListener(OnStepFreeToggleChanged);
            }

            if (corridorClosureToggle != null)
            {
                corridorClosureToggle.onValueChanged.AddListener(OnCorridorClosureToggled);
            }

            if (crowdCongestionToggle != null)
            {
                crowdCongestionToggle.onValueChanged.AddListener(OnCrowdCongestionToggled);
            }

            // Hide template item
            if (destinationItemTemplate != null)
            {
                destinationItemTemplate.SetActive(false);
            }

            RefreshDestinationList();
        }

        private void WireButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(CloseMenu);
                closeButton.onClick.AddListener(CloseMenu);
            }
            if (startNavigationButton != null)
            {
                startNavigationButton.onClick.RemoveListener(OnStartNavigationClicked);
                startNavigationButton.onClick.AddListener(OnStartNavigationClicked);
            }
            if (cancelNavigationButton != null)
            {
                cancelNavigationButton.onClick.RemoveListener(OnCancelNavigationClicked);
                cancelNavigationButton.onClick.AddListener(OnCancelNavigationClicked);
            }

            if (filterAllButton != null)
            {
                filterAllButton.onClick.RemoveAllListeners();
                filterAllButton.onClick.AddListener(() => SetFilter(DestinationCategory.All));
            }
            if (filterAcademicButton != null)
            {
                filterAcademicButton.onClick.RemoveAllListeners();
                filterAcademicButton.onClick.AddListener(() => SetFilter(DestinationCategory.Academic));
            }
            if (filterLabsButton != null)
            {
                filterLabsButton.onClick.RemoveAllListeners();
                filterLabsButton.onClick.AddListener(() => SetFilter(DestinationCategory.Labs));
            }
            if (filterLibraryButton != null)
            {
                filterLibraryButton.onClick.RemoveAllListeners();
                filterLibraryButton.onClick.AddListener(() => SetFilter(DestinationCategory.Library));
            }
            if (filterAmenitiesButton != null)
            {
                filterAmenitiesButton.onClick.RemoveAllListeners();
                filterAmenitiesButton.onClick.AddListener(() => SetFilter(DestinationCategory.Amenities));
            }
        }

        private void Update()
        {
            bool toggleRequested = false;

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.hKey.wasPressedThisFrame)
            {
                toggleRequested = true;
            }
#endif

            if (OVRInput.GetDown(OVRInput.Button.Start) || OVRInput.GetDown(OVRInput.Button.Two) || OVRInput.GetDown(OVRInput.Button.Four))
            {
                toggleRequested = true;
            }

            if (toggleRequested)
            {
                Debug.Log("[MENU UI] Toggle requested via shortcut while menu is active -> Closing menu.");
                CloseMenu();
            }
        }

        public void OpenMenuInFrontOfPlayer(Transform hmdTransform, float distance = 1.8f)
        {
            if (hmdTransform != null)
            {
                Vector3 targetPos = hmdTransform.position + hmdTransform.forward * distance;
                targetPos.y = Mathf.Max(1.2f, hmdTransform.position.y); // Eye level
                Quaternion targetRot = Quaternion.LookRotation(hmdTransform.forward, Vector3.up);

                OpenAtLocation(targetPos, targetRot);
            }
            else
            {
                gameObject.SetActive(true);
            }
        }

        public void OpenAtLocation(Vector3 position, Quaternion rotation)   
        {
            Debug.Log($"[MENU UI] DestinationMenu_Canvas opening at Pos={position}, Rot={rotation.eulerAngles}");
            transform.position = position;
            transform.rotation = rotation;
            transform.localScale = Vector3.one * 0.0022f; // Ensure standard VR world scale

            if (canvas == null) canvas = GetComponent<Canvas>();
            if (canvas != null)
            {
                if (canvas.worldCamera == null)
                {
                    var eye = GameObject.Find("CenterEyeAnchor");
                    if (eye != null) canvas.worldCamera = eye.GetComponent<Camera>();
                    if (canvas.worldCamera == null) canvas.worldCamera = Camera.main;
                }
                canvas.enabled = true;
            }

            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = true;

            // Ensure child panels are active
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(true);
            }

            EnsureDetailsPanelLayout();
            SmartCampus.Interaction.NavigationKioskTerminal.NotifyRemoteKioskOpened();

            gameObject.SetActive(true);

            // Ensure destinationListContainer is resolved if unassigned
            if (destinationListContainer == null)
            {
                var cont = transform.Find("Menu_DestListContainer");
                if (cont != null) destinationListContainer = cont;
            }

            RefreshDestinationList();

            if (selectedNode != null)
            {
                UpdateDetailsPanel(selectedNode);
            }

            if (statusBannerText != null)
            {
                statusBannerText.text = "Select a destination to calculate route.";
            }
        }

        public void CloseMenu()
        {
            Debug.Log("[MENU UI] CloseMenu() called.");
            gameObject.SetActive(false);
        }

        public void SetFilter(DestinationCategory category)
        {
            currentFilter = category;
            RefreshDestinationList();
        }

        public void RefreshDestinationList()
        {
            if (campusGraph == null) campusGraph = FindAnyObjectByType<CampusGraph>();
            Debug.Log($"[MENU UI] RefreshDestinationList: graph={(campusGraph != null)}, container={(destinationListContainer != null)}");
            if (campusGraph == null || destinationListContainer == null) return;

            // Clear previous buttons (destroy existing children to prevent duplicates)
            for (int i = destinationListContainer.childCount - 1; i >= 0; i--)
            {
                Transform child = destinationListContainer.GetChild(i);
                if (child.gameObject == destinationItemTemplate) continue;
                if (Application.isPlaying)
                    Destroy(child.gameObject);
                else
                    DestroyImmediate(child.gameObject);
            }
            spawnedItems.Clear();

            List<CampusNode> destinations = campusGraph.GetDestinations(currentFilter);

            for (int i = 0; i < destinations.Count; i++)
            {
                CampusNode node = destinations[i];
                GameObject itemObj = null;

                if (destinationItemTemplate != null)
                {
                    itemObj = Instantiate(destinationItemTemplate, destinationListContainer);
                }
                else
                {
                    itemObj = CreateProceduralListItem(destinationListContainer);
                }

                itemObj.name = $"Item_{node.nodeId}";
                itemObj.SetActive(true);

                // Set button text
                Text btnText = itemObj.GetComponentInChildren<Text>();
                if (btnText != null)
                {
                    btnText.text = $"{node.displayName}  [Fl.{node.floorLevel}]";
                }

                Button btn = itemObj.GetComponent<Button>() ?? itemObj.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    string capturedId = node.nodeId;
                    btn.onClick.AddListener(() => SelectDestination(capturedId));
                }

                spawnedItems.Add(itemObj);
            }

            // Auto-select first destination if none selected
            if (destinations.Count > 0 && selectedNode == null)
            {
                SelectDestination(destinations[0].nodeId);
            }
        }

        public void SelectDestination(string nodeId)
        {
            if (campusGraph == null) campusGraph = FindAnyObjectByType<CampusGraph>();
            if (campusGraph == null) return;

            selectedNode = campusGraph.GetNode(nodeId);
            if (selectedNode == null) return;

            UpdateDetailsPanel(selectedNode);
        }

        private RectTransform EnsureUIChild(Transform parent, string childName, ref Text textComponent, Font defaultFont, int fontSize, Color color, FontStyle fontStyle = FontStyle.Normal)
        {
            if (textComponent != null && textComponent.GetComponent<RectTransform>() == null)
            {
                textComponent = null;
            }

            Transform child = parent.Find(childName);
            // In Unity, a GameObject has either Transform or RectTransform.
            // If child exists but has a standard Transform, AddComponent<RectTransform> fails with MissingComponentException.
            // Destroy the invalid Transform GameObject and recreate it as a proper UI GameObject.
            if (child != null && child.GetComponent<RectTransform>() == null)
            {
                GameObject oldObj = child.gameObject;
                oldObj.name = childName + "_DEPRECATED_NON_UI";
                oldObj.SetActive(false);
                oldObj.transform.SetParent(null, false);
                var oldText = oldObj.GetComponent<Text>();
                if (oldText != null)
                {
                    if (Application.isPlaying) Destroy(oldText);
                    else DestroyImmediate(oldText);
                }
                if (Application.isPlaying)
                    Destroy(oldObj);
                else
                    DestroyImmediate(oldObj);
                child = null;
                textComponent = null;
            }

            if (child == null)
            {
                var go = new GameObject(childName, typeof(RectTransform), typeof(Text));
                go.transform.SetParent(parent, false);
                child = go.transform;
            }

            RectTransform rt = child.GetComponent<RectTransform>();
            if (textComponent == null)
            {
                textComponent = child.GetComponent<Text>() ?? child.gameObject.AddComponent<Text>();
            }

            if (textComponent != null)
            {
                textComponent.font = defaultFont;
                textComponent.fontSize = fontSize;
                textComponent.fontStyle = fontStyle;
                textComponent.alignment = TextAnchor.MiddleLeft;
                textComponent.color = color;
            }

            return rt;
        }

        public void EnsureDetailsPanelLayout()
        {
            if (detailsPanel == null)
            {
                var panelTrans = transform.Find("Menu_DetailsPanel");
                if (panelTrans != null) detailsPanel = panelTrans.gameObject;
            }

            if (detailsPanel == null) return;

            Font defaultFont = (selectedTitleText != null && selectedTitleText.font != null)
                ? selectedTitleText.font
                : (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf"));

            // 1. Destination Title
            RectTransform titleRt = EnsureUIChild(detailsPanel.transform, "Detail_Title", ref selectedTitleText, defaultFont, 22, new Color(0.2f, 1f, 0.7f, 1f), FontStyle.Bold);
            titleRt.anchorMin = new Vector2(0.04f, 0.87f);
            titleRt.anchorMax = new Vector2(0.96f, 0.98f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            selectedTitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            selectedTitleText.verticalOverflow = VerticalWrapMode.Truncate;

            // 2. Category
            RectTransform catRt = EnsureUIChild(detailsPanel.transform, "Detail_Category", ref selectedCategoryText, defaultFont, 16, new Color(0.45f, 0.85f, 1f, 1f), FontStyle.Bold);
            catRt.anchorMin = new Vector2(0.04f, 0.79f);
            catRt.anchorMax = new Vector2(0.96f, 0.86f);
            catRt.offsetMin = Vector2.zero;
            catRt.offsetMax = Vector2.zero;
            selectedCategoryText.horizontalOverflow = HorizontalWrapMode.Wrap;
            selectedCategoryText.verticalOverflow = VerticalWrapMode.Truncate;

            // 3. Floor
            RectTransform floorRt = EnsureUIChild(detailsPanel.transform, "Detail_Floor", ref selectedFloorText, defaultFont, 15, new Color(0.75f, 0.9f, 1f, 1f), FontStyle.Normal);
            floorRt.anchorMin = new Vector2(0.04f, 0.71f);
            floorRt.anchorMax = new Vector2(0.96f, 0.78f);
            floorRt.offsetMin = Vector2.zero;
            floorRt.offsetMax = Vector2.zero;
            selectedFloorText.horizontalOverflow = HorizontalWrapMode.Wrap;
            selectedFloorText.verticalOverflow = VerticalWrapMode.Truncate;

            // 4. Description
            RectTransform descRt = EnsureUIChild(detailsPanel.transform, "Detail_Desc", ref selectedDescriptionText, defaultFont, 15, new Color(0.95f, 0.95f, 0.95f, 1f), FontStyle.Normal);
            descRt.anchorMin = new Vector2(0.04f, 0.46f);
            descRt.anchorMax = new Vector2(0.96f, 0.69f);
            descRt.offsetMin = Vector2.zero;
            descRt.offsetMax = Vector2.zero;
            selectedDescriptionText.lineSpacing = 1.15f;
            selectedDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            selectedDescriptionText.verticalOverflow = VerticalWrapMode.Truncate;

            // 5. Estimated Distance
            RectTransform distRt = EnsureUIChild(detailsPanel.transform, "Detail_Distance", ref estimatedDistanceText, defaultFont, 16, new Color(1f, 0.85f, 0.25f, 1f), FontStyle.Bold);
            distRt.anchorMin = new Vector2(0.04f, 0.38f);
            distRt.anchorMax = new Vector2(0.96f, 0.44f);
            distRt.offsetMin = Vector2.zero;
            distRt.offsetMax = Vector2.zero;

            // 6. Walking Time
            RectTransform timeRt = EnsureUIChild(detailsPanel.transform, "Detail_WalkTime", ref estimatedTimeText, defaultFont, 16, new Color(0.2f, 0.95f, 1f, 1f), FontStyle.Bold);
            timeRt.anchorMin = new Vector2(0.04f, 0.31f);
            timeRt.anchorMax = new Vector2(0.96f, 0.37f);
            timeRt.offsetMin = Vector2.zero;
            timeRt.offsetMax = Vector2.zero;

            // 7. Buttons: Start Navigation & Cancel Navigation
            if (startNavigationButton == null)
            {
                var sb = detailsPanel.transform.Find("Btn_StartNavigation");
                if (sb != null) startNavigationButton = sb.GetComponent<Button>();
            }
            if (startNavigationButton != null)
            {
                RectTransform rt = startNavigationButton.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.04f, 0.16f);
                rt.anchorMax = new Vector2(0.48f, 0.28f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var img = startNavigationButton.GetComponent<Image>();
                if (img != null) img.color = new Color(0.08f, 0.65f, 0.35f, 1f);

                Text txt = startNavigationButton.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    txt.fontSize = 16;
                    txt.fontStyle = FontStyle.Bold;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = Color.white;
                }
            }

            if (cancelNavigationButton == null)
            {
                var cb = detailsPanel.transform.Find("Btn_CancelNavigation");
                if (cb != null) cancelNavigationButton = cb.GetComponent<Button>();
            }
            if (cancelNavigationButton != null)
            {
                RectTransform rt = cancelNavigationButton.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.52f, 0.16f);
                rt.anchorMax = new Vector2(0.96f, 0.28f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                var img = cancelNavigationButton.GetComponent<Image>();
                if (img != null) img.color = new Color(0.75f, 0.2f, 0.2f, 1f);

                Text txt = cancelNavigationButton.GetComponentInChildren<Text>();
                if (txt != null)
                {
                    txt.fontSize = 15;
                    txt.fontStyle = FontStyle.Bold;
                    txt.alignment = TextAnchor.MiddleCenter;
                    txt.color = Color.white;
                }
            }

            // 8. Toggles: StepFree, Closure & Crowd Sim
            Transform stepFreeDetails = detailsPanel.transform.Find("Toggle_StepFree");
            if (stepFreeDetails != null)
            {
                if (stepFreeToggle == null) stepFreeToggle = stepFreeDetails.GetComponent<Toggle>();
                RectTransform rt = stepFreeDetails.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.04f, 0.11f);
                    rt.anchorMax = new Vector2(0.96f, 0.17f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
            }

            if (corridorClosureToggle == null)
            {
                var t = detailsPanel.transform.Find("Toggle_ClosureSim");
                if (t != null) corridorClosureToggle = t.GetComponent<Toggle>();
            }
            if (corridorClosureToggle != null)
            {
                RectTransform rt = corridorClosureToggle.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.04f, 0.03f);
                rt.anchorMax = new Vector2(0.48f, 0.10f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            if (crowdCongestionToggle == null)
            {
                var t = detailsPanel.transform.Find("Toggle_CrowdSim") ?? detailsPanel.transform.Find("Toggle_CrowdCongestion");
                if (t != null) crowdCongestionToggle = t.GetComponent<Toggle>();
            }
            if (crowdCongestionToggle != null)
            {
                RectTransform rt = crowdCongestionToggle.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.52f, 0.03f);
                rt.anchorMax = new Vector2(0.96f, 0.10f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            // 9. Non-overlapping container arrangement
            Transform listTrans = transform.Find("Menu_DestListContainer");
            if (listTrans != null)
            {
                RectTransform listRt = listTrans.GetComponent<RectTransform>();
                listRt.anchorMin = new Vector2(0.02f, 0.08f);
                listRt.anchorMax = new Vector2(0.49f, 0.71f);
                listRt.offsetMin = Vector2.zero;
                listRt.offsetMax = Vector2.zero;

                var vlg = listTrans.GetComponent<VerticalLayoutGroup>();
                if (vlg == null) vlg = listTrans.gameObject.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 6f;
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                vlg.padding = new RectOffset(6, 6, 6, 6);
            }

            Transform catBar = transform.Find("Menu_CategoryBar");
            if (catBar != null)
            {
                RectTransform catBarRt = catBar.GetComponent<RectTransform>();
                catBarRt.anchorMin = new Vector2(0.02f, 0.73f);
                catBarRt.anchorMax = new Vector2(0.98f, 0.83f);
                catBarRt.offsetMin = Vector2.zero;
                catBarRt.offsetMax = Vector2.zero;
            }

            RectTransform detailsRt = detailsPanel.GetComponent<RectTransform>();
            if (detailsRt != null)
            {
                detailsRt.anchorMin = new Vector2(0.51f, 0.08f);
                detailsRt.anchorMax = new Vector2(0.98f, 0.71f);
                detailsRt.offsetMin = Vector2.zero;
                detailsRt.offsetMax = Vector2.zero;
            }

            Transform headerTrans = transform.Find("Menu_Header");
            if (headerTrans != null)
            {
                RectTransform hRt = headerTrans.GetComponent<RectTransform>();
                hRt.anchorMin = new Vector2(0.02f, 0.85f);
                hRt.anchorMax = new Vector2(0.98f, 0.98f);
                hRt.offsetMin = Vector2.zero;
                hRt.offsetMax = Vector2.zero;
            }

            Transform statusTrans = transform.Find("Menu_StatusBanner");
            if (statusTrans != null)
            {
                RectTransform sRt = statusTrans.GetComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0.02f, 0.01f);
                sRt.anchorMax = new Vector2(0.98f, 0.07f);
                sRt.offsetMin = Vector2.zero;
                sRt.offsetMax = Vector2.zero;
            }
        }

                private string GetFloorDisplayName(int floor)
        {
            switch (floor)
            {
                case 0: return "Ground Floor";
                case 1: return "First Floor";
                case 2: return "Second Floor";
                default: return $"Floor {floor}";
            }
        }

        private void UpdateDetailsPanel(CampusNode node)
        {
            if (node == null) return;
            selectedNode = node;

            EnsureDetailsPanelLayout();

            if (selectedTitleText != null) selectedTitleText.text = node.displayName;
            if (selectedCategoryText != null) selectedCategoryText.text = $"Category: {node.category}";
            if (selectedFloorText != null) selectedFloorText.text = $"Floor: {GetFloorDisplayName(node.floorLevel)} (Fl.{node.floorLevel})";
            if (selectedDescriptionText != null) selectedDescriptionText.text = string.IsNullOrEmpty(node.description) ? "Campus facility and learning space." : node.description;

            // Pre-calculate distance and walking time from player's current location
            if (routeManager == null) routeManager = FindAnyObjectByType<RouteManager>();
            if (campusGraph == null) campusGraph = FindAnyObjectByType<CampusGraph>();

            if (campusGraph != null)
            {
                var tracker = FindAnyObjectByType<PlayerLocationTracker>();
                Vector3 playerPos = tracker != null ? tracker.CurrentGroundPosition : Vector3.zero;
                bool stepFree = stepFreeToggle != null && stepFreeToggle.isOn;
                var startNode = campusGraph.FindNearestNode(playerPos, stepFree) ?? campusGraph.FindNearestNode(playerPos, false);

                if (startNode != null)
                {
                    var preview = AStarPathfinder.FindRoute(campusGraph, startNode.nodeId, node.nodeId, stepFree);
                    if (preview.isSuccess)
                    {
                        if (estimatedDistanceText != null)
                            estimatedDistanceText.text = $"Est. Distance: {preview.totalDistance:F0} m";
                        if (estimatedTimeText != null)
                            estimatedTimeText.text = $"Est. Walk Time: {preview.GetFormattedTime()}";
                        if (estimatedDistanceTimeText != null)
                            estimatedDistanceTimeText.text = $"Est. Distance: {preview.totalDistance:F0}m | Walk Time: {preview.GetFormattedTime()}";
                    }
                    else
                    {
                        if (estimatedDistanceText != null)
                            estimatedDistanceText.text = "Est. Distance: Route pending";
                        if (estimatedTimeText != null)
                            estimatedTimeText.text = "Est. Walk Time: --";
                        if (estimatedDistanceTimeText != null)
                            estimatedDistanceTimeText.text = "No direct route available.";
                    }
                }
            }

            if (startNavigationButton != null)
            {
                startNavigationButton.interactable = true;
            }
        }


        private void OnStartNavigationClicked()
        {
            Debug.Log("[NAV DEBUG] Start Navigation button callback reached.");

            if (selectedNode == null)
            {
                if (campusGraph == null) campusGraph = FindAnyObjectByType<CampusGraph>();
                if (campusGraph != null)
                {
                    var list = campusGraph.GetDestinations(currentFilter);
                    if (list != null && list.Count > 0)
                    {
                        SelectDestination(list[0].nodeId);
                    }
                }
            }

            if (selectedNode == null)
            {
                Debug.LogError("[NAV DEBUG] Start Navigation failed: No destination selected.");
                if (statusBannerText != null)
                    statusBannerText.text = "Please select a destination from the list first.";
                return;
            }

            Debug.Log($"[NAV DEBUG] Selected destination: '{selectedNode.displayName}' ({selectedNode.nodeId}).");

            if (routeManager == null)
                routeManager = FindAnyObjectByType<RouteManager>();

            if (routeManager == null)
            {
                Debug.LogError("[NAV DEBUG] Start Navigation failed: RouteManager component not found.");
                if (statusBannerText != null)
                    statusBannerText.text = "Error: RouteManager missing from scene.";
                return;
            }

            bool stepFree = stepFreeToggle != null && stepFreeToggle.isOn;
            Debug.Log($"[NAV DEBUG] Calling routeManager.StartNavigation('{selectedNode.nodeId}', stepFree={stepFree}).");

            bool started = routeManager.StartNavigation(selectedNode.nodeId, stepFree);
            Debug.Log($"[NAV DEBUG] routeManager.StartNavigation returned: {started}");

            if (started)
            {
                Debug.Log($"[MENU UI] Navigation successfully started to '{selectedNode.displayName}'. Closing menu.");
                if (statusBannerText != null)
                    statusBannerText.text = $"Navigating to {selectedNode.displayName} - Follow the floor arrows!";

                CloseMenu();
            }
            else
            {
                Debug.LogError($"[NAV DEBUG] RouteManager could not start navigation to '{selectedNode.displayName}'.");
                if (statusBannerText != null)
                    statusBannerText.text = $"Could not find a route to {selectedNode.displayName}. Check corridor access.";
            }
        }

        private void OnCancelNavigationClicked()
        {
            if (routeManager == null) routeManager = FindAnyObjectByType<RouteManager>();
            if (routeManager != null)
            {
                routeManager.CancelNavigation();
            }
            if (statusBannerText != null)
            {
                statusBannerText.text = "Navigation cancelled / reset.";
            }
        }

        private void OnStepFreeToggleChanged(bool isOn)
        {
            if (routeManager != null)
            {
                routeManager.SetStepFree(isOn);
            }
            if (selectedNode != null)
            {
                UpdateDetailsPanel(selectedNode);
            }
        }

        private void OnCorridorClosureToggled(bool isClosed)
        {
            if (campusGraph != null)
            {
                // Close primary academic hallway between junction and lab/classes
                campusGraph.SetEdgeClosed("node_central_junction", "node_academic_hallway", isClosed);
                campusGraph.SetEdgeClosed("node_academic_hallway", "node_central_junction", isClosed);
                Debug.Log($"[DestinationMenuUI] Simulation: Main Academic Hallway closure set to {isClosed}.");

                if (routeManager != null && routeManager.HasActiveRoute)
                {
                    // Trigger dynamic recalculation
                    routeManager.StartNavigation(routeManager.ActiveDestinationId, stepFreeToggle != null && stepFreeToggle.isOn);
                }
            }
        }

        private void OnCrowdCongestionToggled(bool isCongested)
        {
            if (campusGraph != null)
            {
                float penalty = isCongested ? 40f : 0f; // 40m equivalent delay
                campusGraph.SetCrowdPenalty("node_central_junction", "node_academic_hallway", penalty);
                campusGraph.SetCrowdPenalty("node_academic_hallway", "node_central_junction", penalty);
                Debug.Log($"[DestinationMenuUI] Simulation: Crowd congestion penalty set to {penalty}m.");

                if (routeManager != null && routeManager.HasActiveRoute)
                {
                    routeManager.StartNavigation(routeManager.ActiveDestinationId, stepFreeToggle != null && stepFreeToggle.isOn);
                }
            }
        }

        private void HandleRouteStarted(RouteResult route, List<RouteStep> steps)
        {
            if (statusBannerText != null && route != null)
            {
                statusBannerText.text = $"Route Active: {route.totalDistance:F0}m ({route.GetFormattedTime()})";
            }
        }

        private void HandleRouteRecalculated(RouteResult route)
        {
            if (statusBannerText != null && route != null)
            {
                statusBannerText.text = $"Route Recalculated! {route.totalDistance:F0}m ({route.GetFormattedTime()})";
            }
        }

        private void HandleRouteCancelled()
        {
            if (statusBannerText != null)
            {
                statusBannerText.text = "Navigation cancelled.";
            }
        }

        private void HandleDestinationReached(string destName)
        {
            if (statusBannerText != null)
            {
                statusBannerText.text = $"You have arrived at {destName}!";
            }
        }

        private GameObject CreateProceduralListItem(Transform parent)
        {
            var btnObj = new GameObject("DestItem");
            btnObj.transform.SetParent(parent, false);

            var rect = btnObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(280, 42);

            var le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 42;
            le.preferredHeight = 42;
            le.flexibleWidth = 1;

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.12f, 0.16f, 0.22f, 0.95f);

            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.2f, 0.45f, 0.75f, 1f);
            colors.pressedColor = new Color(0.1f, 0.8f, 1f, 1f);
            btn.colors = colors;

            var txtObj = new GameObject("Label");
            txtObj.transform.SetParent(btnObj.transform, false);
            var txtRect = txtObj.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = new Vector2(10, 0);
            txtRect.offsetMax = new Vector2(-10, 0);

            var txt = txtObj.AddComponent<Text>();
            txt.alignment = TextAnchor.MiddleLeft;
            txt.fontSize = 18;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            return btnObj;
        }
    }
}
