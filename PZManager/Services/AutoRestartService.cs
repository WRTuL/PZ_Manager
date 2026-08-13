// auto_restart_service.cs
// restarts the server on a schedule because mods update constantly
// and the server won't pick up new versions until you restart it.
// sends rcon warnings so players get a heads up instead of just getting booted.
// also kills the entire java process tree on shutdown because pz spawns child processes
// that don't die when you kill the parent, which causes "malformed packet" errors
// when the new instance tries to bind the same port the old jvm is still sitting on.
// yes this is as annoying as it sounds. yes it took a while to figure out. no i'm not bitter. just questioning my life choices
using System.Timers;
using Timer = System.Timers.Timer;

namespace PZManager.Services
{
    public class AutoRestartService
    {
        private Timer? _restart_timer;
        private Timer? _warning_timer;
        private RconService? _rcon;
        private ServerLaunchService? _launcher;

        // fired when something noteworthy happens — log message + is it bad?
        public event Action<string, bool>? StatusMessage;

        // fired just before the restart actually happens — ui can update
        public event Action? RestartImminent;

        private bool _running = false;
        private DateTime _next_restart;
        private string _server_dir  = "";
        private string _config_name = "";
        private string _jvm_args    = "";
        private string _warning_template = "Server restarting in {minutes} minutes for maintenance.";

        // warning intervals in minutes before restart — counts down at each one
        private static readonly int[] warning_minutes = { 30, 15, 5, 1 };

        public bool IsRunning => _running;
        public DateTime NextRestart => _next_restart;

        /// <summary>
        /// starts the auto-restart schedule.
        /// interval_hours: how often to restart, in real-time hours (e.g. 6.0 = every 6 hours).
        /// rcon and launcher are injected so we can send warnings and do the actual restart.
        /// </summary>
        public void Start(double interval_hours, RconService rcon, ServerLaunchService launcher,
                          string server_dir, string config_name, string jvm_args,
                          string warning_template)
        {
            Stop(); // clean up any existing timer first

            _rcon             = rcon;
            _launcher         = launcher;
            _server_dir       = server_dir;
            _config_name      = config_name;
            _jvm_args         = jvm_args;
            _warning_template = warning_template;
            _running          = true;

            ScheduleNextRestart(interval_hours);
            StatusMessage?.Invoke($"auto-restart enabled — next restart at {_next_restart:HH:mm:ss}", false);
        }

        private void ScheduleNextRestart(double interval_hours)
        {
            var interval_ms = interval_hours * 60 * 60 * 1000;
            _next_restart   = DateTime.Now.AddHours(interval_hours);

            // main restart timer
            _restart_timer = new Timer(interval_ms) { AutoReset = false };
            _restart_timer.Elapsed += async (_, _) => await DoRestart(interval_hours);
            _restart_timer.Start();

            // warning timers — one for each warning interval that fits within the restart interval
            ScheduleWarnings(interval_hours);
        }

        private void ScheduleWarnings(double interval_hours)
        {
            _warning_timer?.Stop();
            _warning_timer?.Dispose();

            var total_minutes = interval_hours * 60;

            foreach (var warn_mins in warning_minutes)
            {
                if (warn_mins >= total_minutes) continue; // warning would be before or at the start — skip

                var delay_ms = (total_minutes - warn_mins) * 60 * 1000;
                var warn_timer = new Timer(delay_ms) { AutoReset = false };
                var capture = warn_mins; // capture for closure
                warn_timer.Elapsed += async (_, _) => await SendWarning(capture);
                warn_timer.Start();
            }
        }

