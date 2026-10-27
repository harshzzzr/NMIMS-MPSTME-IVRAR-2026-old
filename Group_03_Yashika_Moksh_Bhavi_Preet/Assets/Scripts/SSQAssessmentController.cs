using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace C067.Coordination
{
    public enum SSQAssessmentTiming
    {
        Baseline = 0,
        PostTrial = 1,
        PostSession = 2,
        FollowUp = 3
    }

    /// <summary>
    /// Represents an immutable completed Simulator Sickness Questionnaire record
    /// per the validated Kennedy, Lane, Berbaum, and Lilienthal (1993) standard.
    /// </summary>
    [System.Serializable]
    public class SSQAssessmentRecord
    {
        public string participantId = "P001";
        public string trialId = "";
        public string timestampUtc = "";
        public SSQAssessmentTiming timing = SSQAssessmentTiming.PostSession;
        public bool isComplete = false;
        public bool wasWithdrawn = false;
        public string adverseEventNotes = "";

        // 16 Standard Kennedy (1993) Symptoms (values 0=None, 1=Slight, 2=Moderate, 3=Severe)
        public int[] responses = new int[16];

        // Kennedy (1993) Weighted Subscales & Total Score
        public float nauseaScore = -1f;
        public float oculomotorScore = -1f;
        public float disorientationScore = -1f;
        public float totalScore = -1f;

        public const string InstrumentSource = "Kennedy, R. S., Lane, N. E., Berbaum, K. S., & Lilienthal, M. G. (1993). Simulator Sickness Questionnaire: An enhanced method for quantifying simulator sickness. The International Journal of Aviation Psychology, 3(3), 203-220.";
        public const string MedicalDisclaimer = "The Simulator Sickness Questionnaire is an experimental research instrument and does not provide a medical diagnosis.";
    }

    /// <summary>
    /// Implements the authoritative 16-item Simulator Sickness Questionnaire (Kennedy et al., 1993)
    /// in VR (World Space XR Canvas) and desktop fallback mode.
    /// Supports pre-exposure baseline, post-trial, and post-session evaluations, with built-in
    /// participant withdrawal/stop safeguards and exact subscale formula calculations.
    /// </summary>
    public class SSQAssessmentController : MonoBehaviour
    {
        public static SSQAssessmentController Instance { get; private set; }

        public static readonly string[] SymptomNames = new string[]
        {
            "General discomfort",
            "Fatigue",
            "Headache",
            "Eye strain",
            "Difficulty focusing",
            "Increased salivation",
            "Sweating",
            "Nausea",
            "Difficulty concentrating",
            "Fullness of head",
            "Blurred vision",
            "Dizzy (eyes open)",
            "Dizzy (eyes closed)",
            "Vertigo",
            "Stomach awareness",
            "Burping"
        };

        // 0-indexed Kennedy (1993) item mapping:
        // Nausea: 0 (General discomfort), 5 (Increased salivation), 6 (Sweating), 7 (Nausea), 8 (Difficulty concentrating), 14 (Stomach awareness), 15 (Burping)
        private static readonly int[] NauseaIndices = new int[] { 0, 5, 6, 7, 8, 14, 15 };

        // Oculomotor: 0 (General discomfort), 1 (Fatigue), 2 (Headache), 3 (Eye strain), 4 (Difficulty focusing), 8 (Difficulty concentrating), 10 (Blurred vision)
        private static readonly int[] OculomotorIndices = new int[] { 0, 1, 2, 3, 4, 8, 10 };

        // Disorientation: 4 (Difficulty focusing), 7 (Nausea), 9 (Fullness of head), 10 (Blurred vision), 11 (Dizzy eyes open), 12 (Dizzy eyes closed), 13 (Vertigo)
        private static readonly int[] DisorientationIndices = new int[] { 4, 7, 9, 10, 11, 12, 13 };

        // Kennedy (1993) Weighting Multipliers:
        public const float NauseaWeight = 9.54f;
        public const float OculomotorWeight = 7.58f;
        public const float DisorientationWeight = 13.92f;
        public const float TotalScoreMultiplier = 3.74f;

        [Header("Assessment State")]
        public string currentParticipantId = "P001";
        public string currentTrialId = "";
        public SSQAssessmentTiming currentTiming = SSQAssessmentTiming.PostSession;
        public bool isAssessmentActive = false;
        public int currentItemIndex = 0;

        private int[] _currentResponses = new int[16];
        private Action<SSQAssessmentRecord> _onCompletedCallback;

        public event Action<SSQAssessmentRecord> OnAssessmentSubmitted;
        public event Action OnParticipantRequestedWithdrawal;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Authoritative static scoring calculation per Kennedy, Lane, Berbaum, & Lilienthal (1993).
        /// Used by unit test verification and runtime scoring alike.
        /// </summary>
        public static bool CalculateScores(int[] responses, out float nausea, out float oculomotor, out float disorientation, out float totalScore)
        {
            nausea = -1f;
            oculomotor = -1f;
            disorientation = -1f;
            totalScore = -1f;

            if (responses == null || responses.Length != 16) return false;

            // Validate response values (0 to 3)
            int nRaw = 0;
            int oRaw = 0;
            int dRaw = 0;

            for (int i = 0; i < 16; i++)
            {
                if (responses[i] < 0 || responses[i] > 3) return false;
            }

            foreach (int idx in NauseaIndices) nRaw += responses[idx];
            foreach (int idx in OculomotorIndices) oRaw += responses[idx];
            foreach (int idx in DisorientationIndices) dRaw += responses[idx];

            nausea = (float)Math.Round(nRaw * NauseaWeight, 2);
            oculomotor = (float)Math.Round(oRaw * OculomotorWeight, 2);
            disorientation = (float)Math.Round(dRaw * DisorientationWeight, 2);
            totalScore = (float)Math.Round((nRaw + oRaw + dRaw) * TotalScoreMultiplier, 2);

            return true;
        }

        /// <summary>
        /// Begins a standard 16-item SSQ assessment.
        /// </summary>
        public void StartAssessment(string participantId, string trialId, SSQAssessmentTiming timing, Action<SSQAssessmentRecord> onCompleted = null)
        {
            currentParticipantId = participantId;
            currentTrialId = trialId;
            currentTiming = timing;
            _onCompletedCallback = onCompleted;

            Array.Fill(_currentResponses, 0);
            currentItemIndex = 0;
            isAssessmentActive = true;

            Debug.Log($"[SSQ] Started {timing} assessment for {participantId} (Trial: {trialId}).");
        }

        public void RecordResponse(int symptomIndex, int value)
        {
            if (symptomIndex >= 0 && symptomIndex < 16)
            {
                _currentResponses[symptomIndex] = Mathf.Clamp(value, 0, 3);
            }
        }

        public void NextQuestion()
        {
            if (currentItemIndex < 15)
            {
                currentItemIndex++;
            }
            else
            {
                SubmitAssessment();
            }
        }

        public void PreviousQuestion()
        {
            if (currentItemIndex > 0)
            {
                currentItemIndex--;
            }
        }

        /// <summary>
        /// Submits the completed SSQ questionnaire, computes Kennedy (1993) subscales, and notifies listeners.
        /// </summary>
        public void SubmitAssessment()
        {
            if (!isAssessmentActive) return;

            CalculateScores(_currentResponses, out float n, out float o, out float d, out float ts);

            var record = new SSQAssessmentRecord
            {
                participantId = currentParticipantId,
                trialId = currentTrialId,
                timestampUtc = DateTime.UtcNow.ToString("o"),
                timing = currentTiming,
                isComplete = true,
                wasWithdrawn = false,
                responses = (int[])_currentResponses.Clone(),
                nauseaScore = n,
                oculomotorScore = o,
                disorientationScore = d,
                totalScore = ts
            };

            isAssessmentActive = false;
            if (VRScreenHUD.Instance != null) VRScreenHUD.Instance.HideAssessment();
            OnAssessmentSubmitted?.Invoke(record);
            _onCompletedCallback?.Invoke(record);

            Debug.Log($"[SSQ] Completed {currentTiming}: N={n:F2}, O={o:F2}, D={d:F2}, Total={ts:F2}");
        }

        /// <summary>
        /// Participant Stop and Withdrawal control: Immediately terminates assessment safely
        /// if the participant experiences discomfort or requests to exit.
        /// </summary>
        public void RequestWithdrawal(string notes = "Participant requested session stop")
        {
            if (!isAssessmentActive) return;

            var record = new SSQAssessmentRecord
            {
                participantId = currentParticipantId,
                trialId = currentTrialId,
                timestampUtc = DateTime.UtcNow.ToString("o"),
                timing = currentTiming,
                isComplete = false,
                wasWithdrawn = true,
                adverseEventNotes = notes,
                responses = (int[])_currentResponses.Clone(),
                nauseaScore = -1f,
                oculomotorScore = -1f,
                disorientationScore = -1f,
                totalScore = -1f
            };

            isAssessmentActive = false;
            if (VRScreenHUD.Instance != null) VRScreenHUD.Instance.HideAssessment();
            OnParticipantRequestedWithdrawal?.Invoke();
            _onCompletedCallback?.Invoke(record);

            Debug.LogWarning($"[SSQ] Participant {currentParticipantId} withdrew from assessment. Adverse event noted.");
        }

        private void Update()
        {
            if (!isAssessmentActive) return;

            if (VRScreenHUD.Instance != null)
            {
                string header = $"Simulator Sickness Questionnaire ({currentItemIndex + 1} of 16)";
                string question = $"Please rate the severity of:\n\n<size=30><b>{SymptomNames[currentItemIndex]}</b></size>";
                int currentRating = _currentResponses[currentItemIndex];
                string[] optionLabels = new string[] { "None", "Slight", "Moderate", "Severe" };
                string ratingLabel = (currentRating >= 0 && currentRating < 4) ? optionLabels[currentRating] : "None";
                string valText = $"Rating: <b>[{currentRating}] {ratingLabel}</b>";
                string footer = "Press [0: None] [1: Slight] [2: Moderate] [3: Severe] | Next: [Enter / Space / Trigger] | Esc: Stop";

                VRScreenHUD.Instance.ShowAssessmentQuestion(header, question, valText, footer);
            }

            // Keyboard / Controller input
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit0Key.wasPressedThisFrame || Keyboard.current.numpad0Key.wasPressedThisFrame) RecordResponse(currentItemIndex, 0);
                if (Keyboard.current.digit1Key.wasPressedThisFrame || Keyboard.current.numpad1Key.wasPressedThisFrame) RecordResponse(currentItemIndex, 1);
                if (Keyboard.current.digit2Key.wasPressedThisFrame || Keyboard.current.numpad2Key.wasPressedThisFrame) RecordResponse(currentItemIndex, 2);
                if (Keyboard.current.digit3Key.wasPressedThisFrame || Keyboard.current.numpad3Key.wasPressedThisFrame) RecordResponse(currentItemIndex, 3);
                if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame) NextQuestion();
                if (Keyboard.current.escapeKey.wasPressedThisFrame) RequestWithdrawal();
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha0) || Input.GetKeyDown(KeyCode.Keypad0)) RecordResponse(currentItemIndex, 0);
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) RecordResponse(currentItemIndex, 1);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) RecordResponse(currentItemIndex, 2);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) RecordResponse(currentItemIndex, 3);
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)) NextQuestion();
            if (Input.GetKeyDown(KeyCode.Escape)) RequestWithdrawal();
