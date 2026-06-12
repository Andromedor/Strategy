using System;
using System.Threading.Tasks;
using Unity.Services.Lobbies;
using UnityEngine;

namespace Strategy.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkLobbyHeartbeat : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _intervalSeconds = 15f;

        private string _lobbyId;
        private float _timer;
        private bool _isSending;

        public void Initialize(string lobbyId)
        {
            _lobbyId = lobbyId;
            _timer = 0f;
        }

        private void Update()
        {
            if (string.IsNullOrWhiteSpace(_lobbyId) || _isSending)
                return;

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f)
                return;

            _timer = _intervalSeconds;
            _ = SendHeartbeatAsync();
        }

        private async Task SendHeartbeatAsync()
        {
            _isSending = true;
            try
            {
                await LobbyService.Instance.SendHeartbeatPingAsync(_lobbyId);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Lobby heartbeat failed: " + exception.Message);
            }
            finally
            {
                _isSending = false;
            }
        }
    }
}
