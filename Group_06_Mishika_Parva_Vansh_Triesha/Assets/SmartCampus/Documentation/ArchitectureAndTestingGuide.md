# SmartCampus Wayfinding VR Rebuild - Architecture & Testing Guide

## 1. Executive Summary & Overview
The SmartCampus Wayfinding project has been completely rebuilt from scratch inside this existing Unity 6 project workspace. The rebuild replaces the broken, dull, collision-free prototype with an interconnected, fully furnished, collision-enabled, location-aware VR university campus environment.

---

## 2. Critical Existing Constraints & Preservation
- **Unity 6 Version**: `6000.5.7f1`
- **Render Pipeline**: Universal Render Pipeline (URP) `17.5.0`
- **XR SDK Stack**: Meta XR SDK / Meta XR All-In-One SDK `207.0.1` preserved intact.
- **Simulator**: Meta XR Simulator `207.1` registered in OpenXR runtimes and ready for Play Mode.
- **Active Input Handling**: **Input System Package (New) ONLY** (`activeInputHandler: 1`).
  - **Zero legacy `UnityEngine.Input` calls** exist across all project-owned code.
  - Rig locomotion uses `OVRInput` and `UnityEngine.InputSystem.Keyboard` fallback.
  - EventSystem configured with `InputSystemUIInputModule`.

---

## 3. Campus Spatial Architecture & Furnishings
The campus environment was designed with believable architectural proportions, realistic spatial hierarchy, and solid physics collision across every room and doorway:

### A. Arrival Plaza & Campus Entrance (Outdoor: Z = -25m to 0m)
- **Grand Entrance Archway**: Dual concrete pillars and lintel bearing the illuminated signboard `"SMART CAMPUS UNIVERSITY"`.
- **Paved Arrival Plaza**: Stone pavers, landscaped lawns, ornamental planters, shrubbery, park benches, and tall campus lamp posts.
- **Wayfinding Kiosk Terminal**: Physical 3D kiosk at $(3.5, 0, -5.0)$ with an angled screen and proximity interaction prompt HUD.

### B. Reception & Main Lobby (Z = 0m to 16m)
- **Main Entrance Portal**: Wide exterior entryway at $Z = 0$.
- **Curved Reception Desk**: Oak reception counter with staff PC workstation, monitor, and visitor sign-in area.
- **Waiting Lounge**: Modern upholstered seating, armchairs, and oak coffee table.
- **Campus Directory Wall Board**: Room directory listing all wings and destinations.
- **Accent Architectural Walls**: Modern university teal accent finishes with soft ambient point illumination.

### C. Central Corridor Spine & Wing Junctions (Z = 16m to 42m)
- **5-Meter Wide Central Hallway**: Baseboards, directional hanging signs with distance indicators.
- **West Wing Academic Junction** ($Z = 24$): Access portal to Classrooms 101/102 and Computer Lab 204.
- **East Wing Junction** ($Z = 24$): Access portal to Central Library and Student Canteen.
- **Southwest & Southeast Service Bypasses**: Secondary corridors allowing testing and verification of dynamic route recalculations when the central hallway is blocked.

### D. Classroom 101 & Classroom 102 (West Wing)
- **Finished Doorways**: Structural jambs and lintels with interactive hinged oak doors.
- **Signboards**: `"101 - Lecture Hall A"` and `"102 - Seminar Room B"`.
- **Furnishings**: Rows of dual student desks, ergonomic chairs, lecturer podium, and front whiteboards.

### E. Computer Lab 204 (Key Destination)
- **Doorway & Sign**: `"LAB 204 - ADVANCED COMPUTING"`.
- **Furnishings**: Multi-station computer benches with desktop PC towers, widescreen monitors, keyboards, instructor station, and tall network server racks with blinking status LEDs.

### F. Central Library (East Wing)
- **Doorway & Sign**: `"CENTRAL LIBRARY - QUIET STUDY ZONE"`.
- **Furnishings**: Librarian circulation counter, rows of tall oak bookcases stocked with reference book blocks, private study carrels with reading dividers and desk lamps.

### G. Student Canteen & Cafeteria (East Wing)
- **Doorway & Sign**: `"CAMPUS BISTRO & CANTEEN"`.
- **Furnishings**: Long food service counter with stainless steel tray rails, multiple dining tables with four chairs each, and beverage vending machines.

### H. Observation Terrace, Stairs & Accessible Ramp (North Wing: Z = 42m to 56m)
- **Mezzanine Level**: Elevated terrace platform at $Y = 1.8\text{m}$ with steel safety guardrails.
- **Flight of Stairs** (Right side: $X = 2.5\text{m}$): 10 steps rising to $Y = 1.8\text{m}$ (marked as **inaccessible** in graph).
- **ADA-Compliant Ramp** (Left side: $X = -2.5\text{m}$): Smooth gradual incline rising to $Y = 1.8\text{m}$ (marked as **accessible**).
- **Functionality**: Provides a testable physical route comparison where toggling `Step-Free Route` in the menu immediately redirects the navigation path from the stairs to the ramp!

---

## 4. Collision & Physics Locomotion
- **Wall & Doorway Collision**: Every wall is composed of solid `BoxCollider` geometry with cutouts for doorways. The player cannot walk through walls.
- **Interactive Hinged Doors**:
  - Attached to each room with `BoxCollider` on the door leaf.
  - **Closed**: Blocks player passage physically.
  - **Open**: Swings $90^\circ$ around the door hinge, allowing free movement through the doorway.
  - Interacted with via controller laser click or direct trigger.
