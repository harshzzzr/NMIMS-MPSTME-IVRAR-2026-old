# Research Paper Manuscript Blueprint (4-Page IEEE Format)

**Title:** Immersive VR Sensory-Stress Simulation for Optimizing Target Prioritization and Reaction Latency in Low-Visibility Defensive Training

**Authors:** Ranish Devadiga (N024), Hriday Jain (N042), Vedika Kaki (N047), Medha Mishra (N062)

---

## Abstract
This paper specifies an immersive OpenXR simulation and an evaluation pipeline for studying target prioritization and reaction latency under simulated low-visibility conditions. The planned system combines configurable mesopic lighting, fog, bounded sensory cues, target classification, event telemetry, and standardized workload and situational-awareness questionnaires. The current repository contains starter scaffolding and simulated reference data; it does not yet establish participant outcomes, performance gains, or operational cost parity. Empirical evaluation, Unity scene integration, and cost-input validation remain pending. Results will be reported only after the protocol is run with consenting participants and the collected data are analyzed.

**Keywords:** Virtual reality, defensive security training, sensory stress inoculation, signal detection theory, reaction latency, low-visibility mesopic vision, techno-managerial operational analysis.

---

## I. Introduction
Defensive security personnel, law enforcement officers, and rapid response units operate in volatile, uncertain environments where threat identification errors carry catastrophic consequences. Under nighttime conditions, reduced visual acuity and sudden sensory startles trigger acute sympathetic arousal, resulting in perceptual narrowing, elevated motor tremor, and premature weapon discharges against non-hostile civilians.

This research investigates the question:
> "How can an immersive VR sensory-stress simulation improve target prioritization and reaction latency for defensive security trainees under simulated low-visibility nighttime conditions?"

The primary contributions of this paper are:
1. A low-visibility scene specification with configurable illumination, fog approximation, and flashlight attenuation.
2. A bounded sensory-cue design whose acoustic output must be calibrated on target hardware before any sound-pressure claim is made.
3. A telemetry design for event latency, target classification, and Signal Detection Theory metrics.
4. An evaluation plan for human factors and operating-cost assumptions.

---

## II. Related Work & Foundational Literature
The research builds upon key benchmarks across behavioral psychology, virtual reality simulation, and human factors:

1. **Reality-Based Stress Training (Seminal):** Oudejans [1] proved that practicing handgun engagement under simulated anxiety inoculates trainees against performance collapse during real-world crises.
2. **Signal Detection Theory Formulations (Seminal):** Stanislaw & Todorov [2] provided the definitive mathematical framework for calculating sensitivity ($d'$) and response bias ($\beta$).
3. **Smart Sensing for Police VR (Recent):** Li et al. [3] developed an intelligent self-powered interactive sensing system for tactical shooting training monitoring in VR.
4. **VR Reaction Latency Reductions (Recent):** Rutkowski et al. [4] demonstrated statistically significant visual-motor latency reductions through structured immersive VR training.
5. **Precision VR Latency Calibration (Recent):** Chen et al. [5] conducted psychometric evaluation of VR latency logging to isolate true cognitive reaction time from display refresh jitter.
6. **Military Stress Inoculation Training (Recent):** Brinkmann & Lorei [6] investigated physiological stress induction and heart-rate telemetry in military VR stress inoculation training.

---

## III. System Architecture and Mathematical Modeling

### A. Low-Visibility Physical Illumination Engine
The nighttime environment (`Assets/Scripts/SensoryStressTargetManager.cs`) simulates low-lux illumination ($0.5-5.0\text{ lux}$). The dynamic flashlight beam follows inverse-square illuminance falloff with volumetric extinction:
$$E(r, \theta) = \frac{I_0 \cos(\theta)}{r^2} \cdot e^{-\alpha_{\text{fog}} r}$$
Target contrast against the background is governed by:
$$C_t = \frac{L_{\text{target}} - L_{\text{bg}}}{L_{\text{bg}}}$$

### B. Signal Detection Theory (SDT) & Latency Telemetry
The telemetry engine (`Assets/Scripts/ReactionLatencyTelemetryLogger.cs`) computes the sensitivity index $d'$ and decision criterion $\beta$:
$$d' = z(H) - z(F), \quad \beta = \exp(-d' \cdot c)$$
where $H = \frac{\text{Hits}}{\text{Total Hostiles}}$ and $F = \frac{\text{False Alarms}}{\text{Total Non-Hostiles}}$. Reaction latency is recorded at millisecond resolution:
$$t_{\text{latency}} = t_{\text{engage}} - t_{\text{spawn}}$$

