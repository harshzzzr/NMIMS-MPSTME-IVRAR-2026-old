using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace C067.Coordination
{
    public enum TLXScoringMethod
    {
        RawUnweighted = 0,
        PairwiseWeighted = 1
    }

    [System.Serializable]
    public class NASATLXResult
    {
        public string participantId = "P001";
        public string trialId = "";
        public string timestampUtc = "";
        public TLXScoringMethod scoringMethod = TLXScoringMethod.RawUnweighted;
        public string algorithmVersion = "1.0.0-HartStaveland1988";
        public bool isComplete = false;

        // Six NASA-TLX Dimensions (0 - 100 in increments of 5)
        public float mentalDemand = -1f;
        public float physicalDemand = -1f;
        public float temporalDemand = -1f;
        public float performance = -1f;
        public float effort = -1f;
        public float frustration = -1f;

        // Computed Composite Scores
        public float rawTlxScore = -1f;
        public float weightedTlxScore = -1f;

        // Pairwise comparison weights (if pairwise weighted scoring is active)
        public int[] dimensionWeights = new int[6];
    }

    /// <summary>
    /// Implements the standardized NASA Task Load Index (Hart & Staveland, 1988) assessment.
    /// Provides 6 validated subjective workload dimensions evaluated on a 0-100 scale (in steps of 5).
    /// Supports VR World-Space interaction and Desktop OnGUI fallback.
    /// </summary>
    public class NASA_TLXAssessmentController : MonoBehaviour
    {
        public static NASA_TLXAssessmentController Instance { get; private set; }

        public static readonly string[] DimensionNames = new string[]
        {
            "Mental Demand",
            "Physical Demand",
            "Temporal Demand",
            "Performance",
            "Effort",
            "Frustration"
        };

        public static readonly string[] DimensionDescriptions = new string[]
        {
            "How much mental and perceptual activity was required (e.g. thinking, deciding, searching, assessing the fire hazard)?",
            "How much physical activity was required (e.g. walking, dodging, descending multiple stair flights)?",
            "How much time pressure did you feel due to the pace of the evacuation and smoke propagation?",
            "How successful do you think you were in accomplishing your evacuation goals (0 = Good/Success, 100 = Poor/Failure)?",
            "How hard did you have to work (mentally and physically) to accomplish your level of performance?",
            "How insecure, discouraged, irritated, stressed, and annoyed did you feel during the evacuation?"
        };

        public static readonly string[] LowEndpoints = new string[]
        {
            "Low", "Low", "Low", "Good / Success", "Low", "Low"
        };

        public static readonly string[] HighEndpoints = new string[]
        {
            "High", "High", "High", "Poor / Failure", "High", "High"
        };

        [Header("State")]
        public string currentParticipantId = "P001";
        public string currentTrialId = "";
        public bool isAssessmentActive = false;
        public int currentDimensionIndex = 0;
        public TLXScoringMethod scoringMethod = TLXScoringMethod.RawUnweighted;

        private float[] _currentRatings = new float[] { 50f, 50f, 50f, 50f, 50f, 50f };
        private bool[] _dimensionAnswered = new bool[6];
        private Action<NASATLXResult> _onCompletedCallback;

        public event Action<NASATLXResult> OnAssessmentSubmitted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public static float CalculateRawScore(float mental, float physical, float temporal, float performance, float effort, float frustration)
        {
            return (float)Math.Round((mental + physical + temporal + performance + effort + frustration) / 6.0f, 2);
        }

        public void StartAssessment(string participantId, string trialId, Action<NASATLXResult> onCompleted = null)
        {
            currentParticipantId = participantId;
            currentTrialId = trialId;
            _onCompletedCallback = onCompleted;

            for (int i = 0; i < 6; i++)
            {
                _currentRatings[i] = 50.0f;
                _dimensionAnswered[i] = false;
            }

            currentDimensionIndex = 0;
            isAssessmentActive = true;

            Debug.Log($"[NASA-TLX] Started workload assessment for {participantId} (Trial: {trialId}).");
        }

        public void SetRating(int dimensionIndex, float value)
        {
            if (dimensionIndex >= 0 && dimensionIndex < 6)
            {
                // Snap to nearest 5
                float snapped = Mathf.Round(Mathf.Clamp(value, 0f, 100f) / 5f) * 5f;
                _currentRatings[dimensionIndex] = snapped;
                _dimensionAnswered[dimensionIndex] = true;
            }
        }

        public void NextDimension()
        {
            _dimensionAnswered[currentDimensionIndex] = true;
            if (currentDimensionIndex < 5)
            {
                currentDimensionIndex++;
            }
            else
            {
                SubmitAssessment();
            }
        }

        public void PreviousDimension()
        {
            if (currentDimensionIndex > 0)
            {
                currentDimensionIndex--;
            }
        }

        public void SubmitAssessment()
        {
            if (!isAssessmentActive) return;

            // Mark all reviewed as answered
            _dimensionAnswered[currentDimensionIndex] = true;

            float rawScore = CalculateRawScore(
                _currentRatings[0], _currentRatings[1], _currentRatings[2],
                _currentRatings[3], _currentRatings[4], _currentRatings[5]
            );

            var result = new NASATLXResult
            {
                participantId = currentParticipantId,
                trialId = currentTrialId,
                timestampUtc = DateTime.UtcNow.ToString("o"),
                scoringMethod = scoringMethod,
                algorithmVersion = "1.0.0-HartStaveland1988",
                isComplete = true,
                mentalDemand = _currentRatings[0],
                physicalDemand = _currentRatings[1],
                temporalDemand = _currentRatings[2],
                performance = _currentRatings[3],
                effort = _currentRatings[4],
                frustration = _currentRatings[5],
                rawTlxScore = rawScore,
                weightedTlxScore = (scoringMethod == TLXScoringMethod.PairwiseWeighted) ? rawScore : -1f
            };

            isAssessmentActive = false;
            if (VRScreenHUD.Instance != null) VRScreenHUD.Instance.HideAssessment();
            OnAssessmentSubmitted?.Invoke(result);
            _onCompletedCallback?.Invoke(result);

            Debug.Log($"[NASA-TLX] Submitted Raw TLX = {rawScore:F2} for {currentParticipantId} (Trial {currentTrialId}).");
        }

        private void Update()
        {
            if (!isAssessmentActive) return;

            // Update in-VR HUD modal
            if (VRScreenHUD.Instance != null)
            {
                string header = $"NASA Task Load Index ({currentDimensionIndex + 1} of 6)";
                string question = $"<b>{DimensionNames[currentDimensionIndex]}</b>\n\n<size=18>{DimensionDescriptions[currentDimensionIndex]}</size>";
                float val = _currentRatings[currentDimensionIndex];
                string valText = $"Rating: <b>{val:F0}</b> / 100  ({LowEndpoints[currentDimensionIndex]} ↔ {HighEndpoints[currentDimensionIndex]})";
                string footer = "Adjust: [Left/Right Arrows] or Controller Stick | Confirm: [Enter / Space / Trigger]";

                VRScreenHUD.Instance.ShowAssessmentQuestion(header, question, valText, footer);
            }

            // Keyboard & Controller Navigation
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
                {
                    SetRating(currentDimensionIndex, _currentRatings[currentDimensionIndex] - 5f);
                }
                if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
                {
                    SetRating(currentDimensionIndex, _currentRatings[currentDimensionIndex] + 5f);
                }
                if (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    NextDimension();
                }
                if (Keyboard.current.backspaceKey.wasPressedThisFrame)
                {
                    PreviousDimension();
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                SetRating(currentDimensionIndex, _currentRatings[currentDimensionIndex] - 5f);
            }
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                SetRating(currentDimensionIndex, _currentRatings[currentDimensionIndex] + 5f);
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                NextDimension();
            }
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                PreviousDimension();
            }
#endif
        }

        private void OnGUI()
        {
            if (!isAssessmentActive) return;

            GUI.skin.box.fontSize = 14;
            GUI.skin.button.fontSize = 13;
            GUI.skin.label.fontSize = 13;

            int boxWidth = 580;
            int boxHeight = 380;
            int x = (Screen.width - boxWidth) / 2;
            int y = (Screen.height - boxHeight) / 2;

            GUI.Box(new Rect(x, y, boxWidth, boxHeight), "NASA Task Load Index (NASA-TLX) Workload Assessment");

            GUILayout.BeginArea(new Rect(x + 20, y + 35, boxWidth - 40, boxHeight - 50));

            GUILayout.Label($"Dimension {currentDimensionIndex + 1} of 6: <b>{DimensionNames[currentDimensionIndex]}</b>", RichTextLabelStyle());
            GUILayout.Space(5);
            GUILayout.Label(DimensionDescriptions[currentDimensionIndex]);
            GUILayout.Space(15);

            float currentVal = _currentRatings[currentDimensionIndex];

            GUILayout.BeginHorizontal();
            GUILayout.Label($"<b>{LowEndpoints[currentDimensionIndex]} (0)</b>", RichTextLabelStyle(), GUILayout.Width(130));
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<size=16><b>{currentVal:F0}</b></size>", RichTextLabelStyle());
            GUILayout.FlexibleSpace();
            GUILayout.Label($"<b>{HighEndpoints[currentDimensionIndex]} (100)</b>", RichTextLabelStyle(), GUILayout.Width(130));
            GUILayout.EndHorizontal();

            float newVal = GUILayout.HorizontalSlider(currentVal, 0f, 100f, GUILayout.Height(28));
            if (Mathf.Abs(newVal - currentVal) > 0.1f)
            {
                SetRating(currentDimensionIndex, newVal);
            }

            GUILayout.Space(25);
            GUILayout.BeginHorizontal();

            GUI.enabled = (currentDimensionIndex > 0);
            if (GUILayout.Button("◄ Back", GUILayout.Width(110), GUILayout.Height(32)))
            {
                PreviousDimension();
            }
            GUI.enabled = true;

            GUILayout.FlexibleSpace();

            string nextText = (currentDimensionIndex < 5) ? "Next ►" : "Submit TLX ✔";
            if (GUILayout.Button(nextText, GUILayout.Width(130), GUILayout.Height(32)))
            {
                NextDimension();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            GUILayout.Label("<size=10>Hart & Staveland (1988) Raw TLX. Scale in 5-point intervals.</size>", RichTextLabelStyle());

            GUILayout.EndArea();
        }

        private static GUIStyle RichTextLabelStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.richText = true;
            return style;
        }
    }
}
