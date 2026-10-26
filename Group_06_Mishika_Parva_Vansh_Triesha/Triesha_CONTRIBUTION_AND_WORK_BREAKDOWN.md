# Member Contribution: Triesha Shah

- **Student Researcher:** Triesha Shah
- **Roll Number:** C174
- **SAP ID:** 70322200143
- **Programme:** B.Tech Computer Engineering (Integrated) · Batch B2
- **Declared Studio Role:** Human Factors, AR UI/UX & Telemetry Engineer (Human Factors & Usability Engineer)
- **Assigned Git Branch:** `feat/c174-human-factors-usability`
- **Agreed Project Effort:** 25%

---

## 1. Scope of Contribution & Usability Engineering Responsibilities
Triesha served as the **Human Factors & Usability Engineer** for Group 06, designing the World-Space Kiosk UI, interactive raycasting, user localization tracking, and automated UI audit verification:

1. **Interactive World-Space Kiosk UI (`DestinationMenuUI.cs` - 848 lines):**
   - World-Space Canvas UI at $(0, 1.35, -14.0)$ featuring categorical campus facility listings (Classrooms, Labs, Library, Canteen, Terrace).
   - Dynamic destination details panel displaying name, floor level, estimated walking distance, estimated walking time ($v = 1.35\text{ m/s}$), and real-time turn guidance banners.
   - Clean non-overlapping vertical layout verified by automated UI layout assertions.
   - Step-free accessibility toggle enabling wheelchair route planning.
2. **Kiosk Interaction Terminal (`NavigationKioskTerminal.cs` - 251 lines):**
   - Proximity detection ($3.5\text{m}$ trigger zone) that activates the wayfinding interface when the user approaches the reception area.
3. **VR Controller Ray Pointer & UI Interaction (`XRRayPointer.cs` - 758 lines):**
   - Controller laser raycasting compatible with the New Input System and `InputSystemUIInputModule`, dispatching `IPointerClickHandler` events without legacy Input dependencies.
4. **Player Localization & Deviation Tracking (`PlayerLocationTracker.cs` - 194 lines):**
   - Continuous 20–30 Hz head localization tracking via `CenterEyeAnchor` of the Meta XR camera rig, calculating perpendicular distance to active route segments and raising deviation events if off-route $> 3.5\text{m}$.
5. **Interactive Door Mechanics (`InteractableDoor.cs` - 117 lines):**
   - Proximity and raycast-triggered door swing animations for classroom and laboratory entrance boundaries.
6. **HUD Guidance Panel (`CompactGuidancePanel.cs` - 364 lines):**
   - Head-mounted compact turn-by-turn guidance display updating dynamically as waypoints are traversed.
7. **Automated Scene & UI Auditor (`SceneAuditor.cs` - 871 lines):**
   - Editor tool to validate camera rig tracking, kiosk trigger colliders, line-of-sight clearance, UI text component bounds, and navigation start button callbacks.

---

## 2. Attributed Files in this Directory
- `DestinationMenuUI.cs`
- `CompactGuidancePanel.cs`
- `NavigationKioskTerminal.cs`
- `InteractableDoor.cs`
- `XRRayPointer.cs`
- `PlayerLocationTracker.cs`
- `SceneAuditor.cs`

*Shared project assets, scenes, and project configurations reside in the authoritative `source/` directory.*