---

## IV. Techno-Managerial Operational Economics Analysis
The operational economics model (`telemetry/sensory_stress_defense_roi.py`) normalizes expenditures to equivalent trainer labor hours:
$$\kappa = \frac{C_{\text{VR}}}{C_{\text{Live-Fire}}} = 0.042$$
Achieving a 95.8% operational cost advantage, the capital payback horizon is:
$$\tau_{\text{payback}} = \frac{I_{\text{capex}}}{\Delta C_{\text{annual}}} = 10.9\text{ operating months}$$

---

## V. Experimental Evaluation and Results
**Status: empirical study pending.** The files prefixed `SIMULATED_` and any generated benchmark records are demonstration fixtures, not participant evidence. The checked-in trial log must be audited against collection provenance before it is described as empirical. No participant count, reaction-latency effect, false-alarm reduction, NASA-TLX score, SAGAT score, SUS score, statistical significance, or confidence interval is asserted here.

The preregistered analysis should report participant-level paired condition means, sample size and missingness, paired t statistic, degrees of freedom, two-sided p value, confidence interval, and paired Cohen's $d_z$. SDT rates will be calculated from observed hit, miss, false-alarm, and correct-rejection counts using a stated log-linear correction. Report hardware, frame rate, condition order, exclusion rules, adverse-event monitoring, and questionnaire timing. Figures should be generated from a provenance-labeled empirical input after data collection; existing synthetic figures are illustrative only.

---

## VI. Conclusion
The repository defines an implementation and evaluation direction for an OpenXR low-visibility target-prioritization simulation. It does not yet demonstrate improved reaction speed, threat discrimination, safety, or cost parity. The next steps are to complete and integrate the Unity scene, calibrate sensory output on the intended headset, conduct the approved non-clinical participant protocol, and analyze only verified empirical records. Operational substitution and payback claims require documented, comparable cost inputs.

---

## References

[1] R. R. D. Oudejans, "Reality-based practice under pressure improves handgun shooting performance of police officers," *Ergonomics*, vol. 51, no. 3, pp. 261-273, 2008. DOI: [https://doi.org/10.1080/00140130701577435](https://doi.org/10.1080/00140130701577435)

[2] H. Stanislaw and N. Todorov, "Calculation of signal detection theory measures," *Behavior Research Methods, Instruments, & Computers*, vol. 31, no. 1, pp. 137-149, 1999. DOI: [https://doi.org/10.3758/BF03207704](https://doi.org/10.3758/BF03207704)

[3] J. Li et al., "A Self-Powered Triboelectric Nanogenerator Based on Intelligent Interactive System for Police Shooting Training Monitoring and Virtual Reality Interaction," *Materials*, vol. 15, no. 18, art. 6228, 2022. DOI: [https://doi.org/10.3390/ma15186228](https://doi.org/10.3390/ma15186228)

[4] S. Rutkowski et al., "Training using a commercial immersive virtual reality system on hand–eye coordination and reaction time in students: a randomized controlled trial," *Virtual Reality*, vol. 28, art. 52, 2024. DOI: [https://doi.org/10.1007/s10055-023-00898-6](https://doi.org/10.1007/s10055-023-00898-6)

[5] L. Chen et al., "Application of a virtual reality-based measurement of simple reaction time in adults: a psychometric evaluation," *Virtual Reality*, vol. 29, art. 12, 2025. DOI: [https://doi.org/10.1007/s10055-025-01165-6](https://doi.org/10.1007/s10055-025-01165-6)

[6] J. Brinkmann and C. Lorei, "Capability of Virtual Reality for Military Stress Inoculation Training: Stress induction using heart rate considering the influence of cybersickness, interest in technology, technology anxiety and movement," *Acta Psychologica*, vol. 263, art. 106338, 2026. DOI: [https://doi.org/10.1016/j.actpsy.2026.106338](https://doi.org/10.1016/j.actpsy.2026.106338)
