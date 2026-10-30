# Product Submission Status and Economics Validation

## Status

| Deliverable | Current evidence | Status |
| --- | --- | --- |
| Unity Quest/PCVR build and `group-02-v1.0` release | No Unity `Packages/` or `ProjectSettings/` folder, scene, build output, or device validation is present in the group folder. | Pending Unity project integration and hardware validation |
| Participant study and psychometrics | Starter benchmark data are synthetic; the trial log's provenance is undocumented. | Pending approved sessions with at least 10 participants and a provenance review |
| Operational cost parity | The target value `kappa = 0.042` appears as a project assumption; the repository does not include source-backed VR and physical OpEx inputs. | Not validated |
| 350-round substitution | This is a target assumption in the project brief; no session ammunition inventory or policy baseline is supplied. | Not validated |
| Capital payback | The 10.9-month figure lacks documented CapEx and comparable operating-cost inputs. | Not validated |
| Technical dossier PDF and LinkedIn video | The DOCX template exists; the group roster reports the video URL and status as pending. | Member attestations, study results, actual URL, and build details remain outstanding |

## Economics checks

The arithmetic `1 - 0.042 = 0.958` means a 95.8% lower VR operating cost **if** the numerator and denominator are comparable OpEx values with the same scope and period. It does not validate the 0.042 input.

Under the requested payback expression `tau_months = K_capex / (1 - kappa) * 12`, `K_capex` must be a dimensionless fraction expressed on an annual physical-OpEx basis for the units to produce months. A 10.9-month result at `kappa = 0.042` would imply `K_capex = 10.9 * (1 - 0.042) / 12 = 0.87025` of annual physical OpEx. No source in the repository substantiates that input. The analysis script instead accepts explicit CapEx and annual savings in matching currency units and reports payback as `CapEx / annual_savings * 12`.

For comparable costs, provide evidence for headset/system purchase and replacement, software and support, trainer/operator hours, facility time, electricity, maintenance, physical ammunition, safety staff, and number of trainee sessions over a stated period. State whether CapEx is included in OpEx; do not double-count amortization. Report sensitivity to utilization and price assumptions.

## Participant and submission dependencies

The study requires institutional approval and the informed-consent/data-governance process described in `EMPIRICAL_DATA_COLLECTION_PROTOCOL.md`. Store pseudonymous codes only in telemetry. Members must provide their personal contribution summaries, verified repository statistics, and sign-off themselves. The video URL, duration, participation, and published status can be updated only after the actual video exists.

The project materials specify two different video lengths: the strict sprint brief requests 3–4 minutes; the README/dossier asks for 60–90 seconds. Agree with the course submission requirement before recording. Suggested 3–4 minute internal run-of-show if that brief is authoritative: 0:00–0:40 project question and ethics boundary (Medha); 0:40–1:30 XR scene and sensory implementation (Ranish); 1:30–2:20 workload assessments and safe usability flow (Hriday); 2:20–3:10 telemetry, SDT, and data provenance (Vedika); 3:10–3:40 live demo, limitations, and next steps (all). This is a plan only; it does not claim recording or publication.
