using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using SmartCampus.UI;

namespace SmartCampus.Navigation
{
    public class RouteManager : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private CampusGraph campusGraph;
        [SerializeField] private RouteArrowManager arrowManager;
        [SerializeField] private PlayerLocationTracker locationTracker;
        [SerializeField] private CompactGuidancePanel guidancePanel;

        [Header("Navigation Settings")]
        [SerializeField] private bool useNavMeshSmoothing = true;
        [SerializeField] private bool defaultStepFree = false;
        [SerializeField] private float recalculationCooldown = 2.5f;

        public event Action<RouteResult, List<RouteStep>> OnRouteStarted;
        public event Action<RouteResult> OnRouteRecalculated;
        public event Action OnRouteCancelled;
        public event Action<string> OnDestinationReached;

        private RouteResult currentRoute;
        private string activeDestinationId;
        private bool isStepFreeRequired = false;
        private float lastRecalculationTime = -99f;

        public RouteResult CurrentRoute => currentRoute;
        public string ActiveDestinationId => activeDestinationId;
        public bool IsStepFreeRequired => isStepFreeRequired;
        public bool HasActiveRoute => currentRoute != null && currentRoute.isSuccess;

        private void Awake()
        {
            EnsureDependencies();
        }

        private void Start()
        {
            EnsureDependencies();
            SubscribeToEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        public void EnsureDependencies()
        {
            if (campusGraph == null) campusGraph = FindAnyObjectByType<CampusGraph>();
            if (arrowManager == null) arrowManager = FindAnyObjectByType<RouteArrowManager>();
            if (locationTracker == null) locationTracker = FindAnyObjectByType<PlayerLocationTracker>();
            if (guidancePanel == null) guidancePanel = FindAnyObjectByType<CompactGuidancePanel>(FindObjectsInactive.Include);
            isStepFreeRequired = defaultStepFree;
        }

        private void SubscribeToEvents()
        {
            if (locationTracker != null)
            {
                locationTracker.OnPlayerPositionUpdated += HandlePlayerPositionUpdated;
                locationTracker.OnRouteDeviationDetected += HandleRouteDeviation;
                locationTracker.OnDestinationReached += HandleArrival;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (locationTracker != null)
            {
                locationTracker.OnPlayerPositionUpdated -= HandlePlayerPositionUpdated;
                locationTracker.OnRouteDeviationDetected -= HandleRouteDeviation;
                locationTracker.OnDestinationReached -= HandleArrival;
            }
        }

        public void SetStepFree(bool stepFree)
        {
            isStepFreeRequired = stepFree;
            if (HasActiveRoute)
            {
                // Recompute active route with step-free preference
                StartNavigation(activeDestinationId, isStepFreeRequired);
            }
        }

        public bool StartNavigation(string destinationId, bool stepFree)
        {
            isStepFreeRequired = stepFree;
            return StartNavigation(destinationId);
        }

        public bool StartNavigation(string destinationId)
        {
            EnsureDependencies();

            if (campusGraph == null)
            {
                Debug.LogError("[RouteManager] CampusGraph reference is missing.");
                return false;
            }

            CampusNode targetNode = campusGraph.GetNode(destinationId);
            if (targetNode == null)
            {
                Debug.LogError($"[RouteManager] Destination node '{destinationId}' not found in campus graph.");
                return false;
            }

            Vector3 playerPos = (locationTracker != null) ? locationTracker.CurrentGroundPosition : transform.position;

            // If player is already within arrival threshold (2.0m) of destination
            float distToTarget = Vector2.Distance(new Vector2(playerPos.x, playerPos.z), new Vector2(targetNode.position.x, targetNode.position.z));
            if (distToTarget <= 2.0f)
            {
                Debug.Log($"[RouteManager] Player is already at destination '{targetNode.displayName}' ({distToTarget:F1}m away).");
                activeDestinationId = destinationId;
                if (arrowManager != null) arrowManager.ClearRoute();
                if (guidancePanel != null)
                {
                    guidancePanel.gameObject.SetActive(true);
                    guidancePanel.EnsureReferences();
                    guidancePanel.SubscribeToEvents();
                    guidancePanel.HandleDestinationReached(targetNode.displayName);
                }
                OnDestinationReached?.Invoke(targetNode.displayName);
                return true;
            }

            CampusNode startNode = campusGraph.FindNearestNode(playerPos, isStepFreeRequired);
            if (startNode == null)
            {
                startNode = campusGraph.FindNearestNode(playerPos, false);
            }

            if (startNode == null)
            {
                Debug.LogWarning("[RouteManager] Could not find a valid start node near player position.");
                return false;
            }

            // A* Graph search
            RouteResult graphRoute = AStarPathfinder.FindRoute(campusGraph, startNode.nodeId, destinationId, isStepFreeRequired);

            if (!graphRoute.isSuccess && isStepFreeRequired)
            {
                Debug.LogWarning("[RouteManager] Step-free route failed, attempting standard route fallback...");
                graphRoute = AStarPathfinder.FindRoute(campusGraph, startNode.nodeId, destinationId, false);
            }

            if (!graphRoute.isSuccess)
            {
                Debug.LogWarning($"[RouteManager] A* route calculation failed: {graphRoute.errorMessage}");
                return false;
            }

            // Build full walkable waypoint list (starting from player's exact ground position)
            List<Vector3> fullWaypoints = BuildWalkablePath(playerPos, graphRoute.waypoints);
            graphRoute.waypoints = fullWaypoints;

            // Turn-by-turn guidance
            List<RouteStep> steps = GuidanceEngine.GenerateTurnByTurnInstructions(graphRoute.pathNodes);

            currentRoute = graphRoute;
            activeDestinationId = destinationId;

            // Visual arrows
            if (arrowManager != null)
            {
                arrowManager.DisplayRoute(fullWaypoints, targetNode.displayName);
            }

            // Track route progression
            if (locationTracker != null)
            {
                locationTracker.StartTrackingRoute(currentRoute);
            }

            // Activate and display HUD guidance
            if (guidancePanel != null)
            {
                guidancePanel.gameObject.SetActive(true);
                guidancePanel.EnsureReferences();
                guidancePanel.SubscribeToEvents();
                guidancePanel.HandleRouteStarted(currentRoute, steps);
            }

            OnRouteStarted?.Invoke(currentRoute, steps);
            return true;
        }

        public void CancelNavigation()
        {
            currentRoute = null;
            activeDestinationId = null;

            if (arrowManager != null)
            {
                arrowManager.ClearRoute();
            }

            if (locationTracker != null)
            {
                locationTracker.StopTrackingRoute();
            }

            if (guidancePanel != null)
            {
                guidancePanel.HandleRouteCancelled();
            }

            OnRouteCancelled?.Invoke();
        }

        private void HandlePlayerPositionUpdated(Vector3 playerPos)
        {
            if (HasActiveRoute && arrowManager != null)
            {
                arrowManager.UpdatePlayerProgress(playerPos);
            }
        }

        private void HandleRouteDeviation()
        {
            if (!HasActiveRoute || string.IsNullOrEmpty(activeDestinationId)) return;

            if (Time.time - lastRecalculationTime < recalculationCooldown)
            {
                return; // Respect cooldown to prevent recalculation thrashing
            }

            lastRecalculationTime = Time.time;
            Debug.Log("[RouteManager] Off-route deviation detected. Recalculating path from current player position...");

            Vector3 playerPos = locationTracker != null ? locationTracker.CurrentGroundPosition : transform.position;
            CampusNode newStartNode = campusGraph.FindNearestNode(playerPos, isStepFreeRequired);

            if (newStartNode != null)
            {
                RouteResult newRoute = AStarPathfinder.FindRoute(campusGraph, newStartNode.nodeId, activeDestinationId, isStepFreeRequired);
                if (newRoute.isSuccess)
                {
                    List<Vector3> smoothed = BuildWalkablePath(playerPos, newRoute.waypoints);
                    newRoute.waypoints = smoothed;
                    currentRoute = newRoute;

                    CampusNode target = campusGraph.GetNode(activeDestinationId);
                    if (arrowManager != null)
                    {
                        arrowManager.DisplayRoute(smoothed, target != null ? target.displayName : "");
                    }

                    if (locationTracker != null)
                    {
                        locationTracker.StartTrackingRoute(currentRoute);
                    }

                    if (guidancePanel != null)
                    {
                        guidancePanel.HandleRouteRecalculated(newRoute);
                    }

                    OnRouteRecalculated?.Invoke(currentRoute);
                }
            }
        }

        private void HandleArrival()
        {
            string reachedDest = activeDestinationId;
            CampusNode destNode = campusGraph != null ? campusGraph.GetNode(reachedDest) : null;
            string destName = destNode != null ? destNode.displayName : "Destination";

            // Clear visual arrows and route tracking
            if (arrowManager != null) arrowManager.ClearRoute();
            if (locationTracker != null) locationTracker.StopTrackingRoute();
            currentRoute = null;
            activeDestinationId = null;

            // Keep guidance HUD visible with arrival notice
            if (guidancePanel != null)
            {
                guidancePanel.HandleDestinationReached(destName);
            }

            OnDestinationReached?.Invoke(destName);
        }

        private List<Vector3> BuildWalkablePath(Vector3 playerStartPos, List<Vector3> nodeWaypoints)
        {
            var result = new List<Vector3>();
            if (nodeWaypoints == null || nodeWaypoints.Count == 0) return result;

            // Prepend current player position
            result.Add(playerStartPos);

            if (!useNavMeshSmoothing)
            {
                result.AddRange(nodeWaypoints);
                return result;
            }

            // Refine segments via NavMesh if available
            Vector3 currentOrigin = playerStartPos;

            for (int i = 0; i < nodeWaypoints.Count; i++)
            {
                Vector3 target = nodeWaypoints[i];
                var navPath = new NavMeshPath();

                if (NavMesh.CalculatePath(currentOrigin, target, NavMesh.AllAreas, navPath) && navPath.status == NavMeshPathStatus.PathComplete)
                {
                    // Add NavMesh corners (skip the first corner if it matches origin)
                    for (int c = 1; c < navPath.corners.Length; c++)
                    {
                        result.Add(navPath.corners[c]);
                    }
                }
                else
                {
                    // Direct segment fallback
                    result.Add(target);
                }

                currentOrigin = target;
            }

            return result;
        }
    }
}
