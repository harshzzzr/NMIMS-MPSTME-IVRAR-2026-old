# Research and Implementation Guide: VR Emergency Evacuation Simulator

## Project: IVRAR Group 03
## Target Venue: IEEE VR / ACM VRST / Fire Safety Journal

---

## 1. Mathematical and Algorithmic Formulation

### 1.1 Microscopic Crowd Dynamics: Helbing Social Force Model
Crowd occupant movement in the hostel corridors and stairwells is modeled as a system of interacting particles governed by generalized Newtonian dynamics:

$$m_i \frac{d\mathbf{v}_i}{dt} = \mathbf{f}_i^0 + \sum_{j \ne i} \mathbf{f}_{ij} + \sum_W \mathbf{f}_{iW}$$

where:
1. **Desired Driving Force ($\mathbf{f}_i^0$):** Accelerates agent $i$ toward the target exit portal with relaxation time $\tau_i \approx 0.5$ s:
   $$\mathbf{f}_i^0 = m_i \frac{\mathbf{v}_i^0(t) - \mathbf{v}_i(t)}{\tau_i}$$
2. **Interpersonal Repulsive & Friction Force ($\mathbf{f}_{ij}$):** Prevents agent overlapping and models body compression under crowd pressure:
   $$\mathbf{f}_{ij} = \left[ A_i \exp\left(\frac{r_{ij} - d_{ij}}{B_i}\right) + k \, g(r_{ij} - d_{ij}) \right] \mathbf{n}_{ij} + \kappa \, g(r_{ij} - d_{ij}) (\Delta \mathbf{v}_{ji} \cdot \mathbf{t}_{ij}) \mathbf{t}_{ij}$$
   where $r_{ij} = r_i + r_j$ is the sum of agent radii, $d_{ij} = \|\mathbf{x}_i - \mathbf{x}_j\|$ is center-to-center distance, $\mathbf{n}_{ij}$ is the normalized vector pointing from $j$ to $i$, $\mathbf{t}_{ij}$ is the tangential direction, $g(x) = \max(0, x)$, $k = 1.2 \times 10^5 \text{ kg/s}^2$ is the elastic force constant, and $\kappa = 2.4 \times 10^5 \text{ kg/(m}\cdot\text{s)}$ is the sliding friction coefficient.
3. **Wall Repulsion ($\mathbf{f}_{iW}$):** Repels occupants away from corridor walls and closed doors.

### 1.2 Doorway Bottleneck Flow & NBC / NFPA 101 Compliance
According to the National Building Code (NBC 2016) of India and NFPA 101, maximum stairwell door egress flow rate $Q_{\text{crit}}$ is governed by:

$$Q_{\text{crit}} = W_{\text{door}} \times q_{\text{max}}$$

where $W_{\text{door}} = 1.2$ m is the doorway clear width and $q_{\text{max}} = 1.8 \text{ persons} / \text{s} / \text{m width}$. The critical threshold corresponds to an unobstructed capacity of $2.16 \text{ persons/second}$. When crowd density at the portal exceeds critical density ($\rho > \rho_{\text{crit}} \approx 4.0 \text{ persons/m}^2$), arching jams form, dropping effective flow rate $q$ to $1.33 \text{ p/s/m}$ (the classic "faster-is-slower" phenomenon).

### 1.3 Smoke Occlusion & Beer-Lambert Velocity Degradation
The optical density of smoke particles reduces agent walking speed exponentially via the Beer-Lambert law:

$$\mathbf{v}_i^0(\rho_{\text{smoke}}) = \mathbf{v}_i^0(0) \cdot \exp(-\alpha_{\text{ext}} \cdot \rho_{\text{smoke}})$$

where $\alpha_{\text{ext}} = 0.45 \text{ m}^2/\text{g}$ is the specific extinction coefficient and $\rho_{\text{smoke}} \in [0, 1.0]$ represents smoke concentration in the corridor volume.

### 1.4 Inter-Warden Radio Dispatch Latency
Coordination efficiency between the Head Warden and Student Floor Marshals is measured via command dispatch latency:

$$T_{\text{dispatch}} = t_{\text{acknowledge}} - t_{\text{issue}}$$

