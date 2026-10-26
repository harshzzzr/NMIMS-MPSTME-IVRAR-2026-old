# Member Contribution: Parva Gaglani

- **Student Researcher:** Parva Gaglani
- **Roll Number:** C172
- **SAP ID:** 70322200086
- **Programme:** B.Tech Computer Engineering (Integrated) · Batch B2
- **Declared Studio Role:** XR Systems Architect & VIO Drift Calibration Lead
- **Assigned Git Branch:** `feat/c172-xr-systems-architect`
- **Agreed Project Effort:** 25%

---

## 1. Scope of Contribution & Architecture Responsibilities
Parva served as the **XR Systems Architect** for Group 06, overseeing core system lifecycle management, 3D architectural synthesis, runtime locomotion, and scene wiring:

1. **Procedural Campus Architecture (`CampusArchitectureBuilder.cs` - 825 lines):**
   - Procedural generation of the 3D multi-wing university campus environment (Arrival Plaza, Reception Lobby, West Wing Classrooms, North Wing Laboratories & Central Library, East Wing Canteen, and Mezzanine Study Terrace).
   - Placement of physical colliders, floor planes, archways, and architectural structural bounds preventing player clipping.
2. **Campus Material & Shader Synthesis (`CampusMaterialPalette.cs` - 62 lines):**
   - Construction of URP-compliant materials, color schemes, emissive trim shaders, and floor surfaces optimized for XR forward rendering.
3. **Master Runtime Coordination (`SmartCampusManager.cs` - 241 lines):**
   - Central orchestration of player spawning, kiosk proximity zones, and communication between navigation and UI subsystems.
4. **VR Locomotion Rig (`VRPlayerController.cs` - 224 lines):**
   - XR locomotion using New Input System actions and `OVRInput` thumbstick movement without legacy `UnityEngine.Input` dependencies.
   - Smooth locomotion, snap turns, and CharacterController ground alignment.
5. **Automated Scene Setup (`CampusRebuildSetup.cs` - 928 lines):**
   - Editor tooling to assemble campus geometry, assign materials, and wire scene references deterministically.

---

## 2. Attributed Files in this Directory
- `CampusArchitectureBuilder.cs`
- `CampusMaterialPalette.cs`
- `SmartCampusManager.cs`
- `VRPlayerController.cs`
- `CampusRebuildSetup.cs`

*Shared project assets, scenes, and project configurations reside in the authoritative `source/` directory.*
