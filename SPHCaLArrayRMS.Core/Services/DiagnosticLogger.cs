using System.Text;

namespace SPHCaLArrayRMS.Core.Services
{
    public static class DiagnosticLogger
    {
        private static readonly object SyncRoot = new();
        private static StreamWriter? _writer;

        public static event Action<string>? LogReceived;

        public static void Start()
        {
            lock (SyncRoot)
            {
                if (_writer != null) return;
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DebugLogs");
                Directory.CreateDirectory(folder);
                string file = Path.Combine(folder, $"RMS_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                _writer = new StreamWriter(file, true, Encoding.UTF8) { AutoFlush = true };
            }
            Log("System monitor started.");
        }

        public static void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
            lock (SyncRoot)
                _writer?.WriteLine(line);
            LogReceived?.Invoke(line);
        }

        public static void Stop()
        {
            lock (SyncRoot)
            {
                _writer?.Dispose();
                _writer = null;
            }
        }
    }
}
