// rcon_service.cs
// wraps the CoreRCON library so the rest of the app doesn't have to think about it.
// rcon is a simple tcp protocol that lets you run server commands remotely.
// pz implements it correctly which is gud. very gud.
// note: CoreRCON is IDisposable, NOT IAsyncDisposable. this matters. don't let the IDE
// convince you to add await to the dispose call. it will not end well.
using CoreRCON;
using System.Net;

namespace PZManager.Services
{
    public class RconService : IAsyncDisposable
    {
        private RCON? _rcon;
        private bool  _connected;
        public bool IsConnected => _connected;

        public async Task<bool> ConnectAsync(string host, int port, string password)
        {
            try
            {
                await DisconnectAsync();
                _rcon = new RCON(IPAddress.Parse(host), (ushort)port, password);
                await _rcon.ConnectAsync();
                _connected = true;
                return true;
            }
            catch { _connected = false; return false; }
        }

        public async Task<string> SendCommandAsync(string command)
        {
            if (_rcon == null || !_connected)
                return "[not connected to rcon]";
            try { return await _rcon.SendCommandAsync(command); }
            catch (Exception ex) { _connected = false; return $"[rcon error: {ex.Message}]"; }
        }

        public Task DisconnectAsync()
        {
            if (_rcon != null) { try { _rcon.Dispose(); } catch { } _rcon = null; }
            _connected = false;
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync() => await DisconnectAsync();
    }
}
