using System.Collections.Generic;
using UnityEngine;

namespace SmartCampus.Navigation
{
    [ExecuteAlways]
    public class CampusGraph : MonoBehaviour
    {
        [Header("Graph Definition")]
        [SerializeField] private List<CampusNode> nodes = new List<CampusNode>();
        [SerializeField] private List<CampusEdge> edges = new List<CampusEdge>();

        private readonly Dictionary<string, CampusNode> nodeLookup = new Dictionary<string, CampusNode>();
        private readonly Dictionary<string, List<CampusEdge>> adjacencyMap = new Dictionary<string, List<CampusEdge>>();
        private bool isInitialized = false;

        public List<CampusNode> Nodes => nodes;
        public List<CampusEdge> Edges => edges;

        private void Awake()
        {
            RebuildLookup();
        }

        private void OnEnable()
        {
            RebuildLookup();
        }

        public void RebuildLookup()
        {
            nodeLookup.Clear();
            adjacencyMap.Clear();

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node != null && !string.IsNullOrEmpty(node.nodeId))
                {
                    nodeLookup[node.nodeId] = node;
                    adjacencyMap[node.nodeId] = new List<CampusEdge>();
                }
            }

            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];
                if (edge == null) continue;

                // Auto-calculate base distance if not set or <= 0
                if (edge.baseDistance <= 0.001f && nodeLookup.TryGetValue(edge.fromNodeId, out var fromNode) && nodeLookup.TryGetValue(edge.toNodeId, out var toNode))
                {
                    edge.baseDistance = Vector3.Distance(fromNode.position, toNode.position);
                }

                if (adjacencyMap.TryGetValue(edge.fromNodeId, out var fromList))
                {
                    fromList.Add(edge);
                }

                if (edge.isBidirectional && adjacencyMap.TryGetValue(edge.toNodeId, out var toList))
                {
                    toList.Add(edge);
                }
            }

            isInitialized = true;
        }

        public CampusNode GetNode(string nodeId)
        {
            if (!isInitialized) RebuildLookup();
            return nodeLookup.TryGetValue(nodeId, out var node) ? node : null;
        }

        public List<CampusEdge> GetOutgoingEdges(string nodeId)
        {
            if (!isInitialized) RebuildLookup();
            return adjacencyMap.TryGetValue(nodeId, out var list) ? list : new List<CampusEdge>();
        }

        public CampusNode FindNearestNode(Vector3 worldPos, bool requireAccessible = false, float maxDistance = 50f, bool avoidWalls = true)
        {
            if (!isInitialized) RebuildLookup();

            CampusNode bestNode = null;
            float bestScore = float.MaxValue;
            float maxSqrDist = maxDistance * maxDistance;

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (requireAccessible && !node.isAccessible) continue;

                float sqrDist = (node.position - worldPos).sqrMagnitude;
                if (sqrDist > maxSqrDist) continue;

                float score = sqrDist;

                // If avoidWalls is requested, check if a solid wall blocks direct line-of-sight
                if (avoidWalls && Application.isPlaying)
                {
                    Vector3 origin = worldPos + Vector3.up * 0.5f;
                    Vector3 target = node.position + Vector3.up * 0.5f;
                    if (Physics.Linecast(origin, target, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (!hit.collider.CompareTag("Player") && hit.collider.GetComponentInParent<CharacterController>() == null)
                        {
                            // Add penalty so corridors in direct line-of-sight take precedence over rooms through walls
                            score += 500f;
                        }
                    }
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestNode = node;
                }
            }

            return bestNode;
        }

        public List<CampusNode> GetDestinations(DestinationCategory category = DestinationCategory.All)
        {
            var result = new List<CampusNode>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                // Pure corridors and junctions are not selectable destinations
                if (node.nodeType == CampusNodeType.Corridor || node.nodeType == CampusNodeType.Junction)
                {
                    continue;
                }

                if (category == DestinationCategory.All || node.category == category)
                {
                    result.Add(node);
                }
            }
            return result;
        }

        public bool SetEdgeClosed(string nodeA, string nodeB, bool closed)
        {
            bool found = false;
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].Connects(nodeA, nodeB))
                {
                    edges[i].isClosed = closed;
                    found = true;
                }
            }
            return found;
        }

        public bool SetCrowdPenalty(string nodeA, string nodeB, float penalty)
        {
            bool found = false;
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].Connects(nodeA, nodeB))
                {
                    edges[i].crowdPenalty = Mathf.Max(0f, penalty);
                    found = true;
                }
            }
            return found;
        }

        public void AddNode(CampusNode node)
        {
            nodes.Add(node);
            isInitialized = false;
        }

        public void AddEdge(CampusEdge edge)
        {
            edges.Add(edge);
            isInitialized = false;
        }

        public void ClearGraph()
        {
            nodes.Clear();
            edges.Clear();
            nodeLookup.Clear();
            adjacencyMap.Clear();
            isInitialized = false;
        }

        private void OnDrawGizmos()
        {
            if (nodes == null || nodes.Count == 0) return;

            // Draw edges
            if (edges != null)
            {
                for (int i = 0; i < edges.Count; i++)
                {
                    var edge = edges[i];
                    if (edge == null) continue;

                    CampusNode n1 = nodes.Find(n => n.nodeId == edge.fromNodeId);
                    CampusNode n2 = nodes.Find(n => n.nodeId == edge.toNodeId);
                    if (n1 == null || n2 == null) continue;

                    if (edge.isClosed)
                    {
                        Gizmos.color = Color.red;
                    }
                    else if (!edge.isAccessible)
                    {
                        Gizmos.color = new Color(1f, 0.5f, 0f, 0.9f); // Orange for stairs
                    }
                    else
                    {
                        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.7f); // Cyan for normal walkable
                    }

                    Gizmos.DrawLine(n1.position + Vector3.up * 0.1f, n2.position + Vector3.up * 0.1f);
                }
            }

            // Draw nodes
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = nodes[i];
                if (n == null) continue;

                switch (n.nodeType)
                {
                    case CampusNodeType.Entrance:
                        Gizmos.color = Color.green;
                        break;
                    case CampusNodeType.Reception:
                        Gizmos.color = Color.yellow;
                        break;
                    case CampusNodeType.Classroom:
                    case CampusNodeType.ComputerLab:
                    case CampusNodeType.Library:
                    case CampusNodeType.Canteen:
                        Gizmos.color = Color.magenta;
                        break;
                    case CampusNodeType.Stairs:
                    case CampusNodeType.Ramp:
                        Gizmos.color = new Color(1f, 0.5f, 0f);
                        break;
                    default:
                        Gizmos.color = Color.cyan;
                        break;
                }

                Gizmos.DrawSphere(n.position + Vector3.up * 0.1f, 0.35f);
            }
        }
    }
}
