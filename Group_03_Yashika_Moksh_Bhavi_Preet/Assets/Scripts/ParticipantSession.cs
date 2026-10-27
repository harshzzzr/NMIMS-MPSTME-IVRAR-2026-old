using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace C067.Coordination
{
    /// <summary>
    /// Encapsulates an entire experimental evaluation session for an individual participant,
    /// isolating trial data, managing baseline/post-session SSQ records, tracking adverse events,
    /// and enabling reproducible session resumption and structured CSV/JSON export.
    /// </summary>
    [System.Serializable]
    public class ParticipantSession
    {
        [Header("Participant & Session Information")]
        public string participantId = "P001";
        public string sessionId = "";
        public string sessionStartUtc = "";
        public string sessionEndUtc = "";
        public int randomSeed = 42;
        public bool isInterrupted = false;
        public string interruptionReason = "";
        public bool adverseEventEncountered = false;

        [Header("Trials and Sequence")]
        public List<TrialConfiguration> configurations = new List<TrialConfiguration>();
        public List<TrialResult> results = new List<TrialResult>();
        public int currentTrialIndex = 0;

        [Header("Session-Level Simulator Sickness Questionnaires")]
        public SSQAssessmentRecord baselineSsq = null;
        public SSQAssessmentRecord postSessionSsq = null;

        public ParticipantSession()
        {
            sessionId = "SESS_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            sessionStartUtc = DateTime.UtcNow.ToString("o");
        }

        public ParticipantSession(string participantId, int seed = 42) : this()
        {
            this.participantId = participantId;
            this.randomSeed = seed;
        }

        public void AddResult(TrialResult result)
        {
            if (result == null) return;

            // Prevent silent overwriting of existing completed trials
            int existingIdx = results.FindIndex(r => r.trialId == result.trialId);
            if (existingIdx >= 0)
            {
                Debug.LogWarning($"[ParticipantSession] Trial {result.trialId} already exists in session. Archiving previous record.");
                results[existingIdx] = result;
            }
            else
            {
                results.Add(result);
            }

            if (result.adverseEvent)
            {
                adverseEventEncountered = true;
            }
        }

        public List<TrialResult> GetValidResults()
        {
            return results.FindAll(r => !r.isPractice && !r.isExcluded && r.state == TrialLifecycleState.Completed);
        }

        public List<TrialResult> GetPracticeResults()
        {
            return results.FindAll(r => r.isPractice);
        }

        public void ExportCsv(string filePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var sb = new StringBuilder();
                sb.AppendLine(TrialResult.GetCsvHeader());

                foreach (var res in results)
                {
                    sb.AppendLine(res.ToCsvRow());
                }

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                try { Debug.Log($"[ParticipantSession] Successfully exported {results.Count} trial results to {filePath}"); } catch { }
            }
            catch (Exception ex)
            {
                try { Debug.LogError($"[ParticipantSession] Failed to export CSV to {filePath}: {ex.Message}"); } catch { }
            }
        }

        public void SaveSessionJson(string filePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                string json = JsonUtility.ToJson(this, true);
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ParticipantSession] Failed to save session JSON: {ex.Message}");
            }
        }

        public static ParticipantSession LoadSessionJson(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            try
            {
                string json = File.ReadAllText(filePath, Encoding.UTF8);
                return JsonUtility.FromJson<ParticipantSession>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ParticipantSession] Failed to load session JSON: {ex.Message}");
                return null;
            }
        }
    }
}
