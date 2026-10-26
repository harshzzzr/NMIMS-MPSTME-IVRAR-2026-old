using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using SmartCampus.Navigation;

namespace SmartCampus.UI
{
    public class CompactGuidancePanel : MonoBehaviour
    {
        [Header("System References")]
        [SerializeField] private RouteManager routeManager;
        [SerializeField] private PlayerLocationTracker locationTracker;
        [SerializeField] private DestinationMenuUI destinationMenu;
        [SerializeField] private Transform trackingCamera;

        [Header("UI Elements")]
        [SerializeField] private Text destinationText;
        [SerializeField] private Text turnIconText;
        [SerializeField] private Text primaryInstructionText;
        [SerializeField] private Text remainingDistanceText;
        [SerializeField] private Text remainingTimeText;
        [SerializeField] private Text statusBannerText;
        [SerializeField] private Button cancelNavigationButton;
        [SerializeField] private Button openDirectoryButton;
        [SerializeField] private GameObject arrivalNoticeContainer;
        [SerializeField] private Text arrivalNoticeText;
        [SerializeField] private Button chooseNewDestinationButton;

        [Header("Soft Follow VR Settings")]
        [SerializeField] private bool enableSoftFollow = true;
        [SerializeField] private float followDistance = 1.15f;
        [SerializeField] private float heightOffset = -0.28f; // Positioned comfortably below eye level
        [SerializeField] private float followSpeed = 4.5f;

        private List<RouteStep> currentSteps = new List<RouteStep>();
        private int currentStepIndex = 0;
        private bool isNavigating = false;

        private bool isSubscribed = false;

        private void Awake()
        {
            EnsureReferences();
            WireButtons();
        }

        private void Start()
        {
            EnsureReferences();
            SubscribeToEvents();

            // Hidden on startup until navigation begins
            if (!isNavigating)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        public void EnsureReferences()
        {
            if (routeManager == null) routeManager = FindAnyObjectByType<RouteManager>();
            if (locationTracker == null) locationTracker = FindAnyObjectByType<PlayerLocationTracker>();
            if (destinationMenu == null) destinationMenu = FindAnyObjectByType<DestinationMenuUI>(FindObjectsInactive.Include);

            if (trackingCamera == null)
            {
                var center = GameObject.Find("CenterEyeAnchor");
                if (center != null) trackingCamera = center.transform;
                else if (Camera.main != null) trackingCamera = Camera.main.transform;
            }
        }

        private void WireButtons()
        {
            if (cancelNavigationButton != null)
            {
                cancelNavigationButton.onClick.RemoveListener(OnCancelClicked);
                cancelNavigationButton.onClick.AddListener(OnCancelClicked);
            }

            if (openDirectoryButton != null)
            {
                openDirectoryButton.onClick.RemoveListener(OnOpenDirectoryClicked);
                openDirectoryButton.onClick.AddListener(OnOpenDirectoryClicked);
            }

            if (chooseNewDestinationButton != null)
            {
                chooseNewDestinationButton.onClick.RemoveListener(OnChooseNewDestinationClicked);
                chooseNewDestinationButton.onClick.AddListener(OnChooseNewDestinationClicked);
            }
        }

        public void SubscribeToEvents()
        {
            if (isSubscribed) return;
            if (routeManager != null)
            {
                routeManager.OnRouteStarted += HandleRouteStarted;
                routeManager.OnRouteRecalculated += HandleRouteRecalculated;
                routeManager.OnRouteCancelled += HandleRouteCancelled;
                routeManager.OnDestinationReached += HandleDestinationReached;
                isSubscribed = true;
            }

            if (locationTracker != null)
            {
                locationTracker.OnProgressMetricsUpdated += HandleProgressMetricsUpdated;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (!isSubscribed) return;
            if (routeManager != null)
            {
                routeManager.OnRouteStarted -= HandleRouteStarted;
                routeManager.OnRouteRecalculated -= HandleRouteRecalculated;
                routeManager.OnRouteCancelled -= HandleRouteCancelled;
                routeManager.OnDestinationReached -= HandleDestinationReached;
            }

            if (locationTracker != null)
            {
                locationTracker.OnProgressMetricsUpdated -= HandleProgressMetricsUpdated;
            }
            isSubscribed = false;
        }

        private void LateUpdate()
        {
            // Soft-follow VR camera orientation and position
            if (enableSoftFollow && trackingCamera != null && gameObject.activeSelf)
            {
                Vector3 targetPos = trackingCamera.position
                                  + trackingCamera.forward * followDistance
                                  + trackingCamera.up * heightOffset;

                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

                // Face gently toward player
                Vector3 lookDir = transform.position - trackingCamera.position;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(lookDir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * followSpeed);
                }
            }

            // Keyboard/Simulator toggle shortcut (M or Tab)
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.hKey.wasPressedThisFrame || Keyboard.current.mKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame)
                {
                    OnOpenDirectoryClicked();
                }
            }
#endif

            // VR controller menu toggle (Y or B button)
            if (OVRInput.GetDown(OVRInput.Button.Two) || OVRInput.GetDown(OVRInput.Button.Four))
            {
                OnOpenDirectoryClicked();
            }
        }