where $t_{\text{issue}}$ is the millisecond timestamp of the warden's evacuation detour broadcast, and $t_{\text{acknowledge}}$ is the floor marshal's biometric confirmation timestamp logged across the Netcode RPC synchronization network.

### 1.5 Technoeconomic Cost Parity
The university hostel evacuation drill feasibility model compares annual full-building physical drill disruptions against collaborative VR training using dimensionless cost parity $\kappa$:

$$\kappa = \frac{\text{OpEx}_{\text{VR}}}{\text{OpEx}_{\text{Physical}}} = \frac{C_{\text{hmd\_maintenance}} + C_{\text{scenario\_refresh}}}{C_{\text{resident\_disruption}} + C_{\text{warden\_overtime}} + C_{\text{consultant\_fees}}}$$

The capital investment payback horizon in operating months is:

$$\text{Payback Months} = \frac{K_{\text{capex}}}{1 - \kappa} \times 12$$

---

## 2. Individual Student Work Boundaries & Responsibilities

```
===================================================================================================
Student Roll & Name        Assigned Technical Module                       Primary Deliverable
===================================================================================================
C068 - Yashika Patil       Spatial AI & Crowd Navigation Lead              Assets/Scripts/EvacuationEgressManager.cs
                                                                           (Helbing Social Force Physics Engine)
C107 - Moksh Shah          XR Systems Architect & Multi-Floor Environment  Assets/Scripts/EvacuationEgressManager.cs
                                                                           (Hostel Geometry, Portals, OpenXR Rig)
C078 - Bhavi Doshi         Human Factors & Usability Engineer              telemetry/test_evaluation_tools.py
                                                                           (NASA-TLX Workload, Egress Metrics)
C067 - Preet Shah          Network & Coordination Specialist               Assets/Scripts/BottleneckTelemetryLogger.cs
                                                                           (Netcode Synchronization & Dispatch CSV)
===================================================================================================
```

### 2.1 C068 - Yashika Patil (Spatial AI & Crowd Navigation Lead)
- Implement microscopic Helbing Social Force equations governing agent interpersonal pushing and sliding friction.
- Author smoke-induced walking speed attenuation logic based on optical density.
- Validate corridor collision avoidance and bottleneck arching formations.
- **Git Branch:** `feat/c068-spatial-ai-crowd-nav`
- **Oral Viva Focus:** Social force vector equations, parameter tuning for tangential friction ($k$ and $\kappa$), and mathematical proof of the "faster-is-slower" effect at narrow doorways.

### 2.2 C107 - Moksh Shah (XR Systems Architect)
- Construct the 5-story hostel architectural model in Unity 2022.3 conforming to NBC 2016 dimensions ($1.8$ m corridors, $1.2$ m stairwells).
- Configure OpenXR head-mounted display tracking, teleport/smooth locomotion boundaries, and stairwell trigger portals.
- Integrate dynamic volumetric smoke particle systems and emergency lighting strobes.
- **Git Branch:** `feat/c107-xr-systems-architect`
- **Oral Viva Focus:** OpenXR runtime optimization, draw call batching in multi-story environments, NavMesh obstacle carving under dynamic door blockages, and motion-to-photon latency.

### 2.3 C078 - Bhavi Doshi (Human Factors & Usability Engineer)
- Formulate the within-subjects empirical evaluation protocol across $N = 50$ simulated trials.
- Implement in-VR NASA-TLX cognitive workload assessment canvases and evaluate Kennedy SSQ cybersickness scores.
- Analyze pre-evacuation delay distributions ($T_{\text{pre}}$) and calculate Student's t-test and Cohen's $d$ effect sizes.
- **Git Branch:** `feat/c078-human-factors-usabil`
- **Oral Viva Focus:** Human behavioral egress decision modeling, pre-movement delay distribution across hostel floors, and NASA-TLX workload subscale interpretation.

### 2.4 C067 - Preet Shah (Network & Coordination Specialist)
- Author `BottleneckTelemetryLogger.cs` with 90 Hz CSV data capture for doorway flux, jam durations, and dispatch latencies.
- Implement Netcode for GameObjects RPC event broadcasting for head warden commands and marshal wrist terminals.
- Model network latency compensation, packet loss handling, and inter-warden coordination metrics.
- **Git Branch:** `feat/c067-network-coordination`
- **Oral Viva Focus:** Network state synchronization, RPC event sequencing, clock drift correction, and continuous doorway discharge rate derivation.

