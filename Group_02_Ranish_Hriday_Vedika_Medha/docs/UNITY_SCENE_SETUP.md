# Unity Scene Integration Guide

## Project status

This folder is not currently a complete Unity project: it has no `Packages/manifest.json`, `ProjectSettings/`, `.unity` scene, URP asset, XRI action asset, or target/audio assets. The runtime scripts below are source code only. They become a headset application after integration into a Unity 2022.3 LTS project with OpenXR, URP, XR Interaction Toolkit, and the required input system packages installed.

## Create the scene

1. Create/open a Unity 2022.3 LTS project and install URP, OpenXR Plugin 1.8.0 or later, XR Interaction Toolkit 2.5.2 or later, and Input System. Enable OpenXR for the intended build target and configure the matching controller interaction profile.
2. Create `Assets/Scenes`, `Assets/Prefabs`, `Assets/Audio`, and `Assets/Scripts`. Copy the Group 02 C# scripts into `Assets/Scripts`.
3. Create an XR Origin with an XR Camera, an XR Interaction Manager, and left/right Action-based Controller GameObjects. Add the Input Action Manager and XRI default action map. Attach the 6-DoF weapon mesh beneath the right controller and configure its action bindings. The weapon should be a visual/selection prop; this project is a decision-training simulation, not a real weapon controller.
4. Add an AudioSource to an environment object. Set Spatial Blend to 1, Doppler Level to 0, and assign rights-cleared, low-level cue assets. The script caps its Unity gain; Unity volume is not a sound-pressure-level meter. Do not claim 85–95 dB without acoustic measurement at the participant's ear position.
5. Create a spot Light under the XR Camera for the flashlight. Configure its range and spot angle for the scene. Add a separate low-intensity Light aimed at a peripheral wall for the strobe; keep the field of view away from the center of the headset. The implementation limits pulses to six at 6 Hz and caps strobe intensity. Provide a visible stop control and obtain institutional approval for sensory exposure before participant sessions.
6. Create hostile and civilian target prefabs with distinct, non-graphic appearance and colliders. Assign both prefab references and several anchor transforms to `SensoryStressTargetManager`. Keep class cues distinguishable and counterbalance their placement in the study.
7. Add `ReactionLatencyTelemetryLogger` to a scene object, assign the XR Camera transform, and confirm its output directory is writable. Set only a pseudonymous participant code, session code, and condition label. Keep `Data Status` set to `unverified` until the row is from a genuine approved session. Add `SensoryStressTargetManager`, assign lights, cues, prefabs, anchors, and logger. The Unity event-system UI should use XR UI input so controller rays can click the assessment panel.
8. Add a world-space Canvas with a Text question label, Text value label, Slider, Next Button, and `PostTrialAssessmentController`. Assign those controls, a pseudonymous participant code, and a coded session ID in the Inspector. Do not put direct identifiers in the project or telemetry.
9. For each selected/triggered target, call `TargetTrialMetadata.RegisterDecision(true)` from the chosen controller interaction event. The timeout records an ignored decision if no response is made. Start and stop the scenario with explicit UI controls and export after each session.

## Lighting and performance calibration

`RenderSettings` exponential-squared fog is a performant distance-fog approximation. It is not a volumetric fog shader. The flashlight calculation evaluates inverse-square attenuation and exponential fog at the manager's transform; move or replace the reference point with the active target position if the scene requires per-target lux estimation. Unity light intensity is not directly a calibrated lux measurement without scene measurement.

Build a representative scene on the target headset, profile a release build, and record frame-time distributions at the intended refresh rate. Confirm 72 FPS stability and inspect GC allocations in the Unity Profiler before making those performance claims. The 90 Hz head-motion proxy samples at the configured rate but cannot create measurements faster than the frame/update cadence. No result is claimed until profiled on device.

## Export and release

Use a separate empirical output CSV for participant records; do not replace `SIMULATED_*.csv` fixtures with participant data. Validate the generated schema and provenance, back up raw records, and analyze copies. For a release, build a signed Quest APK or PCVR executable, install on a clean device, record build settings and runtime checks, then publish the verified artifact and tag `group-02-v1.0`. The repository alone does not contain the credentials, hardware, approval, or build configuration to complete that release.
