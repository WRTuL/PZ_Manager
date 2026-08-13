// console_page.xaml.cs — server launcher + rcon terminal + log tail in one place
using PZManager.Models;
using PZManager.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PZManager.Views
{
    public partial class ConsolePage : Page
    {
        private readonly RconService _rcon             = new();
        private readonly ServerLaunchService _launcher = new();
        private readonly ObservableCollection<ConsoleLogEntry> _entries = new();
        private readonly List<string> _history = new();
        private int _history_index = -1;
        private CancellationTokenSource? _tail_cts;

        public ConsolePage()
        {
            InitializeComponent();
            LogList.ItemsSource = _entries;

            // register services with the auto-restart page so it can control the server too
            AutoRestartPage.RegisterServices(_rcon, _launcher);

            _launcher.OutputReceived += (line, is_error) =>
                Dispatcher.Invoke(() => Append(line, is_error ? "#FF9E3A3A" : "#FF7A8099"));
            _launcher.ServerExited  += () => Dispatcher.Invoke(() => SetServerStatus(false));
            _launcher.ServerStarted += () => Dispatcher.Invoke(() => SetServerStatus(true));

            StartLogTail();
            AppendSystem("pz manager console — launch server above or connect via rcon.");
        }

        // ── server launch ─────────────────────────────────────────────────────────

        private void Launch_Click(object sender, RoutedEventArgs e)
        {
            var s = MainWindow.AppSettings;
            var ini_path = Path.Combine(s.ConfigDirectory, s.ServerConfigName + ".ini");
            var jvm_args = "-Xmx4g";
            if (File.Exists(ini_path))
            {
                var ini = IniService.ReadIni(ini_path);
                if (ini.TryGetValue("JVMArgs", out var jvm) && !string.IsNullOrWhiteSpace(jvm)) jvm_args = jvm;
            }
            AppendSystem($"launching {s.ServerConfigName}…");
            var started = _launcher.Start(s.ServerDirectory, s.ServerConfigName, jvm_args);
            if (started) { SetServerStatus(true); AppendSystem("server process started."); }
            else AppendError("failed to start — check Settings → Install Directory is correct and StartServer64.bat exists.");
        }

        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            AppendSystem("sending stop signal… (waiting up to 10s for clean shutdown)");
            _launcher.Stop();
            SetServerStatus(false);
        }

        private void SetServerStatus(bool running)
        {
            BtnLaunch.IsEnabled     = !running;
            BtnStopServer.IsEnabled = running;
            if (running)
            {
                TbServerStatus.Text       = "● server running";
                TbServerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0x9E, 0x6F));
                ServerBadge.Background    = new SolidColorBrush(Color.FromRgb(0x16, 0x2A, 0x1E));
                ServerBadge.BorderBrush   = new SolidColorBrush(Color.FromRgb(0x23, 0x4A, 0x30));
            }
            else
            {
                TbServerStatus.Text       = "● server stopped";
                TbServerStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xBF, 0x8C, 0x30));
                ServerBadge.Background    = new SolidColorBrush(Color.FromRgb(0x1E, 0x18, 0x10));
                ServerBadge.BorderBrush   = new SolidColorBrush(Color.FromRgb(0x3A, 0x30, 0x10));
            }
        }

        // ── rcon ──────────────────────────────────────────────────────────────────

        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            if (_rcon.IsConnected)
            {
                await _rcon.DisconnectAsync();
                SetRconStatus(false);
                AppendSystem("rcon disconnected.");
                return;
            }
            var s        = MainWindow.AppSettings;
            var ini_path = Path.Combine(s.ConfigDirectory, s.ServerConfigName + ".ini");
            var rcon_port = 27015; var rcon_pw = "";
            if (File.Exists(ini_path))
            {
                var ini = IniService.ReadIni(ini_path);
                if (ini.TryGetValue("RCONPort",     out var rp)  && int.TryParse(rp, out var rpv)) rcon_port = rpv;
                if (ini.TryGetValue("RCONPassword", out var rpw))                                   rcon_pw   = rpw;
            }
            AppendSystem($"connecting rcon to {s.RconHost}:{rcon_port}…");
            BtnConnect.IsEnabled = false;
            var ok = await _rcon.ConnectAsync(s.RconHost, rcon_port, rcon_pw);
            BtnConnect.IsEnabled = true;
            if (ok) { SetRconStatus(true); AppendSystem($"rcon connected on port {rcon_port}."); }
            else AppendError("rcon connection failed. is the server running? is RCONPassword set in the ini?");
        }

        private void SetRconStatus(bool connected)
        {
            if (connected)
            {
                TbRconStatus.Text     = "● rcon on";
                TbRconStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x4F, 0x9E, 0x6F));
                RconBadge.Background  = new SolidColorBrush(Color.FromRgb(0x16, 0x2A, 0x1E));
                RconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x23, 0x4A, 0x30));
                BtnConnect.Content    = "RCON Disconnect";
                TbCommand.Focus();
            }
            else
            {
                TbRconStatus.Text     = "● rcon off";
                TbRconStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x9E, 0x3A, 0x3A));
                RconBadge.Background  = new SolidColorBrush(Color.FromRgb(0x22, 0x14, 0x16));
                RconBadge.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x1E, 0x1E));
                BtnConnect.Content    = "RCON Connect";
            }
        }

        // ── commands ──────────────────────────────────────────────────────────────

        private async void Send_Click(object sender, RoutedEventArgs e) => await SendCommand();

        private async void Command_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)     { await SendCommand(); e.Handled = true; }
            else if (e.Key == Key.Up)   { NavigateHistory(-1); e.Handled = true; }
            else if (e.Key == Key.Down) { NavigateHistory(1);  e.Handled = true; }
        }

        private async Task SendCommand()
        {
            var cmd = TbCommand.Text.Trim();
            if (string.IsNullOrEmpty(cmd)) return;
            if (!_rcon.IsConnected) { AppendError("not connected to rcon — hit 'RCON Connect' first."); return; }
            TbCommand.Clear();
            _history.Insert(0, cmd); _history_index = -1;
            AppendCommand(cmd);
            var response = await _rcon.SendCommandAsync(cmd);
            if (!string.IsNullOrWhiteSpace(response)) AppendResponse(response);
        }

        private void NavigateHistory(int delta)
        {
            _history_index = Math.Clamp(_history_index + delta, -1, _history.Count - 1);
            TbCommand.Text = _history_index >= 0 ? _history[_history_index] : "";
            TbCommand.CaretIndex = TbCommand.Text.Length;
        }

        // ── log tailing ───────────────────────────────────────────────────────────

        private void StartLogTail()
        {
            _tail_cts = new CancellationTokenSource();
            var token = _tail_cts.Token;
            _ = Task.Run(async () =>
            {
                var logs_dir  = Path.Combine(MainWindow.AppSettings.ConfigDirectory, "Logs");
                string? last_file = null;
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        if (Directory.Exists(logs_dir))
                        {
                            var latest = Directory.GetFiles(logs_dir, "*.txt")
                                .OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
                            if (latest != null && latest != last_file)
                            { last_file = latest; _ = TailFileAsync(latest, token); }
                        }
                    }
                    catch { }
                    await Task.Delay(5000, token);
                }
            }, token);
        }

        private async Task TailFileAsync(string path, CancellationToken token)
        {
            try
            {
                using var fs     = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                fs.Seek(0, SeekOrigin.End);
                while (!token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(token);
                    if (line != null) Dispatcher.Invoke(() => AppendLog(line));
                    else await Task.Delay(400, token);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        // ── output helpers ────────────────────────────────────────────────────────

        private void AppendCommand(string t)  => Append($"> {t}", "#FFE2E4EC");
        private void AppendResponse(string t) => Append(t, "#FF5A8A6E");
        private void AppendSystem(string t)   => Append(t, "#FF4A4F62");
        private void AppendError(string t)    => Append(t, "#FF9E3A3A");
        private void AppendLog(string t)      => Append(t, "#FF7A8099");

        private void Append(string text, string color)
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var line in text.Split('\n'))
                {
                    var t = line.Trim('\r');
                    if (string.IsNullOrWhiteSpace(t)) continue;
                    _entries.Add(new ConsoleLogEntry { Timestamp = DateTime.Now.ToString("HH:mm:ss"), Message = t, Color = color });
                }
                while (_entries.Count > 2000) _entries.RemoveAt(0);
                LogScroller.ScrollToEnd();
            });
        }

        private void Clear_Click(object sender, RoutedEventArgs e) => _entries.Clear();
    }
}
