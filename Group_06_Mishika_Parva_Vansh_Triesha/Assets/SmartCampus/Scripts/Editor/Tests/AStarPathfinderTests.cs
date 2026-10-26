using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SmartCampus.Navigation;

namespace SmartCampus.Tests
{
    [TestFixture]
    public class AStarPathfinderTests
    {
        private GameObject testGameObject;
        private CampusGraph graph;

        [SetUp]
        public void SetUp()
        {
            testGameObject = new GameObject("TestCampusGraph");
            graph = testGameObject.AddComponent<CampusGraph>();

            // Setup a representative test campus layout:
            //
            //              [Terrace]
            //             / (Ramp)   \ (Stairs)
            //     [RampBottom]     [StairBottom]
            //            \            /
            //          [NorthHallway]
            //                |
            //          [CentralJunction] --------- [AcademicHallway] --- [Class101] --- [Lab204]
            //                |                           |
            //             (Spine)                 [SouthwestBypass]
            //                |                           |
            //             [Lobby] -----------------------+
            //                |
            //            [Entrance]

            // Nodes
            graph.AddNode(new CampusNode("node_entrance", "Entrance", new Vector3(0, 0, -18), CampusNodeType.Entrance, DestinationCategory.All, true, 0));
            graph.AddNode(new CampusNode("node_lobby", "Lobby", new Vector3(0, 0, 6), CampusNodeType.Reception, DestinationCategory.Amenities, true, 0));
            graph.AddNode(new CampusNode("node_central_junction", "Central Junction", new Vector3(0, 0, 24), CampusNodeType.Junction, DestinationCategory.All, true, 0));
            graph.AddNode(new CampusNode("node_north_hallway", "North Hallway", new Vector3(0, 0, 40), CampusNodeType.Corridor, DestinationCategory.All, true, 0));
            graph.AddNode(new CampusNode("node_academic_hallway", "Academic Hallway", new Vector3(-6, 0, 24), CampusNodeType.Corridor, DestinationCategory.Academic, true, 0));
            
            // Classroom 101 with corridor and door nodes
            graph.AddNode(new CampusNode("node_corridor_class101", "Corridor Class 101", new Vector3(-6.5f, 0, 18), CampusNodeType.Corridor, DestinationCategory.Academic, true, 0));
            graph.AddNode(new CampusNode("node_door_class101", "Door Class 101", new Vector3(-10, 0, 18), CampusNodeType.Corridor, DestinationCategory.Academic, true, 0));
            graph.AddNode(new CampusNode("node_class101", "Classroom 101", new Vector3(-15, 0, 18), CampusNodeType.Classroom, DestinationCategory.Academic, true, 0));

            // Lab 204 with corridor and door nodes
            graph.AddNode(new CampusNode("node_corridor_lab204", "Corridor Lab 204", new Vector3(-6.5f, 0, 44), CampusNodeType.Corridor, DestinationCategory.Labs, true, 0));
            graph.AddNode(new CampusNode("node_door_lab204", "Door Lab 204", new Vector3(-10, 0, 44), CampusNodeType.Corridor, DestinationCategory.Labs, true, 0));
            graph.AddNode(new CampusNode("node_lab204", "Lab 204", new Vector3(-15, 0, 44), CampusNodeType.ComputerLab, DestinationCategory.Labs, true, 0));

            // Terrace, Stairs, and Ramp
            graph.AddNode(new CampusNode("node_stairs_bottom", "Stairs Bottom", new Vector3(2.5f, 0, 42), CampusNodeType.Stairs, DestinationCategory.Amenities, false, 0));
            graph.AddNode(new CampusNode("node_ramp_bottom", "Ramp Bottom", new Vector3(-2.5f, 0, 42), CampusNodeType.Ramp, DestinationCategory.Amenities, true, 0));
            graph.AddNode(new CampusNode("node_terrace", "Terrace", new Vector3(0, 1.8f, 52), CampusNodeType.Terrace, DestinationCategory.Amenities, true, 1));

            // Bypass node
            graph.AddNode(new CampusNode("node_sw_bypass", "SW Bypass", new Vector3(-10, 0, 12), CampusNodeType.Corridor, DestinationCategory.All, true, 0));

            // Edges (Physical corridor & door connections)
            graph.AddEdge(new CampusEdge("node_entrance", "node_lobby", 24f, true, true));
            graph.AddEdge(new CampusEdge("node_lobby", "node_central_junction", 18f, true, true));
            graph.AddEdge(new CampusEdge("node_central_junction", "node_academic_hallway", 6f, true, true));
            graph.AddEdge(new CampusEdge("node_academic_hallway", "node_corridor_class101", 6f, true, true));
            graph.AddEdge(new CampusEdge("node_corridor_class101", "node_door_class101", 3.5f, true, true));
            graph.AddEdge(new CampusEdge("node_door_class101", "node_class101", 5f, true, true));

            graph.AddEdge(new CampusEdge("node_corridor_class101", "node_corridor_lab204", 26f, true, true));
            graph.AddEdge(new CampusEdge("node_corridor_lab204", "node_door_lab204", 3.5f, true, true));
            graph.AddEdge(new CampusEdge("node_door_lab204", "node_lab204", 5f, true, true));

            // Stairs edge (isAccessible = false)
            graph.AddEdge(new CampusEdge("node_central_junction", "node_north_hallway", 16f, true, true));
            graph.AddEdge(new CampusEdge("node_north_hallway", "node_stairs_bottom", 3.2f, true, true));
            graph.AddEdge(new CampusEdge("node_stairs_bottom", "node_terrace", 10.4f, true, false)); // Stairs: NOT accessible

            // Ramp edge (isAccessible = true, but slightly longer distance)
            graph.AddEdge(new CampusEdge("node_north_hallway", "node_ramp_bottom", 3.2f, true, true));
            graph.AddEdge(new CampusEdge("node_ramp_bottom", "node_terrace", 12.0f, true, true)); // Ramp: Accessible

            // Southwest bypass (Lobby -> Bypass -> Corridor outside Class101)
            graph.AddEdge(new CampusEdge("node_lobby", "node_sw_bypass", 15f, true, true));
            graph.AddEdge(new CampusEdge("node_sw_bypass", "node_corridor_class101", 8f, true, true));

            graph.RebuildLookup();
        }

