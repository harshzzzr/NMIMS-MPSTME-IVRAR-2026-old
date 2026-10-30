using UnityEngine;
using UnityEngine.UI;

namespace IVRAR.Group02.SensoryStress
{
    /// <summary>
    /// Drives an inspector-authored, world-space post-trial panel using standard Unity UI.
    /// Configure the EventSystem with XR UI input so headset controller rays can activate it.
    /// </summary>
    public sealed class PostTrialAssessmentController : MonoBehaviour
    {
        public GameObject panelRoot;
        public Text questionLabel;
        public Text valueLabel;
        public Slider responseSlider;
        public Button nextButton;
        public Text nextButtonLabel;
        public ReactionLatencyTelemetryLogger logger;
        [Tooltip("Pseudonymous code only; never enter a name, email, or student ID.")]
        public string participantCode = "PENDING_CODE";
        public string sessionId = "PENDING_SESSION";

        private readonly float[] tlx = new float[6];
        private readonly string[] questionNames = { "NASA-TLX: Mental demand (0–100)", "NASA-TLX: Physical demand (0–100)", "NASA-TLX: Temporal demand (0–100)", "NASA-TLX: Performance (0–100)", "NASA-TLX: Effort (0–100)", "NASA-TLX: Frustration (0–100)", "Scenario recall: Was the target hostile? 0=Civilian, 1=Hostile", "Scenario recall: Was the target on the left? 0=No, 1=Yes" };
        private int questionIndex;
        private int sagatCorrect;
        private bool targetWasHostile;
        private bool targetWasLeft;
        private string trialId;
        private bool active;

        private void Awake()
        {
            if (logger == null) logger = FindObjectOfType<ReactionLatencyTelemetryLogger>();
            if (panelRoot != null) panelRoot.SetActive(false);
            if (responseSlider != null) responseSlider.onValueChanged.AddListener(OnValueChanged);
            if (nextButton != null) nextButton.onClick.AddListener(Advance);
        }

        /// <summary>Call after a trial, supplying the scenario state needed to score SAGAT objectively.</summary>
        public void BeginAssessment(string completedTrialId, bool lastTargetHostile, bool lastTargetWasLeft)
        {
            if (active || responseSlider == null || questionLabel == null || panelRoot == null) return;
            if (string.IsNullOrWhiteSpace(participantCode) || participantCode == "PENDING_CODE" ||
                string.IsNullOrWhiteSpace(sessionId) || sessionId == "PENDING_SESSION")
            {
                Debug.LogWarning("Assessment withheld: enter approved pseudonymous participant and session codes in the Inspector.");
                return;
            }
            trialId = completedTrialId;
            targetWasHostile = lastTargetHostile;
            targetWasLeft = lastTargetWasLeft;
            questionIndex = 0;
            sagatCorrect = 0;
            active = true;
            panelRoot.SetActive(true);
            ShowQuestion();
        }

        private void ShowQuestion()
        {
            questionLabel.text = questionNames[questionIndex];
            bool recallQuestion = questionIndex >= 6;
            responseSlider.minValue = 0;
            responseSlider.maxValue = recallQuestion ? 1 : 100;
            responseSlider.wholeNumbers = true;
            responseSlider.value = recallQuestion ? 0 : 50;
            if (nextButtonLabel != null) nextButtonLabel.text = questionIndex == questionNames.Length - 1 ? "Save" : "Next";
            OnValueChanged(responseSlider.value);
        }

        private void OnValueChanged(float value)
        {
            if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(value).ToString();
        }

        private void Advance()
        {
            if (!active) return;
            int value = Mathf.RoundToInt(responseSlider.value);
            if (questionIndex < 6) tlx[questionIndex] = value;
            else
            {
                bool responseMatchesTruth = value == 1;
                bool truth = questionIndex == 6 ? targetWasHostile : targetWasLeft;
                if (responseMatchesTruth == truth) sagatCorrect++;
            }

            questionIndex++;
            if (questionIndex < questionNames.Length) { ShowQuestion(); return; }
            if (logger != null) logger.RecordAssessment(participantCode, sessionId, trialId, tlx, sagatCorrect, 2);
            active = false;
            panelRoot.SetActive(false);
        }
    }
}
