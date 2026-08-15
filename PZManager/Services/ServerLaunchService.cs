// server_launch_service.cs
// look at me. i am the window now.
using System.Diagnostics;
using System.IO;

namespace PZManager.Services
{
    public class ServerLaunchService
    {
        private Process? _process;
        private bool _managed_restart = false; // suppresses ServerExited during auto-restart

        public bool IsRunning => _process is { HasExited: false };
        public event Action<string, bool>? OutputReceived;
        public event Action? ServerExited;
        public event Action? ServerStarted;

        /// Call before Stop() during an auto-restart to prevent the UI flipping to "stopped"
        public void BeginManagedRestart() => _managed_restart = true;
        /// Call after Start() completes during an auto-restart to restore normal exit handling
        public void EndManagedRestart()   => _managed_restart = false;

        public bool Start(string server_directory, string config_name, string jvm_args)
        {
            if (IsRunning) { OutputReceived?.Invoke("[server is already running]", true); return false; }

            var bat = Path.Combine(server_directory, "StartServer64.bat");
            if (!File.Exists(bat)) { OutputReceived?.Invoke($"[ERROR] can't find {bat} — check Settings", true); return false; }

            var psi = new ProcessStartInfo
            {
                FileName         = bat,
                Arguments        = $"-servername {config_name}",
                WorkingDirectory = server_directory,
                UseShellExecute  = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                RedirectStandardInput  = true,
                CreateNoWindow   = true,
            };
            if (!string.IsNullOrWhiteSpace(jvm_args))
                psi.EnvironmentVariables["JAVA_OPTS"] = jvm_args;

            try
            {
                _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
                _process.OutputDataReceived += (_, e) => { if (e.Data != null) OutputReceived?.Invoke(e.Data, false); };
                _process.ErrorDataReceived  += (_, e) => { if (e.Data != null) OutputReceived?.Invoke(e.Data, true); };
                _process.Exited += (_, _) =>
                {
                    OutputReceived?.Invoke("[server process has exited]", false);
                    if (!_managed_restart) ServerExited?.Invoke();
                };
                _process.Start();
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
                ServerStarted?.Invoke();   // <-- tell the UI we're back up
                return true;
            }
            catch (Exception ex)
            {
                OutputReceived?.Invoke($"[ERROR] {ex.Message}", true);
                _process = null;
                return false;
            }
        }

        /// <summary>
        /// Graceful stop: send quit via stdin, wait up to 25s, then kill the whole process tree.
        /// PZ spawns a child JVM — killing only the bat process leaves it running and holding the port.
        /// </summary>
        public void Stop()
        {
            if (_process is not { HasExited: false }) return;
            try { _process.StandardInput.WriteLine("quit"); } catch { }

            // give PZ up to 25s to save and shut down cleanly before we pull the plug
            if (!_process.WaitForExit(25_000))
            {
                OutputReceived?.Invoke("[server] graceful shutdown timed out — force-killing process tree", true);
                KillProcessTree(_process);
            }
            _process = null;
        }

        /// <summary>
        /// Immediately kills the server process and all its children (the JVM).
        /// Use when Stop() has already been called but the process is stuck,
        /// or when called directly from the Force Kill button in Settings.
        /// </summary>
        public void ForceKill()
        {
            if (_process == null) return;
            try { KillProcessTree(_process); } catch { }
            _process = null;
            OutputReceived?.Invoke("[server] force-killed.", true);
            ServerExited?.Invoke();
        }

        /// Kills a process and all its child processes recursively.
        /// Required on Windows because bat -> java is a process tree, not a single process.
        private static void KillProcessTree(Process root)
        {
            try
            {
                // get all children before killing root, as killing root may orphan them
                var children = GetChildProcesses(root.Id);
                try { root.Kill(entireProcessTree: true); } catch { }
                // belt and suspenders — kill any that survived
                foreach (var child in children)
                    try { if (!child.HasExited) child.Kill(); } catch { }
            }
            catch { }
        }

        private static List<Process> GetChildProcesses(int parent_id)
        {
            var result = new List<Process>();
            try
            {
                // use WMI to find child processes — works on all Windows versions
                using var searcher = new System.Management.ManagementObjectSearcher(
                    $"SELECT ProcessId FROM Win32_Process WHERE ParentProcessId = {parent_id}");
                foreach (var obj in searcher.Get())
                {
                    var child_id = Convert.ToInt32(obj["ProcessId"]);
                    try { result.Add(Process.GetProcessById(child_id)); } catch { }
                    // recurse into grandchildren (bat -> java -> possible grandchildren)
                    result.AddRange(GetChildProcesses(child_id));
                }
            }
            catch { }
            return result;
        }

        public bool SendConsoleCommand(string command)
        {
            if (_process is not { HasExited: false }) return false;
            try
            {
                _process.StandardInput.WriteLine(command);
                _process.StandardInput.Flush();
                return true;
            }
            catch { return false; }
        }
    }
}