---

## 3. Verified Foundational Papers

The project architecture and empirical protocol are grounded in 6 verified literature foundations:

1. **Helbing & Molnar (1995)**
   - *Title:* Social force model for pedestrian dynamics
   - *Journal:* Physical Review E, vol. 51, no. 5, pp. 4282-4286
   - *DOI:* [10.1103/PhysRevE.51.4282](https://doi.org/10.1103/PhysRevE.51.4282)
   - *Role:* Theoretical and mathematical formulation of microscopic pedestrian forces.

2. **Helbing, Farkas, & Vicsek (2000)**
   - *Title:* Simulating dynamical features of escape panic
   - *Journal:* Nature, vol. 407, no. 6803, pp. 487-495
   - *DOI:* [10.1038/35035023](https://doi.org/10.1038/35035023)
   - *Role:* Jamming physics, tangential friction, and doorway arching blockage models.

3. **Kobes, Helsloot, de Vries, & Post (2010)**
   - *Title:* Building safety and human behaviour in fire: A literature review
   - *Journal:* Fire Safety Journal, vol. 45, no. 1, pp. 1-11
   - *DOI:* [10.1016/j.firesaf.2009.08.005](https://doi.org/10.1016/j.firesaf.2009.08.005)
   - *Role:* Pre-evacuation behavioral delay analysis and environmental cues.

4. **Kinateder et al. (2014)**
   - *Title:* Virtual Reality for Fire Evacuation Research
   - *Journal:* Annals of Computer Science and Information Systems, vol. 2, pp. 313-321
   - *DOI:* [10.15439/2014F94](https://doi.org/10.15439/2014F94)
   - *Role:* Ecological validity and participant immersion methodologies in VR fire trials.

5. **Feng, González, Amor, Lovreglio, & Cabrera-Guerrero (2018)**
   - *Title:* Immersive virtual reality serious games for evacuation training
   - *Journal:* Computers & Education, vol. 127, pp. 252-266
   - *DOI:* [10.1016/j.compedu.2018.09.002](https://doi.org/10.1016/j.compedu.2018.09.002)
   - *Role:* Systematic review of VR training efficacy, game mechanics, and knowledge retention.

6. **Sano, Ronchi, Minegishi, & Nilsson (2017)**
   - *Title:* A pedestrian merging flow model for stair evacuation
   - *Journal:* Fire Safety Journal, vol. 89, pp. 77-89
   - *DOI:* [10.1016/j.firesaf.2017.02.008](https://doi.org/10.1016/j.firesaf.2017.02.008)
   - *Role:* Mathematical models for multi-story stairwell merging ratios and door flux.

---

## 4. Step-by-Step Implementation Roadmap

1. **Sprint 0: Toolchain & Baseline Verification**
   - Verify Unity 2022.3 LTS, OpenXR plugin, and Netcode for GameObjects packages.
   - Run `python telemetry/evacuation_economics.py` to confirm technoeconomic parity parameters.
2. **Sprint 1: Hostel Architecture & Social Force Navigation**
   - Model multi-story hostel corridors and stairwells conforming to NBC 2016.
   - Implement `Assets/Scripts/EvacuationEgressManager.cs` with student `# TODO` implementations.
3. **Sprint 2: Network Coordination & Telemetry Logger**
   - Author `Assets/Scripts/BottleneckTelemetryLogger.cs` for 90 Hz CSV telemetry capture.
   - Implement inter-warden radio dispatch latency tracking and doorway flux monitoring.
4. **Sprint 3: Empirical Benchmarking & Figure Generation**
   - Run `python telemetry/generate_paper_figures.py` to produce benchmark dataset ($N = 50$) and 300 DPI publication figures.
   - Validate that doorway discharge rate adheres to NBC standard thresholds.
5. **Sprint 4: Manuscript Assembly & Final Audit**
   - Assemble experimental findings into `docs/RESEARCH_PAPER_MANUSCRIPT_BLUEPRINT.md`.
   - Execute the automated compliance audit script to ensure zero defects.
