using System;
using UnityEngine;

namespace SmartCampus.Navigation
{
    [Serializable]
    public class CampusEdge
    {
        public string fromNodeId;
        public string toNodeId;
        public float baseDistance;
        public bool isBidirectional = true;
        public bool isAccessible = true; // Step-free route friendly
        public bool isClosed = false;    // Simulated corridor closure or locked door
        public float crowdPenalty = 0f;  // Simulated congestion penalty in meters equivalent

        public CampusEdge() { }

        public CampusEdge(string from, string to, float dist, bool bidirectional = true, bool accessible = true)
        {
            fromNodeId = from;
            toNodeId = to;
            baseDistance = dist;
            isBidirectional = bidirectional;
            isAccessible = accessible;
            isClosed = false;
            crowdPenalty = 0f;
        }

        public float GetEffectiveCost(bool requireStepFree = false)
        {
            if (isClosed)
            {
                return float.PositiveInfinity;
            }

            if (requireStepFree && !isAccessible)
            {
                return float.PositiveInfinity;
            }

            return Mathf.Max(0.1f, baseDistance + crowdPenalty);
        }

        public bool Connects(string a, string b)
        {
            if (fromNodeId == a && toNodeId == b) return true;
            if (isBidirectional && fromNodeId == b && toNodeId == a) return true;
            return false;
        }

        public string GetOtherNode(string nodeId)
        {
            if (fromNodeId == nodeId) return toNodeId;
            if (isBidirectional && toNodeId == nodeId) return fromNodeId;
            return null;
        }
    }
}