#endif
        }

        // Desktop OnGUI fallback allowing full interactive testing in editor or standalone without an HMD
        private void OnGUI()
        {
            if (!isAssessmentActive) return;

            GUI.skin.box.fontSize = 14;
            GUI.skin.button.fontSize = 13;
            GUI.skin.label.fontSize = 13;

            int boxWidth = 540;
            int boxHeight = 360;
            int x = (Screen.width - boxWidth) / 2;
            int y = (Screen.height - boxHeight) / 2;

            GUI.Box(new Rect(x, y, boxWidth, boxHeight), $"Simulator Sickness Questionnaire ({currentTiming})");

            GUILayout.BeginArea(new Rect(x + 20, y + 35, boxWidth - 40, boxHeight - 50));

            GUILayout.Label($"Item {currentItemIndex + 1} of 16", EditorStylesBoldOrFallback());
            GUILayout.Space(5);
            GUILayout.Label($"<b>{SymptomNames[currentItemIndex]}</b>", RichTextLabelStyle());
            GUILayout.Label("Rate the severity of this symptom as you experience it right now:");
            GUILayout.Space(12);

            string[] options = new string[] { "None (0)", "Slight (1)", "Moderate (2)", "Severe (3)" };
            int currentVal = _currentResponses[currentItemIndex];

            for (int v = 0; v < 4; v++)
            {
                bool isSelected = (currentVal == v);
                GUI.color = isSelected ? Color.cyan : Color.white;
                if (GUILayout.Button(options[v], GUILayout.Height(32)))
                {
                    RecordResponse(currentItemIndex, v);
                }
            }
            GUI.color = Color.white;

            GUILayout.Space(15);
            GUILayout.BeginHorizontal();

            GUI.enabled = (currentItemIndex > 0);
            if (GUILayout.Button("◄ Back", GUILayout.Width(100), GUILayout.Height(30)))
            {
                PreviousQuestion();
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            GUI.color = new Color(1f, 0.6f, 0.6f);
            if (GUILayout.Button("Stop / Withdraw", GUILayout.Width(130), GUILayout.Height(30)))
            {
                RequestWithdrawal();
            }
            GUI.color = Color.white;

            string nextText = (currentItemIndex < 15) ? "Next ►" : "Submit ✔";
            if (GUILayout.Button(nextText, GUILayout.Width(110), GUILayout.Height(30)))
            {
                NextQuestion();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("<size=10>Kennedy et al. (1993) validated instrument. Anonymous research evaluation.</size>", RichTextLabelStyle());

            GUILayout.EndArea();
        }

        private static GUIStyle EditorStylesBoldOrFallback()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            return style;
        }

        private static GUIStyle RichTextLabelStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.richText = true;
            return style;
        }
    }
}
