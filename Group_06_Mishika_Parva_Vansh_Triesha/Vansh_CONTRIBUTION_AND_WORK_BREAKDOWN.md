# Member Contribution: Vansh Panchal

- **Student Researcher:** Vansh Panchal
- **Roll Number:** C139
- **SAP ID:** 70322200104
- **Programme:** B.Tech Computer Engineering (Integrated) · Batch B2
- **Declared Studio Role:** 3D Multi-Floor A* Graph & Pathfinding Specialist (Graph Algorithms & Navigation Specialist)
- **Assigned Git Branch:** `feat/c139-graph-algorithms-nav`
- **Authenticated GitHub Identity:** `xdgalpha` (`vanshpanchal210706@gmail.com`)
- **Agreed Project Effort:** 25%

---

## 1. Scope of Contribution & Engineering Responsibilities
Vansh served as the **Graph Algorithms & Navigation Specialist** for Group 06, architecting the 3D multi-floor graph solver, dynamic route calculation, procedural direction arrow generator, and automated test suite:

1. **A* Shortest-Path Graph Engine (`AStarPathfinder.cs` - 241 lines, `CampusGraph.cs` - 260 lines):**
   - Implementation of deterministic A* search algorithm: $f(n) = g(n) + h(n)$ with Euclidean distance heuristic and crowd penalty weighting.
   - Dynamic corridor closure handling: closed hallways are assigned impassable penalties ($10^7$), causing the solver to identify alternative walkways automatically.
   - Step-free accessibility routing (`requireStepFree = true`): dynamically filters non-accessible edges (such as stairs) to route wheelchair users via accessible ramps.
2. **Graph Topology Model (`CampusNode.cs` - 62 lines, `CampusEdge.cs` - 59 lines, `MultiFloorRoutePathfinder.cs` - 52 lines):**
   - 19 interconnected campus landmarks and 20 bidirectional edges covering multi-wing circulation and upper study terrace.
3. **Turn-by-Turn Guidance Engine (`GuidanceEngine.cs` - 148 lines):**
   - Real-time turn instruction generator calculating signed angles between sequential route vectors (`Depart`, `Straight`, `SlightLeft`, `Left`, `SlightRight`, `Right`, `Arrived`).
4. **Active Route Management (`RouteManager.cs` - 336 lines):**
   - Runtime route executor managing active destination, waypoint advancement within $1.3\text{m}$, and automatic off-route recalculation when deviation $> 3.5\text{m}$.
5. **Directional Arrow & Ribbon Visuals (`RouteArrowManager.cs` - 374 lines, `ArrowPulsingAnimation.cs` - 56 lines):**
   - Procedural generation of 3D floor arrows spaced every $1.3\text{m}$ elevated $+0.04\text{m}$ to eliminate Z-fighting, with continuous forward pulsing breathing animations and animated destination beacons.
6. **Automated Integration Test Suite (`AStarPathfinderTests.cs` - 186 lines):**
   - NUnit EditMode test fixture asserting graph integrity, route calculation from Entrance to Lab 204, step-free routing via ramps, and corridor closure rerouting. (100% Pass rate verified in CLI).

---

## 2. Attributed Files in this Directory
- `AStarPathfinder.cs`
- `CampusGraph.cs`
- `CampusNode.cs`
- `CampusEdge.cs`
- `GuidanceEngine.cs`
- `RouteManager.cs`
- `RouteArrowManager.cs`
- `ArrowPulsingAnimation.cs`
- `MultiFloorRoutePathfinder.cs`
- `AStarPathfinderTests.cs`

*Shared project assets, scenes, and project configurations reside in the authoritative `source/` directory.*