        [TearDown]
        public void TearDown()
        {
            if (testGameObject != null)
            {
                Object.DestroyImmediate(testGameObject);
            }
        }

        [Test]
        public void Pathfinding_FindsDirectShortestRoute_EntranceToLobby()
        {
            var result = AStarPathfinder.FindRoute(graph, "node_entrance", "node_lobby", false);

            Assert.IsTrue(result.isSuccess, "Route from entrance to lobby should succeed.");
            Assert.AreEqual(2, result.pathNodes.Count);
            Assert.AreEqual("node_entrance", result.pathNodes[0].nodeId);
            Assert.AreEqual("node_lobby", result.pathNodes[1].nodeId);
            Assert.AreEqual(24f, result.totalDistance, 0.01f);
        }

        [Test]
        public void Pathfinding_StepFreeRouting_ChoosesRampOverStairs()
        {
            // Default route (can use stairs)
            var normalRoute = AStarPathfinder.FindRoute(graph, "node_north_hallway", "node_terrace", false);
            Assert.IsTrue(normalRoute.isSuccess);
            bool normalUsesStairs = normalRoute.pathNodes.Exists(n => n.nodeId == "node_stairs_bottom");
            Assert.IsTrue(normalUsesStairs, "Default route should take stairs since it is shorter (10.4m vs 12.0m).");

            // Step-free route (must avoid stairs)
            var stepFreeRoute = AStarPathfinder.FindRoute(graph, "node_north_hallway", "node_terrace", true);
            Assert.IsTrue(stepFreeRoute.isSuccess);
            bool stepFreeUsesStairs = stepFreeRoute.pathNodes.Exists(n => n.nodeId == "node_stairs_bottom");
            bool stepFreeUsesRamp = stepFreeRoute.pathNodes.Exists(n => n.nodeId == "node_ramp_bottom");

            Assert.IsFalse(stepFreeUsesStairs, "Step-free route must NOT use stairs.");
            Assert.IsTrue(stepFreeUsesRamp, "Step-free route MUST use accessible ramp.");
        }

        [Test]
        public void Pathfinding_CorridorClosure_ReroutesThroughBypass()
        {
            // Close the primary academic hallway between central junction and academic hallway
            graph.SetEdgeClosed("node_central_junction", "node_academic_hallway", true);

            var route = AStarPathfinder.FindRoute(graph, "node_central_junction", "node_class101", false);

            Assert.IsTrue(route.isSuccess, "Route should succeed via the bypass.");
            // Route must go CentralJunction -> Lobby -> SW Bypass -> Class101
            bool usesBypass = route.pathNodes.Exists(n => n.nodeId == "node_sw_bypass");
            Assert.IsTrue(usesBypass, "Route must reroute through SW Bypass when main academic corridor is closed.");
        }

        [Test]
        public void Pathfinding_UnreachableDestination_ReturnsGracefulFailure()
        {
            // Add a completely disconnected node
            graph.AddNode(new CampusNode("node_isolated_rooftop", "Isolated Rooftop", new Vector3(50, 20, 50), CampusNodeType.Terrace, DestinationCategory.All, false, 3));
            graph.RebuildLookup();

            var result = AStarPathfinder.FindRoute(graph, "node_entrance", "node_isolated_rooftop", false);

            Assert.IsFalse(result.isSuccess, "Path to isolated node should fail.");
            Assert.IsNotEmpty(result.errorMessage, "Failure should provide a clear error explanation.");
        }

        [Test]
        public void Pathfinding_CalculatesAccurateDistanceAndTime()
        {
            var result = AStarPathfinder.FindRoute(graph, "node_entrance", "node_lobby", false, 1.5f);

            Assert.IsTrue(result.isSuccess);
            Assert.AreEqual(24f, result.totalDistance, 0.1f);
            // Time = 24m / 1.5m/s = 16 seconds
            Assert.AreEqual(16f, result.estimatedTimeSeconds, 0.1f);
            Assert.AreEqual("16s", result.GetFormattedTime());
        }

        [Test]
        public void Pathfinding_EnforcesDoorwayRouting_Lab204EnteredThroughDoorwayOnly()
        {
            var result = AStarPathfinder.FindRoute(graph, "node_entrance", "node_lab204", false);
            Assert.IsTrue(result.isSuccess, "Route to Lab 204 must succeed.");

            int count = result.pathNodes.Count;
            Assert.AreEqual("node_door_lab204", result.pathNodes[count - 2].nodeId, "Must enter Lab 204 through its designated doorway.");
            Assert.AreEqual("node_lab204", result.pathNodes[count - 1].nodeId);

            for (int i = 0; i < count - 1; i++)
            {
                string a = result.pathNodes[i].nodeId;
                string b = result.pathNodes[i + 1].nodeId;
                Assert.IsFalse(a == "node_class101" && b == "node_lab204", "Route must not cut directly through wall between Class 101 and Lab 204.");
            }
        }
    }
}
