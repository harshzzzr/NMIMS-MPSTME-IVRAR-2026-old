using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartCampus.Navigation
{
    [Serializable]
    public class RouteResult
    {
        public bool isSuccess;
        public string startNodeId;
        public string destinationNodeId;
        public List<CampusNode> pathNodes = new List<CampusNode>();
        public List<Vector3> waypoints = new List<Vector3>();
        public float totalDistance;
        public float estimatedTimeSeconds;
        public bool isStepFree;
        public string errorMessage;

        public string GetFormattedTime()
        {
            int minutes = Mathf.FloorToInt(estimatedTimeSeconds / 60f);
            int seconds = Mathf.FloorToInt(estimatedTimeSeconds % 60f);
            if (minutes > 0)
            {
                return $"{minutes}m {seconds:00}s";
            }
            return $"{seconds}s";
        }
    }

    public static class AStarPathfinder
    {
        public const float DefaultWalkingSpeed = 1.35f; // meters per second (~4.8 km/h)

        private class PriorityQueueNode : IComparable<PriorityQueueNode>
        {
            public string NodeId;
            public float FScore;

            public PriorityQueueNode(string id, float f)
            {
                NodeId = id;
                FScore = f;
            }

            public int CompareTo(PriorityQueueNode other)
            {
                return FScore.CompareTo(other.FScore);
            }
        }

        public static RouteResult FindRoute(CampusGraph graph, string startId, string targetId, bool requireStepFree = false, float walkingSpeed = DefaultWalkingSpeed)
        {
            var result = new RouteResult
            {
                startNodeId = startId,
                destinationNodeId = targetId,
                isStepFree = requireStepFree
            };

            if (graph == null)
            {
                result.isSuccess = false;
                result.errorMessage = "Graph reference is null.";
                return result;
            }

            CampusNode startNode = graph.GetNode(startId);
            CampusNode targetNode = graph.GetNode(targetId);

            if (startNode == null || targetNode == null)
            {
                result.isSuccess = false;
                result.errorMessage = $"Start ({startId}) or target ({targetId}) node was not found in campus graph.";
                return result;
            }

            if (startId == targetId)
            {
                result.isSuccess = true;
                result.pathNodes.Add(startNode);
                result.waypoints.Add(startNode.position);
                result.totalDistance = 0f;
                result.estimatedTimeSeconds = 0f;
                return result;
            }

            // A* algorithm structures
            var openList = new List<PriorityQueueNode>();
            var openSet = new HashSet<string>();
            var closedSet = new HashSet<string>();

            var gScore = new Dictionary<string, float>();
            var fScore = new Dictionary<string, float>();
            var cameFrom = new Dictionary<string, string>();

            gScore[startId] = 0f;
            float initialH = Vector3.Distance(startNode.position, targetNode.position);
            fScore[startId] = initialH;

            openList.Add(new PriorityQueueNode(startId, initialH));
            openSet.Add(startId);

            bool found = false;

            while (openList.Count > 0)
            {
                // Find node with smallest F
                int bestIdx = 0;
                float bestF = openList[0].FScore;
                for (int i = 1; i < openList.Count; i++)
                {
                    if (openList[i].FScore < bestF)
                    {
                        bestF = openList[i].FScore;
                        bestIdx = i;
                    }
                }

                string currentId = openList[bestIdx].NodeId;
                openList.RemoveAt(bestIdx);
                openSet.Remove(currentId);
                closedSet.Add(currentId);

                if (currentId == targetId)
                {
                    found = true;
                    break;
                }

                CampusNode currentNode = graph.GetNode(currentId);
                if (currentNode == null) continue;

                List<CampusEdge> neighbors = graph.GetOutgoingEdges(currentId);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    var edge = neighbors[i];
                    float edgeCost = edge.GetEffectiveCost(requireStepFree);

                    if (float.IsInfinity(edgeCost) || edgeCost < 0f)
                    {
                        continue; // impassable, closed, or stairs when step-free
                    }

                    string neighborId = edge.GetOtherNode(currentId);
                    if (string.IsNullOrEmpty(neighborId) || closedSet.Contains(neighborId))
                    {
                        continue;
                    }

                    CampusNode neighborNode = graph.GetNode(neighborId);
                    if (neighborNode == null) continue;

                    if (requireStepFree && !neighborNode.isAccessible)
                    {
                        continue;
                    }

                    float tentativeG = gScore[currentId] + edgeCost;
                    float currentNeighborG = gScore.TryGetValue(neighborId, out var existingG) ? existingG : float.PositiveInfinity;

                    if (tentativeG < currentNeighborG)
                    {
                        cameFrom[neighborId] = currentId;
                        gScore[neighborId] = tentativeG;

                        float h = Vector3.Distance(neighborNode.position, targetNode.position);
                        float f = tentativeG + h;
                        fScore[neighborId] = f;

                        if (!openSet.Contains(neighborId))
                        {
                            openList.Add(new PriorityQueueNode(neighborId, f));
                            openSet.Add(neighborId);
                        }
                        else
                        {
                            // Update existing entry in openList
                            for (int k = 0; k < openList.Count; k++)
                            {
                                if (openList[k].NodeId == neighborId)
                                {
                                    openList[k].FScore = f;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (!found)
            {
                result.isSuccess = false;
                result.errorMessage = requireStepFree
                    ? $"No accessible step-free route found to {targetNode.displayName}."
                    : $"No route found to {targetNode.displayName} (pathway blocked or disconnected).";
                return result;
            }

            // Reconstruct path
            var reconstructedIds = new List<string>();
            string trace = targetId;
            reconstructedIds.Add(trace);

            while (cameFrom.TryGetValue(trace, out var prev))
            {
                trace = prev;
                reconstructedIds.Add(trace);
            }

            reconstructedIds.Reverse();

            float totalDist = 0f;
            Vector3? lastPos = null;

            for (int i = 0; i < reconstructedIds.Count; i++)
            {
                var n = graph.GetNode(reconstructedIds[i]);
                if (n != null)
                {
                    result.pathNodes.Add(n);
                    result.waypoints.Add(n.position);

                    if (lastPos.HasValue)
                    {
                        totalDist += Vector3.Distance(lastPos.Value, n.position);
                    }
                    lastPos = n.position;
                }
            }

            result.isSuccess = true;
            result.totalDistance = totalDist;
            result.estimatedTimeSeconds = walkingSpeed > 0.1f ? (totalDist / walkingSpeed) : (totalDist / DefaultWalkingSpeed);

            return result;
        }
    }
}