        public void HandleRouteStarted(RouteResult route, List<RouteStep> steps)
        {
            isNavigating = true;
            currentSteps = steps ?? new List<RouteStep>();
            currentStepIndex = 0;

            if (arrivalNoticeContainer != null) arrivalNoticeContainer.SetActive(false);

            if (destinationText != null && route != null)
            {
                var graph = FindAnyObjectByType<CampusGraph>();
                var targetNode = graph != null ? graph.GetNode(route.destinationNodeId) : null;
                destinationText.text = targetNode != null ? targetNode.displayName : route.destinationNodeId;
            }

            if (statusBannerText != null) statusBannerText.text = "Navigation Active";

            UpdateStepDisplay(0, route != null ? route.totalDistance : 0f, route != null ? route.estimatedTimeSeconds : 0f);

            // Position immediately in front of user
            EnsureReferences();
            if (trackingCamera != null)
            {
                transform.position = trackingCamera.position + trackingCamera.forward * followDistance + trackingCamera.up * heightOffset;
                transform.rotation = Quaternion.LookRotation(transform.position - trackingCamera.position, Vector3.up);
            }

            gameObject.SetActive(true);
        }

        public void HandleRouteRecalculated(RouteResult route)
        {
            if (statusBannerText != null)
            {
                statusBannerText.text = "Route Recalculated";
            }

            if (route != null)
            {
                currentSteps = GuidanceEngine.GenerateTurnByTurnInstructions(route.pathNodes);
                currentStepIndex = 0;
                UpdateStepDisplay(0, route.totalDistance, route.estimatedTimeSeconds);
            }
        }

        public void HandleRouteCancelled()
        {
            isNavigating = false;
            gameObject.SetActive(false);
        }

        public void HandleDestinationReached(string destName)
        {
            isNavigating = false;

            EnsureReferences();

            // Position panel comfortably in front of user upon arrival
            if (trackingCamera != null)
            {
                transform.position = trackingCamera.position + trackingCamera.forward * followDistance + trackingCamera.up * heightOffset;
                transform.rotation = Quaternion.LookRotation(transform.position - trackingCamera.position, Vector3.up);
            }

            if (arrivalNoticeContainer != null)
            {
                arrivalNoticeContainer.SetActive(true);
            }

            if (arrivalNoticeText != null)
            {
                arrivalNoticeText.text = $"ARRIVED AT {destName.ToUpper()}!";
            }

            if (statusBannerText != null)
            {
                statusBannerText.text = "Destination Reached";
            }

            gameObject.SetActive(true);
        }

        private void HandleProgressMetricsUpdated(float remainingDist, float remainingTime, int currentWaypointIdx)
        {
            if (!isNavigating) return;

            // Advance step if waypoint passed
            if (currentSteps != null && currentSteps.Count > 0)
            {
                int bestStep = 0;
                for (int s = 0; s < currentSteps.Count; s++)
                {
                    if (currentWaypointIdx >= currentSteps[s].waypointIndex)
                    {
                        bestStep = s;
                    }
                }
                currentStepIndex = Mathf.Clamp(bestStep, 0, currentSteps.Count - 1);
            }

            UpdateStepDisplay(currentStepIndex, remainingDist, remainingTime);
        }

        private void UpdateStepDisplay(int stepIdx, float distanceMeters, float timeSeconds)
        {
            if (remainingDistanceText != null)
            {
                remainingDistanceText.text = $"{distanceMeters:F0}m";
            }

            if (remainingTimeText != null)
            {
                int mins = Mathf.FloorToInt(timeSeconds / 60f);
                int secs = Mathf.FloorToInt(timeSeconds % 60f);
                remainingTimeText.text = (mins > 0) ? $"{mins}m {secs:00}s" : $"{secs}s";
            }

            if (currentSteps != null && stepIdx < currentSteps.Count)
            {
                var step = currentSteps[stepIdx];
                if (primaryInstructionText != null)
                {
                    primaryInstructionText.text = step.instructionText;
                }

                if (turnIconText != null)
                {
                    turnIconText.text = GetTurnIcon(step.turnType);
                }
            }
        }

        private string GetTurnIcon(TurnType turn)
        {
            switch (turn)
            {
                case TurnType.Straight: return "[ ^ ]";
                case TurnType.TurnRight: return "[ -> ]";
                case TurnType.SlightRight: return "[ / ]";
                case TurnType.SharpRight: return "[ ==> ]";
                case TurnType.TurnLeft: return "[ <- ]";
                case TurnType.SlightLeft: return "[ \\ ]";
                case TurnType.SharpLeft: return "[ <== ]";
                case TurnType.StairsUp: return "[ ^ STAIRS ]";
                case TurnType.StairsDown: return "[ v STAIRS ]";
                case TurnType.Ramp: return "[ RAMP ]";
                case TurnType.Arrival: return "[ * ARRIVED * ]";
                default: return "[ ^ ]";
            }
        }

        private void OnCancelClicked()
        {
            if (routeManager != null)
            {
                routeManager.CancelNavigation();
            }
            gameObject.SetActive(false);
        }

        private void OnOpenDirectoryClicked()
        {
            EnsureReferences();
            if (destinationMenu != null)
            {
                if (destinationMenu.gameObject.activeSelf)
                {
                    destinationMenu.CloseMenu();
                }
                else
                {
                    destinationMenu.OpenMenuInFrontOfPlayer(trackingCamera);
                }
            }
        }

        private void OnChooseNewDestinationClicked()
        {
            EnsureReferences();
            if (arrivalNoticeContainer != null) arrivalNoticeContainer.SetActive(false);
            gameObject.SetActive(false);
            if (destinationMenu != null)
            {
                destinationMenu.OpenMenuInFrontOfPlayer(trackingCamera);
            }
        }
    }
}
