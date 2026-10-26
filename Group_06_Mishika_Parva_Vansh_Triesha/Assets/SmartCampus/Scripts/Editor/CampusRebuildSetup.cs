using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Unity.AI.Navigation;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
using SmartCampus.Core;
using SmartCampus.Interaction;
using SmartCampus.Navigation;
using SmartCampus.UI;

namespace SmartCampus.Editor
{
    public static class CampusRebuildSetup
    {
        [InitializeOnLoadMethod]
        private static void CheckForRebuildRequest()
        {
            if (Application.isBatchMode) return;
            string flagPath = "rebuild_requested.flag";
            if (System.IO.File.Exists(flagPath))
            {
                try
                {
                    System.IO.File.Delete(flagPath);
                    EditorApplication.delayCall += () =>
                    {
                        SetupFullScene();
                    };
                }
                catch (System.Exception ex)
                {
                    Debug.LogError("[CampusRebuildSetup] Error checking rebuild flag: " + ex.Message);
                }
            }
        }

        private static T GetOrAdd<T>(this GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        [MenuItem("SmartCampus/Rebuild Complete Campus Scene", false, 1)]
        public static void SetupFullScene()
        {
            Debug.Log("[CampusRebuildSetup] Initiating Full SmartCampus Scene Rebuild...");

            // 1. Open or create active scene
            string scenePath = "Assets/Scenes/SampleScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 2. Ensure or create EventSystem with New Input System UI module
            SetupEventSystem();

            // 3. Ensure Master Campus Manager Object
            var campusManagerObj = GameObject.Find("SmartCampus_Master") ?? new GameObject("SmartCampus_Master");
            campusManagerObj.transform.position = Vector3.zero;

            var campusManager = campusManagerObj.GetOrAdd<SmartCampusManager>();
            var archBuilder = campusManagerObj.GetOrAdd<CampusArchitectureBuilder>();
            var graph = campusManagerObj.GetOrAdd<CampusGraph>();
            var routeManager = campusManagerObj.GetOrAdd<RouteManager>();
            var arrowManager = campusManagerObj.GetOrAdd<RouteArrowManager>();
            var locationTracker = campusManagerObj.GetOrAdd<PlayerLocationTracker>();

            // 4. Build Full Furnished Campus Architecture
            archBuilder.BuildFullCampus();

            // 5. Build Graph Nodes & Edges
            campusManager.BuildGraphData();

            // 6. Build Physical Wayfinding Kiosk Terminal
            var kioskObj = SetupKioskTerminal(campusManagerObj.transform);

            // 7. Build VR World-Space Destination Menu UI
            var menuUI = SetupWorldSpaceDestinationMenu(kioskObj.transform);

            // 7b. Build Compact Turn-by-Turn Guidance HUD Panel
            var guidanceHUD = SetupCompactGuidanceHUD(campusManagerObj.transform);

            // 8. Configure XR Player Rig & Locomotion & Pointers
            var playerRig = SetupPlayerRig();

            // 9. Bake NavMesh Surface
            SetupAndBakeNavMesh(campusManagerObj);

            // 10. Wire references
            campusManager.EnsureDependencies();
            routeManager.EnsureDependencies();

            // 11. Configure Build Settings
            UpdateBuildSettings(scenePath);

            // 12. Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // Also save a copy to Assets/SmartCampus/Scenes/SmartCampusScene.unity
            string smartCampusScenePath = "Assets/SmartCampus/Scenes/SmartCampusScene.unity";
            EditorSceneManager.SaveScene(scene, smartCampusScenePath, true);

            Debug.Log($"[CampusRebuildSetup] Full SmartCampus Rebuild Completed Successfully! Saved to {scenePath} and {smartCampusScenePath}");
        }

        private static void SetupEventSystem()
        {
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var esObj = new GameObject("EventSystem");
                es = esObj.AddComponent<EventSystem>();
            }

            var legacyModule = es.GetComponent<StandaloneInputModule>();
            if (legacyModule != null)
            {
                Object.DestroyImmediate(legacyModule);
            }

#if ENABLE_INPUT_SYSTEM
            var inputModule = es.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
            {
                inputModule = es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
#endif
        }

        private static GameObject SetupKioskTerminal(Transform parent)
        {
            var kioskObj = GameObject.Find("Kiosk_WayfindingTerminal");
            if (kioskObj == null)
            {
                kioskObj = new GameObject("Kiosk_WayfindingTerminal");
                kioskObj.transform.SetParent(parent);
            }

            // Placed at plaza near front entrance
            kioskObj.transform.position = new Vector3(3.5f, 0, -5.0f);
            kioskObj.transform.rotation = Quaternion.Euler(0, -25f, 0);

            // Kiosk pedestal & screen body
            var body = GameObject.Find("Kiosk_Pedestal") ?? GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Kiosk_Pedestal";
            body.transform.SetParent(kioskObj.transform, false);
            body.transform.localPosition = new Vector3(0, 0.7f, 0);
            body.transform.localScale = new Vector3(1.2f, 1.4f, 0.5f);
            var bodyRend = body.GetComponent<Renderer>();
            if (bodyRend != null) bodyRend.sharedMaterial = CampusMaterialPalette.DarkMetal;

            var screen = GameObject.Find("Kiosk_ScreenAngle") ?? GameObject.CreatePrimitive(PrimitiveType.Cube);
            screen.name = "Kiosk_ScreenAngle";
            screen.transform.SetParent(kioskObj.transform, false);
            screen.transform.localPosition = new Vector3(0, 1.35f, 0.05f);
            screen.transform.localRotation = Quaternion.Euler(30f, 0, 0);
            screen.transform.localScale = new Vector3(1.1f, 0.7f, 0.1f);
            var screenRend = screen.GetComponent<Renderer>();
            if (screenRend != null) screenRend.sharedMaterial = CampusMaterialPalette.SignBoard;

            var kioskCol = kioskObj.GetOrAdd<BoxCollider>();
            kioskCol.size = new Vector3(1.6f, 2.0f, 1.2f);
            kioskCol.center = new Vector3(0, 1.0f, 0);

            var kiosk = kioskObj.GetOrAdd<NavigationKioskTerminal>();

            // Proximity interaction prompt banner
            var promptObj = GameObject.Find("Kiosk_PromptHUD");
            if (promptObj == null)
            {
                promptObj = new GameObject("Kiosk_PromptHUD");
                promptObj.transform.SetParent(kioskObj.transform, false);
                promptObj.transform.localPosition = new Vector3(0, 2.2f, 0);

                var c = promptObj.AddComponent<Canvas>();
                c.renderMode = RenderMode.WorldSpace;
                var rt = promptObj.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(400, 100);
                rt.localScale = Vector3.one * 0.004f;

                var txtObj = new GameObject("PromptText");
                txtObj.transform.SetParent(promptObj.transform, false);
                var txt = txtObj.AddComponent<Text>();
                txt.text = "[Point & Click Kiosk or Press Trigger to Open Wayfinding]";
                txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                txt.fontSize = 20;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.color = new Color(0.1f, 1f, 0.5f);
                var trt = txtObj.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.sizeDelta = Vector2.zero;
            }

            return kioskObj;
        }

        private static DestinationMenuUI SetupWorldSpaceDestinationMenu(Transform kioskTransform)
        {
            var menuObj = GameObject.Find("DestinationMenu_Canvas");
            if (menuObj == null)
            {
                menuObj = new GameObject("DestinationMenu_Canvas");
            }

            menuObj.transform.position = new Vector3(3.5f, 1.45f, -4.2f);
            menuObj.transform.rotation = Quaternion.Euler(0, 155f, 0); // Facing player approaching kiosk

            var canvas = menuObj.GetOrAdd<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = menuObj.GetOrAdd<RectTransform>();
            rt.sizeDelta = new Vector2(1000, 700);
            rt.localScale = Vector3.one * 0.0022f; // Comfortable VR scale (~2.2m x 1.54m in front of player)

            var scaler = menuObj.GetOrAdd<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2;

            var raycaster = menuObj.GetOrAdd<GraphicRaycaster>();

            // Background Panel
            var bgObj = GameObject.Find("Menu_Background") ?? new GameObject("Menu_Background");
            bgObj.transform.SetParent(menuObj.transform, false);
            var bgRt = bgObj.GetOrAdd<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bgObj.GetOrAdd<Image>();
            bgImg.color = new Color(0.06f, 0.09f, 0.15f, 0.94f); // Sleek dark slate glass

            // Header Banner
            var headerObj = GameObject.Find("Menu_Header") ?? new GameObject("Menu_Header");
            headerObj.transform.SetParent(menuObj.transform, false);
            var headerRt = headerObj.GetOrAdd<RectTransform>();
            headerRt.anchorMin = new Vector2(0, 0.88f);
            headerRt.anchorMax = Vector2.one;
            headerRt.offsetMin = new Vector2(20, 0);
            headerRt.offsetMax = new Vector2(-20, -10);
            var headerImg = headerObj.GetOrAdd<Image>();
            headerImg.color = new Color(0.1f, 0.22f, 0.36f, 0.95f);

            var titleTextObj = GameObject.Find("Menu_TitleText") ?? new GameObject("Menu_TitleText");
            titleTextObj.transform.SetParent(headerObj.transform, false);
            var titleTxt = titleTextObj.GetOrAdd<Text>();
            titleTxt.text = "SMARTCAMPUS WAYFINDING DIRECTORY";
            titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleTxt.fontSize = 28;
            titleTxt.alignment = TextAnchor.MiddleLeft;
            titleTxt.color = new Color(0.2f, 0.9f, 1f);
            var titleRt = titleTextObj.GetOrAdd<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = new Vector2(20, 0);
            titleRt.offsetMax = new Vector2(-60, 0);

            // Close 'X' Button
            var closeBtnObj = GameObject.Find("Menu_CloseBtn") ?? new GameObject("Menu_CloseBtn");
            closeBtnObj.transform.SetParent(headerObj.transform, false);
            var closeRt = closeBtnObj.GetOrAdd<RectTransform>();
            closeRt.anchorMin = new Vector2(1, 0.5f);
            closeRt.anchorMax = new Vector2(1, 0.5f);
            closeRt.sizeDelta = new Vector2(50, 50);
            closeRt.anchoredPosition = new Vector2(-30, 0);
            var closeImg = closeBtnObj.GetOrAdd<Image>();
            closeImg.color = new Color(0.7f, 0.15f, 0.15f);
            var closeBtn = closeBtnObj.GetOrAdd<Button>();

            var closeTxtObj = new GameObject("CloseTxt");
            closeTxtObj.transform.SetParent(closeBtnObj.transform, false);
            var closeTxt = closeTxtObj.AddComponent<Text>();
            closeTxt.text = "X";
            closeTxt.font = titleTxt.font;
            closeTxt.fontSize = 24;
            closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.color = Color.white;
            var closeTxtRt = closeTxtObj.GetComponent<RectTransform>();
            closeTxtRt.anchorMin = Vector2.zero;
            closeTxtRt.anchorMax = Vector2.one;
            closeTxtRt.sizeDelta = Vector2.zero;

            // Status Banner below header
            var statusObj = GameObject.Find("Menu_StatusBanner") ?? new GameObject("Menu_StatusBanner");
            statusObj.transform.SetParent(menuObj.transform, false);
            var statusRt = statusObj.GetOrAdd<RectTransform>();
            statusRt.anchorMin = new Vector2(0, 0.81f);
            statusRt.anchorMax = new Vector2(1, 0.88f);
            statusRt.offsetMin = new Vector2(20, 0);
            statusRt.offsetMax = new Vector2(-20, 0);
            var statusTxt = statusObj.GetOrAdd<Text>();
            statusTxt.text = "Select a destination to calculate route.";
            statusTxt.font = titleTxt.font;
            statusTxt.fontSize = 20;
            statusTxt.alignment = TextAnchor.MiddleCenter;
            statusTxt.color = new Color(0.85f, 0.95f, 1f);

            // Left Filter Column (Category tabs)
            var catBarObj = GameObject.Find("Menu_CategoryBar") ?? new GameObject("Menu_CategoryBar");
            catBarObj.transform.SetParent(menuObj.transform, false);
            var catRt = catBarObj.GetOrAdd<RectTransform>();
            catRt.anchorMin = new Vector2(0, 0.73f);
            catRt.anchorMax = new Vector2(1, 0.80f);
            catRt.offsetMin = new Vector2(20, 0);
            catRt.offsetMax = new Vector2(-20, 0);
            var catLayout = catBarObj.GetOrAdd<HorizontalLayoutGroup>();
            catLayout.spacing = 10;
            catLayout.childForceExpandWidth = true;
            catLayout.childForceExpandHeight = true;

            Button allBtn = CreateCategoryButton(catBarObj.transform, "All", "ALL DESTINATIONS");
            Button acadBtn = CreateCategoryButton(catBarObj.transform, "Academic", "ACADEMIC WING");
            Button labBtn = CreateCategoryButton(catBarObj.transform, "Labs", "COMPUTING LABS");
            Button libBtn = CreateCategoryButton(catBarObj.transform, "Library", "LIBRARY");
            Button amenBtn = CreateCategoryButton(catBarObj.transform, "Amenities", "AMENITIES");

            // Destination List Scroll Container (Left side)
            var listObj = GameObject.Find("Menu_DestListContainer") ?? new GameObject("Menu_DestListContainer");
            listObj.transform.SetParent(menuObj.transform, false);
            var listRt = listObj.GetOrAdd<RectTransform>();
            listRt.anchorMin = new Vector2(0.02f, 0.08f);
            listRt.anchorMax = new Vector2(0.48f, 0.71f);
            listRt.offsetMin = Vector2.zero;
            listRt.offsetMax = Vector2.zero;
            var listLayout = listObj.GetOrAdd<VerticalLayoutGroup>();
            listLayout.spacing = 6;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;

            // Details Panel (Right side)
            var detailsObj = GameObject.Find("Menu_DetailsPanel") ?? new GameObject("Menu_DetailsPanel");
            detailsObj.transform.SetParent(menuObj.transform, false);
            var detailsRt = detailsObj.GetOrAdd<RectTransform>();
            detailsRt.anchorMin = new Vector2(0.51f, 0.08f);
            detailsRt.anchorMax = new Vector2(0.98f, 0.71f);
            detailsRt.offsetMin = Vector2.zero;
            detailsRt.offsetMax = Vector2.zero;
            var detailsImg = detailsObj.GetOrAdd<Image>();
            detailsImg.color = new Color(0.11f, 0.16f, 0.24f, 0.95f);

            // Details contents: Title
            var dtTitleObj = new GameObject("Detail_Title", typeof(RectTransform), typeof(Text));
            dtTitleObj.transform.SetParent(detailsObj.transform, false);
            var dtTitle = dtTitleObj.GetComponent<Text>();
            dtTitle.text = "Computer Lab 204";
            dtTitle.font = titleTxt.font;
            dtTitle.fontSize = 22;
            dtTitle.fontStyle = FontStyle.Bold;
            dtTitle.color = new Color(0.2f, 1f, 0.7f);
            var dtTitleRt = dtTitleObj.GetComponent<RectTransform>();
            dtTitleRt.anchorMin = new Vector2(0.04f, 0.87f);
            dtTitleRt.anchorMax = new Vector2(0.96f, 0.98f);
            dtTitleRt.offsetMin = Vector2.zero;
            dtTitleRt.offsetMax = Vector2.zero;

            // Category
            var dtCatObj = new GameObject("Detail_Category", typeof(RectTransform), typeof(Text));
            dtCatObj.transform.SetParent(detailsObj.transform, false);
            var dtCat = dtCatObj.GetComponent<Text>();
            dtCat.text = "Category: Computing Labs";
            dtCat.font = titleTxt.font;
            dtCat.fontSize = 16;
            dtCat.fontStyle = FontStyle.Bold;
            dtCat.color = new Color(0.45f, 0.85f, 1f);
            var dtCatRt = dtCatObj.GetComponent<RectTransform>();
            dtCatRt.anchorMin = new Vector2(0.04f, 0.79f);
            dtCatRt.anchorMax = new Vector2(0.96f, 0.86f);
            dtCatRt.offsetMin = Vector2.zero;
            dtCatRt.offsetMax = Vector2.zero;

            // Floor
            var dtFloorObj = new GameObject("Detail_Floor", typeof(RectTransform), typeof(Text));
            dtFloorObj.transform.SetParent(detailsObj.transform, false);
            var dtFloor = dtFloorObj.GetComponent<Text>();
            dtFloor.text = "Floor: Ground Floor (Fl.0)";
            dtFloor.font = titleTxt.font;
            dtFloor.fontSize = 15;
            dtFloor.color = new Color(0.75f, 0.9f, 1f);
            var dtFloorRt = dtFloorObj.GetComponent<RectTransform>();
            dtFloorRt.anchorMin = new Vector2(0.04f, 0.71f);
            dtFloorRt.anchorMax = new Vector2(0.96f, 0.78f);
            dtFloorRt.offsetMin = Vector2.zero;
            dtFloorRt.offsetMax = Vector2.zero;

            // Description
            var dtDescObj = new GameObject("Detail_Desc", typeof(RectTransform), typeof(Text));
            dtDescObj.transform.SetParent(detailsObj.transform, false);
            var dtDesc = dtDescObj.GetComponent<Text>();
            dtDesc.text = "High-performance computing lab with workstations and server racks.";
            dtDesc.font = titleTxt.font;
            dtDesc.fontSize = 15;
            dtDesc.color = new Color(0.95f, 0.95f, 0.95f);
            var dtDescRt = dtDescObj.GetComponent<RectTransform>();
            dtDescRt.anchorMin = new Vector2(0.04f, 0.46f);
            dtDescRt.anchorMax = new Vector2(0.96f, 0.69f);
            dtDescRt.offsetMin = Vector2.zero;
            dtDescRt.offsetMax = Vector2.zero;

            // Distance
            var dtDistObj = new GameObject("Detail_Distance", typeof(RectTransform), typeof(Text));
            dtDistObj.transform.SetParent(detailsObj.transform, false);
            var dtDist = dtDistObj.GetComponent<Text>();
            dtDist.text = "Est. Distance: 48 m";
            dtDist.font = titleTxt.font;
            dtDist.fontSize = 16;
            dtDist.fontStyle = FontStyle.Bold;
            dtDist.color = new Color(1f, 0.85f, 0.25f);
            var dtDistRt = dtDistObj.GetComponent<RectTransform>();
            dtDistRt.anchorMin = new Vector2(0.04f, 0.38f);
            dtDistRt.anchorMax = new Vector2(0.96f, 0.44f);
            dtDistRt.offsetMin = Vector2.zero;
            dtDistRt.offsetMax = Vector2.zero;

            // Walk Time
            var dtTimeObj = new GameObject("Detail_WalkTime", typeof(RectTransform), typeof(Text));
            dtTimeObj.transform.SetParent(detailsObj.transform, false);
            var dtTime = dtTimeObj.GetComponent<Text>();
            dtTime.text = "Est. Walk Time: 36s";
            dtTime.font = titleTxt.font;
            dtTime.fontSize = 16;
            dtTime.fontStyle = FontStyle.Bold;
            dtTime.color = new Color(0.2f, 0.95f, 1f);
            var dtTimeRt = dtTimeObj.GetComponent<RectTransform>();
            dtTimeRt.anchorMin = new Vector2(0.04f, 0.31f);
            dtTimeRt.anchorMax = new Vector2(0.96f, 0.37f);
            dtTimeRt.offsetMin = Vector2.zero;
            dtTimeRt.offsetMax = Vector2.zero;

            // Start Navigation Button
            var startBtnObj = new GameObject("Btn_StartNavigation", typeof(RectTransform), typeof(Image), typeof(Button));
            startBtnObj.transform.SetParent(detailsObj.transform, false);
            var startBtnRt = startBtnObj.GetComponent<RectTransform>();
            startBtnRt.anchorMin = new Vector2(0.04f, 0.18f);
            startBtnRt.anchorMax = new Vector2(0.48f, 0.29f);
            startBtnRt.offsetMin = Vector2.zero;
            startBtnRt.offsetMax = Vector2.zero;
            var startBtnImg = startBtnObj.GetComponent<Image>();
            startBtnImg.color = new Color(0.08f, 0.65f, 0.35f);
            var startBtn = startBtnObj.GetComponent<Button>();

            var startTxtObj = new GameObject("StartTxt", typeof(RectTransform), typeof(Text));
            startTxtObj.transform.SetParent(startBtnObj.transform, false);
            var startTxt = startTxtObj.GetComponent<Text>();
            startTxt.text = "START NAVIGATION";
            startTxt.font = titleTxt.font;
            startTxt.fontSize = 16;
            startTxt.fontStyle = FontStyle.Bold;
            startTxt.alignment = TextAnchor.MiddleCenter;
            startTxt.color = Color.white;
            var srt = startTxtObj.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.sizeDelta = Vector2.zero;

            // Cancel Navigation Button
            var cancelBtnObj = new GameObject("Btn_CancelNavigation", typeof(RectTransform), typeof(Image), typeof(Button));
            cancelBtnObj.transform.SetParent(detailsObj.transform, false);
            var cancelBtnRt = cancelBtnObj.GetComponent<RectTransform>();
            cancelBtnRt.anchorMin = new Vector2(0.52f, 0.18f);
            cancelBtnRt.anchorMax = new Vector2(0.96f, 0.29f);
            cancelBtnRt.offsetMin = Vector2.zero;
            cancelBtnRt.offsetMax = Vector2.zero;
            var cancelBtnImg = cancelBtnObj.GetComponent<Image>();
            cancelBtnImg.color = new Color(0.75f, 0.2f, 0.2f);
            var cancelBtn = cancelBtnObj.GetComponent<Button>();

            var cancelTxtObj = new GameObject("CancelTxt", typeof(RectTransform), typeof(Text));
            cancelTxtObj.transform.SetParent(cancelBtnObj.transform, false);
            var cancelTxt = cancelTxtObj.GetComponent<Text>();
            cancelTxt.text = "CANCEL / RESET";
            cancelTxt.font = titleTxt.font;
            cancelTxt.fontSize = 15;
            cancelTxt.fontStyle = FontStyle.Bold;
            cancelTxt.alignment = TextAnchor.MiddleCenter;
            cancelTxt.color = Color.white;
            var crt = cancelTxtObj.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.sizeDelta = Vector2.zero;

            // Step-Free Route Toggle
            var toggleStepObj = new GameObject("Toggle_StepFree", typeof(RectTransform), typeof(Toggle));
            toggleStepObj.transform.SetParent(detailsObj.transform, false);
            var toggleStepRt = toggleStepObj.GetComponent<RectTransform>();
            toggleStepRt.anchorMin = new Vector2(0.04f, 0.11f);
            toggleStepRt.anchorMax = new Vector2(0.96f, 0.17f);
            toggleStepRt.offsetMin = Vector2.zero;
            toggleStepRt.offsetMax = Vector2.zero;
            var stepToggle = toggleStepObj.GetComponent<Toggle>();

            var stepLabelObj = new GameObject("StepLabel", typeof(RectTransform), typeof(Text));
            stepLabelObj.transform.SetParent(toggleStepObj.transform, false);
            var stepLabel = stepLabelObj.GetComponent<Text>();
            stepLabel.text = "[x] Require Step-Free / Accessible Route (Ramps only, avoid stairs)";
            stepLabel.font = titleTxt.font;
            stepLabel.fontSize = 14;
            stepLabel.color = new Color(0.9f, 0.9f, 0.9f);
            var slrt = stepLabelObj.GetComponent<RectTransform>();
            slrt.anchorMin = Vector2.zero;
            slrt.anchorMax = Vector2.one;
            slrt.sizeDelta = Vector2.zero;

            // Corridor Closure Simulation Toggle
            var toggleClosureObj = new GameObject("Toggle_ClosureSim", typeof(RectTransform), typeof(Toggle));
            toggleClosureObj.transform.SetParent(detailsObj.transform, false);
            var toggleClosureRt = toggleClosureObj.GetComponent<RectTransform>();
            toggleClosureRt.anchorMin = new Vector2(0.04f, 0.03f);
            toggleClosureRt.anchorMax = new Vector2(0.48f, 0.10f);
            toggleClosureRt.offsetMin = Vector2.zero;
            toggleClosureRt.offsetMax = Vector2.zero;
            var closureToggle = toggleClosureObj.GetComponent<Toggle>();

            var closureLabelObj = new GameObject("ClosureLabel", typeof(RectTransform), typeof(Text));
            closureLabelObj.transform.SetParent(toggleClosureObj.transform, false);
            var closureLabel = closureLabelObj.GetComponent<Text>();
            closureLabel.text = "[Sim] Block Hallway";
            closureLabel.font = titleTxt.font;
            closureLabel.fontSize = 13;
            closureLabel.color = new Color(1f, 0.6f, 0.6f);
            var clrt = closureLabelObj.GetComponent<RectTransform>();
            clrt.anchorMin = Vector2.zero;
            clrt.anchorMax = Vector2.one;
            clrt.sizeDelta = Vector2.zero;

            // Crowd Congestion Simulation Toggle
            var toggleCrowdObj = new GameObject("Toggle_CrowdSim", typeof(RectTransform), typeof(Toggle));
            toggleCrowdObj.transform.SetParent(detailsObj.transform, false);
            var toggleCrowdRt = toggleCrowdObj.GetComponent<RectTransform>();
            toggleCrowdRt.anchorMin = new Vector2(0.52f, 0.03f);
            toggleCrowdRt.anchorMax = new Vector2(0.96f, 0.10f);
            toggleCrowdRt.offsetMin = Vector2.zero;
            toggleCrowdRt.offsetMax = Vector2.zero;
            var crowdToggle = toggleCrowdObj.GetComponent<Toggle>();

            var crowdLabelObj = new GameObject("CrowdLabel", typeof(RectTransform), typeof(Text));
            crowdLabelObj.transform.SetParent(toggleCrowdObj.transform, false);
            var crowdLabel = crowdLabelObj.GetComponent<Text>();
            crowdLabel.text = "[Sim] Crowd Delay";
            crowdLabel.font = titleTxt.font;
            crowdLabel.fontSize = 13;
            crowdLabel.color = new Color(1f, 0.85f, 0.5f);
            var cdlrt = crowdLabelObj.GetComponent<RectTransform>();
            cdlrt.anchorMin = Vector2.zero;
            cdlrt.anchorMax = Vector2.one;
            cdlrt.sizeDelta = Vector2.zero;

            // Wire everything to DestinationMenuUI
            var menuScript = menuObj.GetOrAdd<DestinationMenuUI>();

            // Assign private serialized fields via SerializedObject
            var so = new SerializedObject(menuScript);
            so.FindProperty("headerTitleText").objectReferenceValue = titleTxt;
            so.FindProperty("statusBannerText").objectReferenceValue = statusTxt;
            so.FindProperty("closeButton").objectReferenceValue = closeBtn;
            so.FindProperty("stepFreeToggle").objectReferenceValue = stepToggle;
            so.FindProperty("filterAllButton").objectReferenceValue = allBtn;
            so.FindProperty("filterAcademicButton").objectReferenceValue = acadBtn;
            so.FindProperty("filterLabsButton").objectReferenceValue = labBtn;
            so.FindProperty("filterLibraryButton").objectReferenceValue = libBtn;
            so.FindProperty("filterAmenitiesButton").objectReferenceValue = amenBtn;
            so.FindProperty("destinationListContainer").objectReferenceValue = listRt;
            so.FindProperty("detailsPanel").objectReferenceValue = detailsObj;
            so.FindProperty("selectedTitleText").objectReferenceValue = dtTitle;
            so.FindProperty("selectedCategoryText").objectReferenceValue = dtCat;
            so.FindProperty("selectedFloorText").objectReferenceValue = dtFloor;
            so.FindProperty("selectedDescriptionText").objectReferenceValue = dtDesc;
            so.FindProperty("estimatedDistanceText").objectReferenceValue = dtDist;
            so.FindProperty("estimatedTimeText").objectReferenceValue = dtTime;
            so.FindProperty("startNavigationButton").objectReferenceValue = startBtn;
            so.FindProperty("cancelNavigationButton").objectReferenceValue = cancelBtn;
            so.FindProperty("corridorClosureToggle").objectReferenceValue = closureToggle;
            so.FindProperty("crowdCongestionToggle").objectReferenceValue = crowdToggle;
            so.ApplyModifiedProperties();

            return menuScript;
        }

        private static CompactGuidancePanel SetupCompactGuidanceHUD(Transform parent)
        {
            var hudObj = GameObject.Find("CompactGuidance_HUD");
            if (hudObj == null)
            {
                hudObj = new GameObject("CompactGuidance_HUD");
                hudObj.transform.SetParent(parent);
            }

            var canvas = hudObj.GetOrAdd<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = hudObj.GetOrAdd<RectTransform>();
            rt.sizeDelta = new Vector2(440, 160);
            rt.localScale = Vector3.one * 0.0018f;

            var scaler = hudObj.GetOrAdd<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2;
            hudObj.GetOrAdd<GraphicRaycaster>();

            // Panel Background
            var bgObj = GameObject.Find("HUD_Background") ?? new GameObject("HUD_Background");
            bgObj.transform.SetParent(hudObj.transform, false);
            var bgRt = bgObj.GetOrAdd<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bgObj.GetOrAdd<Image>();
            bgImg.color = new Color(0.08f, 0.12f, 0.18f, 0.92f); // Sleek dark slate glass

            // Top Status Bar (Accent)
            var topBar = GameObject.Find("HUD_TopBar") ?? new GameObject("HUD_TopBar");
            topBar.transform.SetParent(hudObj.transform, false);
            var topRt = topBar.GetOrAdd<RectTransform>();
            topRt.anchorMin = new Vector2(0, 0.88f);
            topRt.anchorMax = Vector2.one;
            topRt.sizeDelta = Vector2.zero;
            var topImg = topBar.GetOrAdd<Image>();
            topImg.color = new Color(0.0f, 0.75f, 0.95f, 0.9f); // Cyan accent line

            // Destination Label
            var destObj = GameObject.Find("HUD_DestText") ?? new GameObject("HUD_DestText");
            destObj.transform.SetParent(hudObj.transform, false);
            var destTxt = destObj.GetOrAdd<Text>();
            destTxt.text = "NAVIGATING TO: Computer Lab 204";
            destTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            destTxt.fontSize = 18;
            destTxt.alignment = TextAnchor.MiddleLeft;
            destTxt.color = new Color(0.3f, 1f, 0.6f);
            var destRt = destObj.GetOrAdd<RectTransform>();
            destRt.anchorMin = new Vector2(0.05f, 0.65f);
            destRt.anchorMax = new Vector2(0.95f, 0.88f);
            destRt.sizeDelta = Vector2.zero;

            // Turn Icon
            var iconObj = GameObject.Find("HUD_TurnIcon") ?? new GameObject("HUD_TurnIcon");
            iconObj.transform.SetParent(hudObj.transform, false);
            var iconTxt = iconObj.GetOrAdd<Text>();
            iconTxt.text = "[ -> ]";
            iconTxt.font = destTxt.font;
            iconTxt.fontSize = 22;
            iconTxt.alignment = TextAnchor.MiddleCenter;
            iconTxt.color = new Color(1f, 0.85f, 0.2f);
            var iconRt = iconObj.GetOrAdd<RectTransform>();
            iconRt.anchorMin = new Vector2(0.04f, 0.32f);
            iconRt.anchorMax = new Vector2(0.24f, 0.65f);
            iconRt.sizeDelta = Vector2.zero;

            // Primary Instruction
            var instObj = GameObject.Find("HUD_InstructionText") ?? new GameObject("HUD_InstructionText");
            instObj.transform.SetParent(hudObj.transform, false);
            var instTxt = instObj.GetOrAdd<Text>();
            instTxt.text = "Turn right at Academic Wing junction in 12m";
            instTxt.font = destTxt.font;
            instTxt.fontSize = 16;
            instTxt.alignment = TextAnchor.MiddleLeft;
            instTxt.color = Color.white;
            var instRt = instObj.GetOrAdd<RectTransform>();
            instRt.anchorMin = new Vector2(0.26f, 0.32f);
            instRt.anchorMax = new Vector2(0.95f, 0.65f);
            instRt.sizeDelta = Vector2.zero;

            // Remaining Distance & Time
            var distObj = GameObject.Find("HUD_MetricsText") ?? new GameObject("HUD_MetricsText");
            distObj.transform.SetParent(hudObj.transform, false);
            var distTxt = distObj.GetOrAdd<Text>();
            distTxt.text = "Remaining: 34m | Walk Time: 25s";
            distTxt.font = destTxt.font;
            distTxt.fontSize = 15;
            distTxt.alignment = TextAnchor.MiddleLeft;
            distTxt.color = new Color(0.85f, 0.92f, 1f);
            var distRt = distObj.GetOrAdd<RectTransform>();
            distRt.anchorMin = new Vector2(0.05f, 0.06f);
            distRt.anchorMax = new Vector2(0.55f, 0.30f);
            distRt.sizeDelta = Vector2.zero;

            // Cancel Navigation Button (Ruby red)
            var cancelBtnObj = GameObject.Find("HUD_CancelBtn") ?? new GameObject("HUD_CancelBtn");
            cancelBtnObj.transform.SetParent(hudObj.transform, false);
            var cancelRt = cancelBtnObj.GetOrAdd<RectTransform>();
            cancelRt.anchorMin = new Vector2(0.76f, 0.05f);
            cancelRt.anchorMax = new Vector2(0.96f, 0.28f);
            cancelRt.sizeDelta = Vector2.zero;
            var cancelImg = cancelBtnObj.GetOrAdd<Image>();
            cancelImg.color = new Color(0.75f, 0.18f, 0.18f);
            var cancelBtn = cancelBtnObj.GetOrAdd<Button>();

            var cancelTxtObj = new GameObject("CancelTxt");
            cancelTxtObj.transform.SetParent(cancelBtnObj.transform, false);
            var cancelTxt = cancelTxtObj.AddComponent<Text>();
            cancelTxt.text = "CANCEL";
            cancelTxt.font = destTxt.font;
            cancelTxt.fontSize = 13;
            cancelTxt.alignment = TextAnchor.MiddleCenter;
            cancelTxt.color = Color.white;
            var crt = cancelTxtObj.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.sizeDelta = Vector2.zero;

            // Open Directory Button
            var menuBtnObj = GameObject.Find("HUD_MenuBtn") ?? new GameObject("HUD_MenuBtn");
            menuBtnObj.transform.SetParent(hudObj.transform, false);
            var menuRt = menuBtnObj.GetOrAdd<RectTransform>();
            menuRt.anchorMin = new Vector2(0.56f, 0.05f);
            menuRt.anchorMax = new Vector2(0.74f, 0.28f);
            menuRt.sizeDelta = Vector2.zero;
            var menuImg = menuBtnObj.GetOrAdd<Image>();
            menuImg.color = new Color(0.15f, 0.35f, 0.55f);
            var menuBtn = menuBtnObj.GetOrAdd<Button>();

            var menuTxtObj = new GameObject("MenuTxt");
            menuTxtObj.transform.SetParent(menuBtnObj.transform, false);
            var menuTxt = menuTxtObj.AddComponent<Text>();
            menuTxt.text = "MENU";
            menuTxt.font = destTxt.font;
            menuTxt.fontSize = 13;
            menuTxt.alignment = TextAnchor.MiddleCenter;
            menuTxt.color = Color.white;
            var mrt = menuTxtObj.GetComponent<RectTransform>();
            mrt.anchorMin = Vector2.zero;
            mrt.anchorMax = Vector2.one;
            mrt.sizeDelta = Vector2.zero;

            // Arrival Notice Overlay
            var arrivalObj = GameObject.Find("HUD_ArrivalNotice") ?? new GameObject("HUD_ArrivalNotice");
            arrivalObj.transform.SetParent(hudObj.transform, false);
            var arrivalRt = arrivalObj.GetOrAdd<RectTransform>();
            arrivalRt.anchorMin = Vector2.zero;
            arrivalRt.anchorMax = Vector2.one;
            arrivalRt.sizeDelta = Vector2.zero;
            var arrivalImg = arrivalObj.GetOrAdd<Image>();
            arrivalImg.color = new Color(0.05f, 0.25f, 0.12f, 0.98f);

            var arrTxtObj = new GameObject("ArrivalText");
            arrTxtObj.transform.SetParent(arrivalObj.transform, false);
            var arrTxt = arrTxtObj.AddComponent<Text>();
            arrTxt.text = "ARRIVED AT DESTINATION!";
            arrTxt.font = destTxt.font;
            arrTxt.fontSize = 20;
            arrTxt.alignment = TextAnchor.MiddleCenter;
            arrTxt.color = Color.white;
            var art = arrTxtObj.GetComponent<RectTransform>();
            art.anchorMin = new Vector2(0.05f, 0.35f);
            art.anchorMax = new Vector2(0.95f, 0.9f);
            art.sizeDelta = Vector2.zero;

            var newDestBtnObj = new GameObject("Btn_NewDest");
            newDestBtnObj.transform.SetParent(arrivalObj.transform, false);
            var newDestRt = newDestBtnObj.AddComponent<RectTransform>();
            newDestRt.anchorMin = new Vector2(0.2f, 0.08f);
            newDestRt.anchorMax = new Vector2(0.8f, 0.32f);
            newDestRt.sizeDelta = Vector2.zero;
            var newDestImg = newDestBtnObj.AddComponent<Image>();
            newDestImg.color = new Color(0.1f, 0.6f, 0.3f);
            var newDestBtn = newDestBtnObj.AddComponent<Button>();

            var newDestTxtObj = new GameObject("Text");
            newDestTxtObj.transform.SetParent(newDestBtnObj.transform, false);
            var newDestTxt = newDestTxtObj.AddComponent<Text>();
            newDestTxt.text = "CHOOSE NEW DESTINATION";
            newDestTxt.font = destTxt.font;
            newDestTxt.fontSize = 14;
            newDestTxt.alignment = TextAnchor.MiddleCenter;
            newDestTxt.color = Color.white;
            var ndrt = newDestTxtObj.GetComponent<RectTransform>();
            ndrt.anchorMin = Vector2.zero;
            ndrt.anchorMax = Vector2.one;
            ndrt.sizeDelta = Vector2.zero;

            arrivalObj.SetActive(false);

            // Wire to CompactGuidancePanel script
            var hudScript = hudObj.GetOrAdd<CompactGuidancePanel>();
            var so = new SerializedObject(hudScript);
            so.FindProperty("destinationText").objectReferenceValue = destTxt;
            so.FindProperty("turnIconText").objectReferenceValue = iconTxt;
            so.FindProperty("primaryInstructionText").objectReferenceValue = instTxt;
            so.FindProperty("remainingDistanceText").objectReferenceValue = distTxt;
            so.FindProperty("remainingTimeText").objectReferenceValue = distTxt;
            so.FindProperty("statusBannerText").objectReferenceValue = destTxt;
            so.FindProperty("cancelNavigationButton").objectReferenceValue = cancelBtn;
            so.FindProperty("openDirectoryButton").objectReferenceValue = menuBtn;
            so.FindProperty("arrivalNoticeContainer").objectReferenceValue = arrivalObj;
            so.FindProperty("arrivalNoticeText").objectReferenceValue = arrTxt;
            so.FindProperty("chooseNewDestinationButton").objectReferenceValue = newDestBtn;
            so.ApplyModifiedProperties();

            hudObj.SetActive(false);
            return hudScript;
        }

        private static Button CreateCategoryButton(Transform parent, string id, string label)
        {
            var btnObj = new GameObject($"Btn_{id}");
            btnObj.transform.SetParent(parent, false);

            var img = btnObj.AddComponent<Image>();
            img.color = new Color(0.15f, 0.25f, 0.38f);

            var btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.2f, 0.5f, 0.8f);
            colors.pressedColor = new Color(0.1f, 0.8f, 1f);
            btn.colors = colors;

            var txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            var txt = txtObj.AddComponent<Text>();
            txt.text = label;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.fontSize = 15;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;

            var rt = txtObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;

            return btn;
        }

