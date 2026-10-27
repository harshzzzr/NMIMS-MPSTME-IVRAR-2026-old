using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace C067.Coordination
{
    /// <summary>
    /// Thread-safe, non-blocking asynchronous CSV writer using a bounded producer-consumer queue.
    /// Performs disk I/O exclusively on a dedicated background worker thread with zero Unity API calls.
    /// Resilient against I/O exceptions, disk stalls, buffer overflow, and process exit.
    /// Guarantees that two logger instances never write to the same output file simultaneously.
    /// </summary>
    public class TelemetryCsvWriter : IDisposable
    {
        // Global registry of actively locked writing paths across all instances
        private static readonly HashSet<string> s_activeOutputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly object s_registryLock = new object();

        private readonly ConcurrentQueue<BottleneckTelemetrySample> _sampleQueue;
        private readonly int _maxQueueCapacity;
        private readonly float _flushIntervalSeconds;
        private readonly string _filePath;
        private readonly Thread _workerThread;
        private readonly CancellationTokenSource _cts;
        private readonly AutoResetEvent _wakeEvent;

        private int _droppedSamplesCount;
        private long _writtenSamplesCount;
        private volatile bool _isRunning;
        private volatile bool _hasFatalError;
        private string _lastErrorDescription = string.Empty;
        private double _lastErrorLogTime = -999.0;
        private const double ERROR_LOG_THROTTLE_SEC = 5.0;

        public string FilePath => _filePath;
        public bool IsRunning => _isRunning;
        public bool HasFatalError => _hasFatalError;
        public int DroppedSamplesCount => _droppedSamplesCount;
        public long WrittenSamplesCount => _writtenSamplesCount;
        public int PendingQueueCount => _sampleQueue.Count;
        public string LastErrorDescription => _lastErrorDescription;
        public float FlushIntervalSeconds => _flushIntervalSeconds;

        /// <summary>
        /// Initializes the CSV writer, ensures unique file reservation, writes the CSV header,
        /// and launches the dedicated background I/O worker thread.
        /// </summary>
        public TelemetryCsvWriter(string directoryPath, string baseFilename, int maxQueueCapacity = 50000, float flushIntervalSeconds = 0.05f)
        {
            _maxQueueCapacity = Math.Max(1, maxQueueCapacity);
            _flushIntervalSeconds = Math.Clamp(flushIntervalSeconds, 0.005f, 5.0f);
            _sampleQueue = new ConcurrentQueue<BottleneckTelemetrySample>();
            _droppedSamplesCount = 0;
            _writtenSamplesCount = 0;
            _wakeEvent = new AutoResetEvent(false);
            _cts = new CancellationTokenSource();

            try
            {
                if (string.IsNullOrEmpty(directoryPath))
                {
                    directoryPath = Application.persistentDataPath;
                }

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string safeBase = string.IsNullOrEmpty(baseFilename) ? "telemetry" : SanitizeFilename(baseFilename);

                // Reserve a strictly unique file path preventing multi-instance collision
                _filePath = ReserveUniqueFilePath(directoryPath, safeBase);

                // Write CSV header synchronously once before starting background loop
                using (var sw = new StreamWriter(new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read), Encoding.UTF8))
                {
                    sw.WriteLine(BottleneckTelemetrySample.CsvHeader);
                    sw.Flush();
                }

                _isRunning = true;
                _workerThread = new Thread(BackgroundWriteLoop)
                {
                    Name = $"TelemetryCsvWriter_{safeBase}",
                    IsBackground = true,
                    Priority = System.Threading.ThreadPriority.BelowNormal
                };
                _workerThread.Start();
            }
            catch (Exception ex)
            {
                _hasFatalError = true;
                _lastErrorDescription = $"Failed to initialize TelemetryCsvWriter: {ex.Message}";
                Debug.LogError($"[TelemetryCsvWriter] {_lastErrorDescription}");
            }
        }

        /// <summary>
        /// Atomically claims an unused file path in the global registry to guarantee no simultaneous writes.
        /// </summary>
        private static string ReserveUniqueFilePath(string directory, string safeBase)
        {
            lock (s_registryLock)
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string candidate = Path.Combine(directory, $"{safeBase}_{timestamp}.csv");

                int attempt = 1;
                while (s_activeOutputFiles.Contains(candidate) || File.Exists(candidate))
                {
                    string uniqueGuid = Guid.NewGuid().ToString("N").Substring(0, 6);
                    candidate = Path.Combine(directory, $"{safeBase}_{timestamp}_{attempt}_{uniqueGuid}.csv");
                    attempt++;
                }

                s_activeOutputFiles.Add(candidate);
                return candidate;
            }
        }

        /// <summary>
        /// Releases a claimed file path upon writer closure or disposal.
        /// </summary>
        private static void ReleaseFilePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            lock (s_registryLock)
            {
                s_activeOutputFiles.Remove(filePath);
            }
        }

        /// <summary>
        /// Enqueues a sample from the Unity main thread without blocking.
        /// Drops sample and increments dropped counter if the bounded buffer is full.
        /// </summary>
        public bool EnqueueSample(BottleneckTelemetrySample sample)
        {
            if (!_isRunning || _hasFatalError)
            {
                Interlocked.Increment(ref _droppedSamplesCount);
                return false;
            }

            if (_sampleQueue.Count >= _maxQueueCapacity)
            {
                Interlocked.Increment(ref _droppedSamplesCount);
                LogThrottledWarning($"Buffer capacity reached ({_sampleQueue.Count}/{_maxQueueCapacity}). Dropping sample.");
                return false;
            }

            sample.droppedSamplesCumulative = _droppedSamplesCount;
            _sampleQueue.Enqueue(sample);
            _wakeEvent.Set();
            return true;
        }

        /// <summary>
        /// Dedicated background writing loop. No Unity engine APIs called here!
        /// </summary>
        private void BackgroundWriteLoop()
        {
            StreamWriter writer = null;
            try
            {
                // Open append stream with 64KB buffer for optimal sequential write throughput
                var fileStream = new FileStream(_filePath, FileMode.Append, FileAccess.Write, FileShare.Read, 65536);
                writer = new StreamWriter(fileStream, Encoding.UTF8);

                var token = _cts.Token;
                int batchWrites = 0;
                int wakeTimeoutMs = Math.Max(5, (int)(_flushIntervalSeconds * 1000f));

                while (!token.IsCancellationRequested || !_sampleQueue.IsEmpty)
                {
                    // Wait for wake signal or periodic timeout to batch flush
                    _wakeEvent.WaitOne(wakeTimeoutMs);

                    while (_sampleQueue.TryDequeue(out BottleneckTelemetrySample sample))
                    {
                        try
                        {
                            string row = sample.ToCsvRow();
                            writer.WriteLine(row);
                            _writtenSamplesCount++;
                            batchWrites++;

                            if (batchWrites >= 100)
                            {
                                writer.Flush();
                                batchWrites = 0;
                            }
                        }
                        catch (Exception ex)
                        {
                            _lastErrorDescription = $"I/O write error: {ex.Message}";
                            break;
                        }
                    }

                    if (batchWrites > 0)
                    {
                        writer.Flush();
                        batchWrites = 0;
                    }
                }

                writer.Flush();
            }
            catch (Exception ex)
            {
                _hasFatalError = true;
                _lastErrorDescription = $"Fatal background writer failure: {ex.Message}";
            }
            finally
            {
                if (writer != null)
                {
                    try { writer.Flush(); } catch { }
                    try { writer.Dispose(); } catch { }
                }
                _isRunning = false;
                ReleaseFilePath(_filePath);
            }
        }

        /// <summary>
        /// Safely flushes all pending samples and waits for the worker thread to exit cleanly.
        /// </summary>
        public void FlushAndClose(int timeoutMs = 2500)
        {
            if (!_isRunning) return;

            try
            {
                _cts.Cancel();
                _wakeEvent.Set();

                if (_workerThread != null && _workerThread.IsAlive)
                {
                    _workerThread.Join(timeoutMs);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TelemetryCsvWriter] Exception during FlushAndClose: {ex.Message}");
            }
            finally
            {
                _isRunning = false;
                ReleaseFilePath(_filePath);
            }
        }

        public void Dispose()
        {
            FlushAndClose();
            _cts.Dispose();
            _wakeEvent.Dispose();
        }

        private void LogThrottledWarning(string message)
        {
            double now = System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
            if (now - _lastErrorLogTime >= ERROR_LOG_THROTTLE_SEC)
            {
                _lastErrorLogTime = now;
                try
                {
                    Debug.LogWarning($"[TelemetryCsvWriter] {message}");
                }
                catch
                {
                    Console.WriteLine($"[TelemetryCsvWriter] {message}");
                }
            }
        }

        private static string SanitizeFilename(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
            }
            return sb.ToString();
        }
    }
}
