using System;
using System.Collections.Generic;
using UnityEngine;

namespace SmartCampus.Navigation
{
    [Serializable]
    public class RouteStep
    {
        public int waypointIndex;
        public string instructionText;
        public float distanceMeters;
        public Vector3 position;
        public TurnType turnType;
    }

    public enum TurnType
    {
        Start,
        Straight,
        SlightRight,
        TurnRight,
        SharpRight,
        SlightLeft,
        TurnLeft,
        SharpLeft,
        StairsUp,
        StairsDown,
        Ramp,
        Arrival
    }

    public static class GuidanceEngine
    {
        public static List<RouteStep> GenerateTurnByTurnInstructions(List<CampusNode> nodes)
        {
            var steps = new List<RouteStep>();
            if (nodes == null || nodes.Count == 0) return steps;

            if (nodes.Count == 1)
            {
                steps.Add(new RouteStep
                {
                    waypointIndex = 0,
                    instructionText = $"You are at {nodes[0].displayName}.",
                    distanceMeters = 0f,
                    position = nodes[0].position,
                    turnType = TurnType.Arrival
                });
                return steps;
            }

            // Step 0: Initial departure
            float seg0Dist = Vector3.Distance(nodes[0].position, nodes[1].position);
            steps.Add(new RouteStep
            {
                waypointIndex = 0,
                instructionText = $"Depart from {nodes[0].displayName} towards {nodes[1].displayName} ({seg0Dist:F0}m)",
                distanceMeters = seg0Dist,
                position = nodes[0].position,
                turnType = TurnType.Start
            });

            // Intermediate turns
            for (int i = 1; i < nodes.Count - 1; i++)
            {
                Vector3 pPrev = nodes[i - 1].position;
                Vector3 pCurr = nodes[i].position;
                Vector3 pNext = nodes[i + 1].position;

                Vector3 vIn = (pCurr - pPrev);
                Vector3 vOut = (pNext - pCurr);
                float segDist = vOut.magnitude;

                // Flatten to horizontal plane for turn angle
                Vector3 vInH = new Vector3(vIn.x, 0, vIn.z).normalized;
                Vector3 vOutH = new Vector3(vOut.x, 0, vOut.z).normalized;

                float signedAngle = Vector3.SignedAngle(vInH, vOutH, Vector3.up);
                TurnType turn = ClassifyTurn(signedAngle, nodes[i], nodes[i + 1]);

                string turnPhrase = GetTurnPhrase(turn);
                string text = $"{turnPhrase} at {nodes[i].displayName} towards {nodes[i + 1].displayName} ({segDist:F0}m)";

                steps.Add(new RouteStep
                {
                    waypointIndex = i,
                    instructionText = text,
                    distanceMeters = segDist,
                    position = pCurr,
                    turnType = turn
                });
            }

            // Final step: Arrival
            steps.Add(new RouteStep
            {
                waypointIndex = nodes.Count - 1,
                instructionText = $"Arrive at {nodes[nodes.Count - 1].displayName}",
                distanceMeters = 0f,
                position = nodes[nodes.Count - 1].position,
                turnType = TurnType.Arrival
            });

            return steps;
        }

        private static TurnType ClassifyTurn(float signedAngle, CampusNode currNode, CampusNode nextNode)
        {
            if (currNode.nodeType == CampusNodeType.Stairs || nextNode.nodeType == CampusNodeType.Stairs)
            {
                return (nextNode.position.y > currNode.position.y) ? TurnType.StairsUp : TurnType.StairsDown;
            }

            if (currNode.nodeType == CampusNodeType.Ramp || nextNode.nodeType == CampusNodeType.Ramp)
            {
                return TurnType.Ramp;
            }

            if (signedAngle >= -25f && signedAngle <= 25f) return TurnType.Straight;
            if (signedAngle > 25f && signedAngle <= 70f) return TurnType.SlightRight;
            if (signedAngle > 70f && signedAngle <= 120f) return TurnType.TurnRight;
            if (signedAngle > 120f) return TurnType.SharpRight;
            if (signedAngle < -25f && signedAngle >= -70f) return TurnType.SlightLeft;
            if (signedAngle < -70f && signedAngle >= -120f) return TurnType.TurnLeft;
            return TurnType.SharpLeft;
        }

        private static string GetTurnPhrase(TurnType turn)
        {
            switch (turn)
            {
                case TurnType.Straight: return "Continue straight";
                case TurnType.SlightRight: return "Bear slight right";
                case TurnType.TurnRight: return "Turn right";
                case TurnType.SharpRight: return "Take sharp right";
                case TurnType.SlightLeft: return "Bear slight left";
                case TurnType.TurnLeft: return "Turn left";
                case TurnType.SharpLeft: return "Take sharp left";
                case TurnType.StairsUp: return "Take stairs up";
                case TurnType.StairsDown: return "Take stairs down";
                case TurnType.Ramp: return "Follow accessible ramp";
                case TurnType.Arrival: return "Arrive at destination";
                default: return "Continue along path";
            }
        }
    }
}