        private static GameObject SetupPlayerRig()
        {
            // Find existing XR rig or camera rig
            var rig = GameObject.Find("[BuildingBlock] Camera Rig") ?? GameObject.Find("OVRCameraRig");

            if (rig == null)
            {
                if (Camera.main != null)
                {
                    rig = Camera.main.transform.root.gameObject;
                }
                else
                {
                    rig = new GameObject("XRPlayerRig");
                    var camObj = new GameObject("CenterEyeAnchor");
                    camObj.transform.SetParent(rig.transform);
                    var cam = camObj.AddComponent<Camera>();
                    cam.tag = "MainCamera";
                }
            }

            // Spawn player at Arrival Plaza facing the campus entrance
            rig.transform.position = new Vector3(0, 0, -18f);
            rig.transform.rotation = Quaternion.identity;

            // Character Controller
            var cc = rig.GetComponent<CharacterController>();
            if (cc == null)
            {
                cc = rig.AddComponent<CharacterController>();
            }
            cc.radius = 0.3f;
            cc.height = 1.75f;
            cc.center = new Vector3(0, 0.88f, 0);
            cc.stepOffset = 0.25f;
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.04f;

            // Locomotion
            var playerCtrl = rig.GetComponent<VRPlayerController>();
            if (playerCtrl == null)
            {
                playerCtrl = rig.AddComponent<VRPlayerController>();
            }
            playerCtrl.LocateRigReferences();

            // XR Ray Pointer on Right Controller / Camera
            var rightHand = GameObject.Find("RightHandAnchor") ?? GameObject.Find("RightControllerAnchor");
            Transform pointerParent = (rightHand != null) ? rightHand.transform : (Camera.main != null ? Camera.main.transform : rig.transform);

            var pointer = pointerParent.GetComponentInChildren<XRRayPointer>();
            if (pointer == null)
            {
                var pObj = new GameObject("XRRayPointer");
                pObj.transform.SetParent(pointerParent, false);
                pointer = pObj.AddComponent<XRRayPointer>();
            }

            // Location Tracker
            var tracker = rig.GetOrAdd<PlayerLocationTracker>();
            tracker.LocateTrackingReferences();

            return rig;
        }

        private static void SetupAndBakeNavMesh(GameObject campusMaster)
        {
            var surface = campusMaster.GetOrAdd<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.defaultArea = 0; // Walkable

            // Bake NavMesh
            surface.BuildNavMesh();
            Debug.Log("[CampusRebuildSetup] NavMesh baked successfully across all campus floors, corridors, and ramps!");
        }

        private static void UpdateBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;

            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                {
                    scenes[i].enabled = true;
                    // Move to index 0
                    var current = scenes[i];
                    scenes.RemoveAt(i);
                    scenes.Insert(0, current);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[CampusRebuildSetup] Build settings updated: scene 0 set to '{scenePath}'.");
        }
    }
}
// Rebuild trigger timestamp: 10/25/2026 18:47:59