- **Dynamic HMD Capsule Synchronization (`VRPlayerController`)**:
  - In room-scale VR, walking physically moves the HMD camera inside the tracking space without moving the root GameObject.
  - `VRPlayerController` aligns `CharacterController.center` to `CenterEyeAnchor.localPosition` on the XZ plane every frame and scales capsule height to the user's actual eye height.
  - This prevents the player's head from penetrating physical walls or doors while stationary.

---

## 5. Interaction & Navigation UI System
- **Context-Sensitive Triggering**:
  - The large navigation menu does **not** permanently cover the user's view.
  - It opens only when the player interacts with the **Wayfinding Kiosk Terminal** at the arrival plaza or presses the menu toggle.
  - Can be closed at any time via the `"X"` button.
- **100% Reliable Pointer Click Execution (`XRRayPointer`)**:
  - Uses `ExecuteEvents.GetEventHandler<IPointerClickHandler>(target)` to traverse parent hierarchy up to the Button component when the laser hits child Text or Image elements.
  - Dispatches `IPointerClickHandler`, `ISubmitHandler`, and direct `Button.onClick.Invoke()`.
  - Provides visual hover highlights and reticle feedback.
- **Destination Menu Features**:
  - Category filtering tabs: *All*, *Academic Wing*, *Computing Labs*, *Library*, *Amenities*.
  - Destination information panel: Title, Category, Floor level, and Description.
  - Real-time pre-calculated route distance and estimated walking time from the player's current ground position.
  - **Start Navigation** (Emerald green) & **Cancel / Reset** (Ruby red).
  - **Step-Free Route Toggle**: Forces route through ramps and accessible corridors.
  - **Corridor Closure Simulation Toggle**: Closes the primary central hallway to verify dynamic A* route recalculation in real-time.

---

## 6. Location-Aware A* Navigation & Directional Arrows
- **Campus Graph (`CampusGraph`)**:
  - 17 named nodes spanning the entire campus grounds, lobby, corridors, rooms, stairs, and terrace.
  - 23 bidirectional edges with accurate base distances, accessibility tags, and simulated crowd penalties.
- **A* Pathfinder (`AStarPathfinder`)**:
  - Admissible Euclidean distance heuristic $f(n) = g(n) + h(n)$.
  - Filters out closed edges and inaccessible stair edges when step-free mode is requested.
  - Computes total route distance and estimated walking time based on average walking speed ($1.35\text{ m/s}$).
- **Walkable NavMesh Path Smoothing (`RouteManager`)**:
  - Smooths graph segments using Unity 6 `NavMesh.CalculatePath` across the baked `NavMeshSurface`.
- **Dynamic Floor Arrows (`RouteArrowManager`)**:
  - Procedural 3D chevron arrow meshes spawned every $1.25\text{m}$ along the path.
  - Elevated at $+0.04\text{m}$ above the floor to completely eliminate z-fighting.
  - Oriented precisely along segment forward headings and turn angles.
  - Animated with subtle forward ripple and alpha breathing (`ArrowPulsingAnimation`).
  - Destination beacon at target with arrival target ring, vertical light beam, and rotating diamond marker.
  - Arrows behind the player are automatically deactivated as progress is made.
- **Off-Route Detection & Recalculation (`PlayerLocationTracker`)**:
  - Continuously projects HMD position to ground level.
  - Detects if the player drifts $> 3.5\text{m}$ off the designated path and triggers automatic route recalculation.
  - Detects arrival when within $1.8\text{m}$ of the target and clears the route with arrival audio/banner feedback.

---

## 7. Recommended Unity Asset Store Models & Official Links
While the procedural architecture and furnishings provide a complete, cohesive, functional baseline, the following official Unity Asset Store packs can be imported to add high-fidelity scanned props and modular models:

1. **POLYGON - Office & Academy Pack (by Synty Studios)**:
   - Official Store Page: [https://assetstore.unity.com/publishers/16638](https://assetstore.unity.com/publishers/16638)
   - Compatibility: Unity 6 / URP compatible.
   - Contains: Detailed modular campus walls, lecture hall chairs, chemistry equipment, library desks, and indoor plants.
2. **Modular School Interior (URP)**:
   - Official Store Page: [https://assetstore.unity.com/packages/3d/environments/interior-modular-school-pack-164741](https://assetstore.unity.com/packages/3d/environments/interior-modular-school-pack-164741)
   - Compatibility: URP PBR materials.
   - Contains: Lockers, classroom chalkboards, laboratory fume hoods, cafeteria trays.
3. **Unity Technologies - Starter Assets (First Person / XR URP)**:
   - Official Store Page: [https://assetstore.unity.com/packages/essentials/starter-assets-firstperson-updates-regularly-193405](https://assetstore.unity.com/packages/essentials/starter-assets-firstperson-updates-regularly-193405)
   - Compatibility: Standard Unity 6 Package Manager download.

---

## 8. Verification & Test Report
- **EditMode Unit Tests**: `AStarPathfinderTests` (5/5 passed, 100% success rate):
  1. `Pathfinding_FindsDirectShortestRoute_EntranceToLobby` -> **PASSED**
  2. `Pathfinding_StepFreeRouting_ChoosesRampOverStairs` -> **PASSED**
  3. `Pathfinding_CorridorClosure_ReroutesThroughBypass` -> **PASSED**
  4. `Pathfinding_UnreachableDestination_ReturnsGracefulFailure` -> **PASSED**
  5. `Pathfinding_CalculatesAccurateDistanceAndTime` -> **PASSED**
- **Legacy Input Audit**: `audit_input_calls.ps1` -> **PASSED** (0 occurrences of `UnityEngine.Input` in project C# code).
- **Scene Build Output**: Saved to `Assets/Scenes/SampleScene.unity` and `Assets/SmartCampus/Scenes/SmartCampusScene.unity`.
