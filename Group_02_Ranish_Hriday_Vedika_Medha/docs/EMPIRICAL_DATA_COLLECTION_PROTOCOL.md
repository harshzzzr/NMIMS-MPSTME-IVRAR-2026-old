# Empirical Data Collection Protocol

## Current evidence status

No participant results are established by the starter CSVs. Files prefixed `SIMULATED_` are fixtures. The checked-in `reaction_latency_trial_log.csv` has no documented collection provenance and must not be presented as empirical until the team verifies its source. The analysis tool reads a supplied file and never generates or overwrites participant records.

## Participant session procedure

1. Obtain required institutional/faculty approval before recruitment. Give each participant an information sheet describing the simulated content, sensory cues, data collected, retention, and contact for questions. Obtain informed consent before starting.
2. Participation must be voluntary. Use a pseudonymous participant code; keep any identity-to-code key separately with restricted access. Participants may stop or withdraw without penalty. Do not log names, email addresses, or student IDs in telemetry.
3. Screen for headset contraindications using the institution-approved protocol. Explain that the scenario contains simulated threat imagery, intermittent light, and sound. Provide a pause/stop control. Stop immediately on discomfort, dizziness, nausea, distress, or participant request; record only a coded adverse-event flag if approved.
4. Record headset model, Unity build, refresh rate, condition order, session date (or coded session time), operator code, and calibration status. Do not claim 90 Hz capture unless the device/build confirms it.
5. Collect the same pre/post procedure in each condition. After each trial, record the target decision and latency. After each condition, administer NASA-TLX and SAGAT. Administer SUS after system use and Kennedy SSQ at the protocol-specified time. Store questionnaire responses under the participant code and session ID.
6. Export CSV, verify row counts and schema, back up the raw file read-only, then analyze a copy. Keep synthetic and empirical files separate and mark every analysis input's provenance.

## Telemetry schema

The Unity event logger writes one row per resolved target trial. Fields are: `TimestampUtc`, pseudonymous `ParticipantID`, `SessionID`, `Condition`, `DataStatus`, `TrialID`, `TargetType`, `TraineeAction`, `LatencyMs`, `Hit`, `Miss`, `FalseAlarm`, `CorrectRejection`, `DistanceMeters`, `PresentationAngleDeg`, `HeadYawJitterVarianceDeg2`, `DPrime`, and `BetaCriterion`. Leave `DataStatus` as `unverified` until an approved genuine session is actually collected; do not label synthetic rows empirical. The head-yaw variance is an exploratory motion proxy and is not a validated measure of cognitive workload or tremor. A separate assessment export is keyed by pseudonymous `ParticipantID`, `SessionID`, and `TrialID` and contains all six NASA-TLX subscales plus SAGAT-style recall counts; do not put direct identifiers in either file.

For paired latency analysis, the analysis CSV must include `participant_id`, `condition`, and `reaction_latency_ms`, with one or more trial rows per participant and condition. The pipeline averages at participant-condition level before a paired test. For SDT, rows must include `target_type` (`hostile`/`civilian`) and `trainee_action` (`engaged`/`ignored`). Use the log-linear correction `(count + 0.5)/(class total + 1)` before z transforms.

## Questionnaire scoring

- NASA-TLX: collect all six 0–100 subscales (mental, physical, temporal, performance, effort, frustration); state whether raw or weighted scoring is used. The provided raw-score calculator uses the unweighted mean.
- SAGAT: define scenario-specific probes and objectively score each response against the scenario state captured at the freeze point. Report probe wording, denominator, and scoring rules.
- SUS: collect all ten standard items on the 1–5 response scale, then calculate the standard 0–100 score.
- Kennedy SSQ: collect all 16 standard symptom items on the 0–3 scale at the specified assessment time; use the provided subscale weights and preserve raw responses.

## Analysis and reporting

Use participant-level paired condition means for reaction latency when the same people complete both conditions. Report the paired t statistic, degrees of freedom, p value, confidence interval, and paired Cohen's `d_z`; report exclusions and missing data. Compute SDT from actual decision counts, explicitly identifying hits, misses, false alarms, and correct rejections. Report participant N and descriptive statistics for each questionnaire. Do not make causal claims from an uncontrolled convenience sample. `kappa`, ammunition substitution, facility savings, and payback require source-backed cost inputs independent of the participant CSV.

## Required reporting gates

- At least 10 genuine completed participant sessions before reporting the planned minimum-sample study.
- No release of direct identifiers or participant-level data without the approved consent and data governance process.
- Keep results labeled pending until the raw data, provenance, scoring, and analysis have been reviewed by the team.
