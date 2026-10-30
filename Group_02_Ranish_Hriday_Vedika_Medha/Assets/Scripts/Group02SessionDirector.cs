using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace IVRAR.Group02.SensoryStress
{
    public enum ArcadeGameState { Ready, Playing, Finished }

    /// <summary>
    /// Master session and arcade gameplay director for Group 02 VR Tactical Simulation.
    /// Drives target prioritization gameplay, scoring, combo multipliers, round timer,
    /// and HUD updates for Meta XR Simulator and VR headsets.
    /// </summary>
    public class Group02SessionDirector : MonoBehaviour
    {
        [Header("System References")]
        public SensoryStressTargetManager targetManager;
        public ReactionLatencyTelemetryLogger telemetryLogger;
        public PostTrialAssessmentController assessmentController;

        [Header("UI Controls")]
        public Text statusText;
        public Button startSessionButton;
        public Button stopSessionButton;
        public Button openAssessmentButton;

        [Header("Arcade Configuration")]
        public float roundDurationSeconds = 60f;
        public string participantCode = "ARCADE-PILOT-01";
        public string sessionId = "ARCADE-SESSION-01";

        [Header("Live Arcade State")]
        public ArcadeGameState currentState = ArcadeGameState.Ready;
        public int score = 0;
        public int comboMultiplier = 1;
        public int maxCombo = 1;
        public int hostilesHit = 0;
        public int hostilesMissed = 0;
        public int civiliansPenalized = 0;
        public int civiliansDisciplined = 0;
        public float timeRemaining = 60f;

        private bool sessionRunning = false;
        private string lastTrialId = "trial-0001";
        private bool lastHostile = true;
        private bool lastWasLeft = false;
        private string lastFeedback = "Press SPACE or Pull TRIGGER to Start";
        private int currentTrialCount = 0;
        private Coroutine spawnNextCoroutine;

        public bool IsSessionRunning => sessionRunning;

        private void Start()
        {
            if (targetManager == null) targetManager = FindObjectOfType<SensoryStressTargetManager>();
            if (telemetryLogger == null) telemetryLogger = FindObjectOfType<ReactionLatencyTelemetryLogger>();
            if (assessmentController == null) assessmentController = FindObjectOfType<PostTrialAssessmentController>();

            if (startSessionButton != null) startSessionButton.onClick.AddListener(StartSession);
            if (stopSessionButton != null) stopSessionButton.onClick.AddListener(StopSession);
            if (openAssessmentButton != null) openAssessmentButton.onClick.AddListener(OpenAssessment);

            if (assessmentController != null)
            {
                assessmentController.participantCode = participantCode;
                assessmentController.sessionId = sessionId;
            }
            if (telemetryLogger != null)
            {
                telemetryLogger.participantCode = participantCode;
                telemetryLogger.sessionId = sessionId;
            }

            var assessPanel = GameObject.Find("AssessmentPanel");
            if (assessPanel != null) assessPanel.SetActive(false);

            SetState(ArcadeGameState.Ready);
        }

        private void Update()
        {
            if (currentState == ArcadeGameState.Ready || currentState == ArcadeGameState.Finished)
            {
                if (CheckStartTrigger())
                {
                    StartSession();
                    return;
                }
            }
            else if (currentState == ArcadeGameState.Playing)
            {
                timeRemaining -= Time.deltaTime;
                if (timeRemaining <= 0f)
                {
                    timeRemaining = 0f;
                    FinishGame();
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
                {
                    StopSession();
                    return;
                }

                UpdateHUD();
            }

            if (Input.GetKeyDown(KeyCode.T))
            {
                OpenAssessment();
            }
            if (Input.GetKeyDown(KeyCode.Alpha1) && targetManager != null)
            {
                targetManager.SpawnRandomizedTarget(1);
                lastTrialId = "trial-0001";
                lastHostile = true;
                lastWasLeft = false;
            }
            if (Input.GetKeyDown(KeyCode.E) && telemetryLogger != null)
            {
                telemetryLogger.ExportTelemetryToDisk();
                UpdateStatus("Telemetry exported to disk.");
            }
        }

        public void HandleTargetDecision(TargetTrialMetadata target, bool engaged, float latencyMs)
        {
            if (currentState != ArcadeGameState.Playing) return;

            lastTrialId = target.TrialId;
            lastHostile = target.IsHostile;

            if (target.IsHostile)
            {
                if (engaged)
                {
                    int points = 100 * comboMultiplier;
                    score += points;
                    comboMultiplier++;
                    if (comboMultiplier > maxCombo) maxCombo = comboMultiplier;
                    hostilesHit++;
                    lastFeedback = $"<color=#00FF66>HOSTILE HIT! +{points} (COMBO x{comboMultiplier})</color>";
                }
                else
                {
                    comboMultiplier = 1;
                    hostilesMissed++;
                    lastFeedback = "<color=#FF9900>HOSTILE ESCAPED! COMBO RESET</color>";
                }
            }
            else
            {
                if (engaged)
                {
                    score = Mathf.Max(0, score - 150);
                    comboMultiplier = 1;
                    civiliansPenalized++;
                    lastFeedback = "<color=#FF2222>CIVILIAN PENALTY! -150 PTS (COMBO RESET)</color>";
                }
                else
                {
                    score += 25;
                    civiliansDisciplined++;
                    lastFeedback = "<color=#00CCFF>TRIGGER DISCIPLINE BONUS +25 PTS</color>";
                }
            }

            UpdateHUD();

        }

        private IEnumerator SpawnNextTargetDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (currentState == ArcadeGameState.Playing && targetManager != null)
            {
                currentTrialCount++;
                targetManager.SpawnRandomizedTarget(currentTrialCount);
            }
        }

        public bool CheckStartTrigger()
        {
            // 1. Keyboard / Mouse inputs
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.S) || Input.GetMouseButtonDown(0))
                return true;

            // 2. New Input System
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame ||
                 UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame ||
                 UnityEngine.InputSystem.Keyboard.current.sKey.wasPressedThisFrame))
                return true;

            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                return true;

            if (UnityEngine.InputSystem.Gamepad.current != null &&
                (UnityEngine.InputSystem.Gamepad.current.buttonSouth.wasPressedThisFrame ||
                 UnityEngine.InputSystem.Gamepad.current.rightTrigger.wasPressedThisFrame))
                return true;
