# Group 06 Capstone Submission Manifest

- **Course:** Introduction to Virtual Reality & Augmented Reality (IVRAR - 702TG0C003 / 702COI002)
- **Institution:** SVKM's NMIMS Mukesh Patel School of Technology Management & Engineering (MPSTME)
- **Academic Term:** Academic Year 2026–2027 (Semester V)
- **Group:** Group 06 (Batch B2)
- **Research Title:** *How can an AR visual-marker navigation system using ArUco and QR anchors optimize transit time and route-finding errors across multi-storey university buildings for first-year students?*
- **Submission Date:** Monday, 26-Oct-2026, 23:59 IST
- **Target Release Tag:** `group-06-v1.0`

---

## 1. Student Research Team Roster

| Member Name | Roll No. | SAP ID | Programme | Studio Role | Assigned Branch | Authenticated Account |
| :--- | :---: | :---: | :--- | :--- | :--- | :--- |
| **Mishika Shah** | C136 | 70322200040 | B.Tech (Integrated) | Spatial Vision & AR Lead | `feat/c136-spatial-vision-ar-lead` | Pending individual push |
| **Parva Gaglani** | C172 | 70322200086 | B.Tech (Integrated) | XR Systems Architect | `feat/c172-xr-systems-architect` | Pending individual push |
| **Vansh Panchal** | C139 | 70322200104 | B.Tech (Integrated) | Graph Algorithms & Navigation Specialist | `feat/c139-graph-algorithms-nav` | `xdgalpha` (authenticated) |
| **Triesha Shah** | C174 | 70322200143 | B.Tech (Integrated) | Human Factors & Usability Engineer | `feat/c174-human-factors-usability` | Pending individual push |

---

## 2. Directory Structure & Included Files

```
Group_06 Parva_Mishika_Vansh_Triesha/
├── README.md                                          # Master group documentation & user setup guide
├── submission_manifest.md                             # This file (manifest of deliverables & verification)
├── docs/
│   ├── IVRAR_Group_06_Technical_Dossier.docx          # Primary completed technical dossier (with form controls)
│   ├── IVRAR_Group_06_Technical_Dossier.pdf           # Natively exported 6-page submission PDF
│   ├── LITERATURE_REVIEW_AND_FOUNDATIONAL_PAPERS.md   # Peer-reviewed papers with verified DOIs
│   ├── RESEARCH_PAPER_MANUSCRIPT_BLUEPRINT.md         # 4-page IEEE research manuscript blueprint
│   ├── TEAM_ROSTER.json                               # Machine-readable group metadata & roles
│   ├── toolchain_spec.md                              # Unity 6, Meta XR SDK v207, URP toolchain spec
│   └── figures/
│       ├── figure1_system_architecture.png            # High-level architecture schematic
│       ├── figure2_kinematic_telemetry.png            # Wayfinding trajectory & localization plot
│       ├── figure3_comparative_performance.png        # Comparative transit time & error distributions
│       ├── video_poster.png                           # LinkedIn walkthrough video poster frame
│       └── README.md                                  # Figure provenance documentation
├── source/
│   ├── Assets/                                        # Authoritative Unity assets, scenes, materials, scripts
│   ├── Packages/                                      # Unity Package Manager manifest & lockfile
│   └── ProjectSettings/                               # Complete project settings & Input System bindings
├── builds/
│   ├── smartcampus.apk                                # Standalone Android / Meta Quest executable (83.2 MB)
│   └── BUILD_VERIFICATION_REPORT.md                   # Build settings, SDKs, architecture, verification
└── member_contributions/
    ├── Parva/                                         # Core architecture, procedural campus, VR locomotion
    │   ├── CampusArchitectureBuilder.cs
    │   ├── CampusMaterialPalette.cs
    │   ├── SmartCampusManager.cs
    │   ├── VRPlayerController.cs
    │   ├── CampusRebuildSetup.cs
    │   └── README.md
    ├── Mishika/                                       # Spatial vision, ArUco anchors, literature reviews
    │   ├── ArUcoAnchorPoseManager.cs
    │   ├── LITERATURE_REVIEW_AND_FOUNDATIONAL_PAPERS.md
    │   └── README.md
    ├── Vansh/                                         # A* graph solver, navigation manager, floor arrows, tests
    │   ├── AStarPathfinder.cs
    │   ├── CampusGraph.cs
    │   ├── CampusNode.cs
    │   ├── CampusEdge.cs
    │   ├── GuidanceEngine.cs
    │   ├── RouteManager.cs
    │   ├── RouteArrowManager.cs
    │   ├── ArrowPulsingAnimation.cs
    │   ├── MultiFloorRoutePathfinder.cs
    │   ├── AStarPathfinderTests.cs
    │   └── README.md
    └── Triesha/                                       # World-space UI, kiosk interaction, ray pointer, telemetry
        ├── DestinationMenuUI.cs
        ├── CompactGuidancePanel.cs
        ├── NavigationKioskTerminal.cs
        ├── InteractableDoor.cs
        ├── XRRayPointer.cs
        ├── PlayerLocationTracker.cs
        ├── SceneAuditor.cs
        └── README.md
```

---

## 3. Review Workspace Archives (Root Level)
Four self-contained ZIP archives generated directly in `C:\Users\Vansh\smartcampus\`:
1. `Parva.zip` (25,240 bytes) — Parva's verified source code and documentation.
2. `Mishika.zip` (5,846 bytes) — Mishika's verified source code and documentation.
3. `Vansh.zip` (19,981 bytes) — Vansh's verified source code and documentation.
4. `Triesha.zip` (29,612 bytes) — Triesha's verified source code and documentation.

---

## 4. Exclusions & Git Hygiene Verification
Strict compliance with `.gitignore` and course policies:
- **Zero Heavy Binary Builds in Git History:** `smartcampus.apk` is stored in `builds/` for local grading and attached to GitHub Release `group-06-v1.0`; it is never committed directly to git tracking.
- **Zero Temporary Folders:** `Library/`, `Temp/`, `Obj/`, `Logs/`, `UserSettings/`, and `.vs/` are strictly excluded from source staging.
- **Clean LFS Footprint:** Binary asset rules in `.gitattributes` respected; total clean source size is only 11.88 MB.
- **Zero Legacy Input Calls:** Verified 100% compliant with New Input System (`activeInputHandler: 1`).

---

## 5. Outstanding Tasks & Manual Action Items

| Item | Responsible Member(s) | Status | Action Required |
| :--- | :--- | :---: | :--- |
| **Individual GitHub Commits** | Parva (C172), Mishika (C136), Triesha (C174) | Ready for Auth | Authenticate personal GitHub account, checkout assigned branch, and push commits. |
| **LinkedIn Showcase Video** | All 4 Members | In Progress | Record 3-4 minute walkthrough with all members speaking; post on LinkedIn and paste URL into Section 5. |
| **Live Usability Trials ($N \ge 10$)** | All 4 Members | Scheduled | Conduct empirical testing during Saturday Expo (31-Oct-2026); record raw CSV telemetry and compute SUS scores. |
| **Peer Review Scoring** | All 4 Members | Scheduled | Score Group 07 and Group 15 during Saturday Expo oral defense. |