        private async Task SendWarning(int minutes_remaining)
        {
            if (!_running) return;

            var msg = _warning_template.Replace("{minutes}", minutes_remaining.ToString());

            if (_rcon?.IsConnected == true)
            {
                // preferred path: RCON broadcast
                await _rcon.SendCommandAsync($"servermsg \"{msg}\"");
                StatusMessage?.Invoke($"[auto-restart] warning sent via rcon: {minutes_remaining}min remaining", false);
            }
            else if (_launcher?.IsRunning == true)
            {
                // fallback path: stdin — works even if RCON port is blocked. this is here because RCON port can be blocked, looking at you, you know who you are ;)
                // PZ server accepts the same commands on stdin as it does via RCON
                var ok = _launcher.SendConsoleCommand($"servermsg \"{msg}\"");
                StatusMessage?.Invoke(ok
                    ? $"[auto-restart] warning sent via stdin (rcon unavailable): {minutes_remaining}min remaining"
                    : $"[auto-restart] warning could not be sent — server not running?", !ok);
            }
            else
            {
                StatusMessage?.Invoke($"[auto-restart] warning skipped — neither rcon nor server process available", true);
            }
        }

        private async Task DoRestart(double interval_hours)
        {
            if (!_running) return;

            StatusMessage?.Invoke("[auto-restart] initiating scheduled restart…", false);
            RestartImminent?.Invoke();

            // take a backup before anything else — clean snapshot at a known-good point
            if (PZManager.Views.BackupPage.AutoBackupEnabled)
            {
                StatusMessage?.Invoke("[auto-restart] taking pre-restart backup…", false);
                PZManager.Views.BackupPage.TakeAutoBackup(MainWindow_AppSettings);
                StatusMessage?.Invoke("[auto-restart] backup done.", false);
            }

            // save before shutting down — RCON first, stdin fallback
            if (_rcon?.IsConnected == true)
            {
                StatusMessage?.Invoke("[auto-restart] sending save via rcon…", false);
                await _rcon.SendCommandAsync("save");
                await Task.Delay(5000); // give PZ time to flush the save to disk
            }
            else if (_launcher?.IsRunning == true)
            {
                StatusMessage?.Invoke("[auto-restart] sending save via stdin (rcon unavailable)…", false);
                _launcher.SendConsoleCommand("save");
                await Task.Delay(5000);
            }

            // tell the launcher this is a managed restart so it suppresses the ServerExited event —
            // we don't want ConsolePage flipping to "server stopped" in the middle of a restart
            _launcher?.BeginManagedRestart();

            // graceful stop — kills process tree if it doesn't exit in time
            _launcher?.Stop();

            // wait for the port to actually free. 10s is conservative but safe.
            // too short = new JVM binds before old one releases → malformed packets + FPS spikes
            StatusMessage?.Invoke("[auto-restart] waiting for port to clear…", false);
            await Task.Delay(10_000);

            // read latest jvm args in case they changed while the server was running
            var ini_path = System.IO.Path.Combine(MainWindow_AppSettings.ConfigDirectory,
                                                   MainWindow_AppSettings.ServerConfigName + ".ini");
            var jvm = _jvm_args;
            if (System.IO.File.Exists(ini_path))
            {
                var ini = PZManager.Services.IniService.ReadIni(ini_path);
                if (ini.TryGetValue("JVMArgs", out var j) && !string.IsNullOrWhiteSpace(j)) jvm = j;
            }

            var started = _launcher?.Start(_server_dir, _config_name, jvm) ?? false;
            // EndManagedRestart re-enables the ServerExited event for future normal exits
            _launcher?.EndManagedRestart();

            StatusMessage?.Invoke(started
                ? "[auto-restart] server restarted successfully."
                : "[auto-restart] ERROR — server failed to restart. check the log.", !started);

            ScheduleNextRestart(interval_hours);
            StatusMessage?.Invoke($"[auto-restart] next restart at {_next_restart:HH:mm:ss}", false);
        }

        public void Stop()
        {
            _running = false;
            _restart_timer?.Stop(); _restart_timer?.Dispose(); _restart_timer = null;
            _warning_timer?.Stop(); _warning_timer?.Dispose(); _warning_timer = null;
        }

        // static accessor so DoRestart can reach AppSettings without a circular dependency.
        // yes this is slightly dirty. no, i don't regret it. cry about it.
        internal static PZManager.Models.AppSettings MainWindow_AppSettings
            => PZManager.MainWindow.AppSettings;
    }
}