#endif

            // 3. XR Controller inputs (Meta XR Simulator / Quest HMD)
            var rightController = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
            if (rightController.isValid)
            {
                if (rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool trig) && trig) return true;
                if (rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool prim) && prim) return true;
                if (rightController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float val) && val > 0.5f) return true;
            }

            var leftController = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
            if (leftController.isValid)
            {
                if (leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out bool trigL) && trigL) return true;
                if (leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool primL) && primL) return true;
                if (leftController.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float valL) && valL > 0.5f) return true;
            }

            return false;
        }

        public void StartSession()
        {
            score = 0;
            comboMultiplier = 1;
            maxCombo = 1;
            hostilesHit = 0;
            hostilesMissed = 0;
            civiliansPenalized = 0;
            civiliansDisciplined = 0;
            timeRemaining = roundDurationSeconds;
            currentTrialCount = 0;
            sessionRunning = true;

            SetState(ArcadeGameState.Playing);

            // Start multi-target arcade wave with up to 10 concurrent targets
            if (targetManager != null)
            {
                targetManager.StartMultiTargetSession(10);
            }

            lastFeedback = "TARGET BREACH ACTIVE - ENGAGE RED HOSTILES ONLY!";
            UpdateHUD();
        }

        public void FinishGame()
        {
            sessionRunning = false;
            SetState(ArcadeGameState.Finished);

            if (targetManager != null)
                targetManager.TerminateTrialSequence();

            var strayTargets = FindObjectsOfType<TargetTrialMetadata>();
            foreach (var t in strayTargets) Destroy(t.gameObject);

            if (telemetryLogger != null) telemetryLogger.ExportTelemetryToDisk();

            int totalHostiles = hostilesHit + hostilesMissed;
            float accuracy = totalHostiles > 0 ? (hostilesHit * 100f / totalHostiles) : 100f;

            string summary = $"<color=#FFD700>SESSION COMPLETE!</color>\n" +
                             $"FINAL SCORE: <b>{score}</b>  |  MAX COMBO: <b>x{maxCombo}</b>\n" +
                             $"Hostiles: {hostilesHit}/{totalHostiles} ({accuracy:F0}%) | Civilian Penalties: {civiliansPenalized}\n" +
                             $"<i>Press SPACE, ENTER, or TRIGGER to Play Again</i>";

            UpdateStatus(summary);
        }

        public void StopSession()
        {
            sessionRunning = false;
            SetState(ArcadeGameState.Finished);
            if (targetManager != null)
                targetManager.TerminateTrialSequence();

            var strayTargets = FindObjectsOfType<TargetTrialMetadata>();
            foreach (var t in strayTargets) Destroy(t.gameObject);
            if (telemetryLogger != null) telemetryLogger.ExportTelemetryToDisk();
            UpdateStatus($"Session Stopped. Final Score: {score}. Press Space to Restart.");
        }

        public void SetState(ArcadeGameState newState)
        {
            currentState = newState;
            switch (currentState)
            {
                case ArcadeGameState.Ready:
                    UpdateStatus("<color=#00FFFF>TACTICAL ARCADE BREACH</color>\n" +
                                 "RED = HOSTILE (+100 PTS)  |  BLUE = CIVILIAN (-150 PTS)\n" +
                                 "<b>Press SPACE, ENTER, or Pull TRIGGER to Start</b>");
                    break;
                case ArcadeGameState.Playing:
                    UpdateHUD();
                    break;
                case ArcadeGameState.Finished:
                    break;
            }
        }

        public void UpdateHUD()
        {
            if (currentState != ArcadeGameState.Playing) return;

            string hud = $"<b>SCORE:</b> <color=#FFD700>{score}</color>  |  " +
                         $"<b>COMBO:</b> <color=#00FF66>x{comboMultiplier}</color>  |  " +
                         $"<b>TIME:</b> <color=#00FFFF>{Mathf.CeilToInt(timeRemaining)}s</color>\n" +
                         $"{lastFeedback}";

            UpdateStatus(hud);
        }

        public void OpenAssessment()
        {
            if (assessmentController != null)
            {
                assessmentController.BeginAssessment(lastTrialId, lastHostile, lastWasLeft);
                UpdateStatus("Post-Trial Assessment Panel OPEN.");
            }
        }

        public void UpdateStatus(string message)
        {
            if (statusText != null) statusText.text = message;
            Debug.Log($"[ArcadeDirector] {message}");
        }
    }
}
