using System;
using UnityEngine;

namespace SmartCampus.Navigation
{
    public class PlayerLocationTracker : MonoBehaviour
    {
        [Header("Tracking References")]
        [SerializeField] private Transform trackingHead; // CenterEyeAnchor or MainCamera
        [SerializeField] private Transform trackingRoot; // XR Rig root

        [Header("Deviation & Arrival Parameters")]
        [SerializeField] private float deviationThreshold = 3.5f; // Recalculate route if player drifts > 3.5m off path
        [SerializeField] private float arrivalThreshold = 1.8f;   // Destination reached threshold in meters
        [SerializeField] private float groundCheckDistance = 5.0f;
        [SerializeField] private LayerMask groundLayerMask = ~0; // Default all layers

        public event Action<Vector3> OnPlayerPositionUpdated;
        public event Action OnRouteDeviationDetected;
        public event Action OnDestinationReached;
        public event Action<float, float, int> OnProgressMetricsUpdated;

        private Vector3 currentVirtualGroundPosition;
        private RouteResult activeRoute;
        private int currentTargetWaypointIndex = 0;
        private bool isTrackingActiveRoute = false;

        public Vector3 CurrentGroundPosition => currentVirtualGroundPosition;
        public bool IsTrackingActive => isTrackingActiveRoute;

        private void Start()
        {
            LocateTrackingReferences();
        }

        public void LocateTrackingReferences()
        {
            if (trackingHead == null)
            {
                // Try to find Meta XR CenterEyeAnchor
                var centerEye = GameObject.Find("CenterEyeAnchor");
                if (centerEye != null)
                {
                    trackingHead = centerEye.transform;
                }
                else if (Camera.main != null)
                {
                    trackingHead = Camera.main.transform;
                }
            }

            if (trackingRoot == null)
            {
                if (trackingHead != null && trackingHead.parent != null)
                {
                    trackingRoot = trackingHead.root;
                }
                else
                {
                    trackingRoot = transform;
                }
            }
        }

        private void Update()
        {
            UpdatePlayerLocation();

            if (isTrackingActiveRoute && activeRoute != null && activeRoute.isSuccess && activeRoute.waypoints.Count > 0)
            {
                CheckRouteProgress();
            }
        }

        private void UpdatePlayerLocation()
        {
            Vector3 rawPos = transform.position;

            if (trackingHead != null)
            {
                // Use HMD position
                rawPos = trackingHead.position;
            }
            else if (trackingRoot != null)
            {
                rawPos = trackingRoot.position;
            }

            // Project to ground floor using raycast
            if (Physics.Raycast(rawPos + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, groundCheckDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
            {
                currentVirtualGroundPosition = hit.point;
            }
            else
            {
                currentVirtualGroundPosition = new Vector3(rawPos.x, 0f, rawPos.z);
            }

            OnPlayerPositionUpdated?.Invoke(currentVirtualGroundPosition);
        }

        public void StartTrackingRoute(RouteResult route)
        {
            activeRoute = route;
            currentTargetWaypointIndex = 0;
            isTrackingActiveRoute = (route != null && route.isSuccess && route.waypoints.Count > 0);
        }

        public void StopTrackingRoute()
        {
            isTrackingActiveRoute = false;
            activeRoute = null;
            currentTargetWaypointIndex = 0;
        }

        private void CheckRouteProgress()
        {
            var waypoints = activeRoute.waypoints;
            int total = waypoints.Count;

            // Check destination arrival
            Vector3 destinationPos = waypoints[total - 1];
            float distToArrival = Vector2.Distance(
                new Vector2(currentVirtualGroundPosition.x, currentVirtualGroundPosition.z),
                new Vector2(destinationPos.x, destinationPos.z)
            );

            if (distToArrival <= arrivalThreshold)
            {
                // Ensure a solid wall does not separate player from the destination arrival point
                if (Application.isPlaying)
                {
                    Vector3 origin = currentVirtualGroundPosition + Vector3.up * 0.5f;
                    Vector3 target = destinationPos + Vector3.up * 0.5f;
                    if (Physics.Linecast(origin, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (!hit.collider.CompareTag("Player") && hit.collider.GetComponentInParent<CharacterController>() == null)
                        {
                            // A wall or obstacle separates player from destination point -> Not arrived yet
                            return;
                        }
                    }
                }

                isTrackingActiveRoute = false;
                OnDestinationReached?.Invoke();
                return;
            }

            // Find closest segment and advance target waypoint
            float minDistanceToRoute = float.MaxValue;
            int bestSegment = currentTargetWaypointIndex;

            for (int i = 0; i < total - 1; i++)
            {
                float d = DistancePointToSegment(currentVirtualGroundPosition, waypoints[i], waypoints[i + 1]);
                if (d < minDistanceToRoute)
                {
                    minDistanceToRoute = d;
                    bestSegment = i + 1; // target is end of this segment
                }
            }

            currentTargetWaypointIndex = Mathf.Clamp(bestSegment, 0, total - 1);

            // Compute accurate remaining route distance & walking time
            float remainingDist = Vector3.Distance(currentVirtualGroundPosition, waypoints[currentTargetWaypointIndex]);
            for (int w = currentTargetWaypointIndex; w < total - 1; w++)
            {
                remainingDist += Vector3.Distance(waypoints[w], waypoints[w + 1]);
            }
            float remainingTime = remainingDist / 1.35f;
            OnProgressMetricsUpdated?.Invoke(remainingDist, remainingTime, currentTargetWaypointIndex);

            // Check off-route deviation
            if (minDistanceToRoute > deviationThreshold)
            {
                // Trigger route recalculation
                OnRouteDeviationDetected?.Invoke();
            }
        }

        private float DistancePointToSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 seg = end - start;
            float lenSqr = seg.sqrMagnitude;
            if (lenSqr < 0.0001f) return Vector3.Distance(point, start);

            float t = Mathf.Clamp01(Vector3.Dot(point - start, seg) / lenSqr);
            Vector3 proj = start + seg * t;
            return Vector3.Distance(point, proj);
        }
    }
}
