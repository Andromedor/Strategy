using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Strategy.Core;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace Strategy.Networking
{
    public readonly struct NetworkSessionResult
    {
        public bool Success { get; }
        public string JoinCode { get; }
        public string Message { get; }

        private NetworkSessionResult(bool success, string joinCode, string message)
        {
            Success = success;
            JoinCode = joinCode;
            Message = message;
        }

        public static NetworkSessionResult Ok(string joinCode, string message)
        {
            return new NetworkSessionResult(true, joinCode, message);
        }

        public static NetworkSessionResult Fail(string message)
        {
            return new NetworkSessionResult(false, string.Empty, message);
        }
    }

    public static class NetworkSessionService
    {
#if UNITY_WEBGL
        private const string RelayConnectionType = "wss";
#else
        private const string RelayConnectionType = "dtls";
#endif

        private const string RelayJoinCodeKey = "relay_join_code";
        private const string MapIdKey = "map_id";
        private const string TeamModeKey = "team_mode";
        private const string StartingResourcesKey = "starting_resources";

        public static async Task<NetworkSessionResult> HostLobbyAsync(MatchLaunchConfig config)
        {
            if (config == null)
                return NetworkSessionResult.Fail("Match config is missing.");

            try
            {
                NetworkManager networkManager = EnsureNetworkManager();
                await InitializeUnityServices();

                int maxConnections = Mathf.Max(1, config.Teams.Count - 1);
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
                string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                ConfigureTransport(networkManager, allocation);

                Lobby lobby = await LobbyService.Instance.CreateLobbyAsync(
                    BuildLobbyName(config),
                    Mathf.Max(2, config.Teams.Count),
                    new CreateLobbyOptions
                    {
                        IsPrivate = false,
                        Data = BuildLobbyData(config, relayJoinCode)
                    });

                AttachHeartbeat(networkManager.gameObject, lobby.Id);

                if (!networkManager.StartHost())
                    return NetworkSessionResult.Fail("NetworkManager.StartHost failed.");

                return NetworkSessionResult.Ok(lobby.LobbyCode, "Lobby created. Code: " + lobby.LobbyCode);
            }
            catch (Exception exception)
            {
                return NetworkSessionResult.Fail("Online host failed: " + exception.Message);
            }
        }

        public static async Task<NetworkSessionResult> JoinLobbyAsync(string lobbyCode)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode))
                return NetworkSessionResult.Fail("Lobby code is empty.");

            try
            {
                NetworkManager networkManager = EnsureNetworkManager();
                await InitializeUnityServices();

                Lobby lobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode.Trim());
                if (lobby.Data == null ||
                    !lobby.Data.TryGetValue(RelayJoinCodeKey, out DataObject relayCodeData) ||
                    string.IsNullOrWhiteSpace(relayCodeData.Value))
                {
                    return NetworkSessionResult.Fail("Lobby does not contain Relay connection data.");
                }

                JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(relayCodeData.Value);
                ConfigureTransport(networkManager, allocation);

                return networkManager.StartClient()
                    ? NetworkSessionResult.Ok(lobby.LobbyCode, "Connected to lobby " + lobby.LobbyCode)
                    : NetworkSessionResult.Fail("NetworkManager.StartClient failed.");
            }
            catch (Exception exception)
            {
                return NetworkSessionResult.Fail("Online join failed: " + exception.Message);
            }
        }

        private static async Task InitializeUnityServices()
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        private static NetworkManager EnsureNetworkManager()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null)
                return networkManager;

            GameObject root = new("NetworkManager");
            UnityEngine.Object.DontDestroyOnLoad(root);

            networkManager = root.AddComponent<NetworkManager>();
            UnityTransport transport = root.AddComponent<UnityTransport>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                EnableSceneManagement = true
            };
            root.AddComponent<NetworkCommandBridge>();
            return networkManager;
        }

        private static UnityTransport GetTransport(NetworkManager networkManager)
        {
            if (!networkManager.TryGetComponent(out UnityTransport transport))
                transport = networkManager.gameObject.AddComponent<UnityTransport>();

            transport.UseWebSockets = RelayConnectionType.StartsWith("ws", StringComparison.Ordinal);
            networkManager.NetworkConfig.NetworkTransport = transport;
            return transport;
        }

        private static void ConfigureTransport(NetworkManager networkManager, Allocation allocation)
        {
            UnityTransport transport = GetTransport(networkManager);
            transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnectionType));
        }

        private static void ConfigureTransport(NetworkManager networkManager, JoinAllocation allocation)
        {
            UnityTransport transport = GetTransport(networkManager);
            transport.SetRelayServerData(allocation.ToRelayServerData(RelayConnectionType));
        }

        private static Dictionary<string, DataObject> BuildLobbyData(MatchLaunchConfig config, string relayJoinCode)
        {
            DataObject.VisibilityOptions publicData = DataObject.VisibilityOptions.Public;
            DataObject.VisibilityOptions memberData = DataObject.VisibilityOptions.Member;
            return new Dictionary<string, DataObject>
            {
                [RelayJoinCodeKey] = new(memberData, relayJoinCode),
                [MapIdKey] = new(publicData, config.MapId),
                [TeamModeKey] = new(publicData, config.TeamMode.ToString()),
                [StartingResourcesKey] = new(publicData, config.Teams.Count > 0 ? config.Teams[0].StartingResources.ToString() : "0")
            };
        }

        private static string BuildLobbyName(MatchLaunchConfig config)
        {
            string mapName = config.Map != null ? config.Map.DisplayName : config.MapId;
            return string.IsNullOrWhiteSpace(mapName) ? "RTS Skirmish" : "RTS - " + mapName;
        }

        private static void AttachHeartbeat(GameObject target, string lobbyId)
        {
            NetworkLobbyHeartbeat heartbeat = target.GetComponent<NetworkLobbyHeartbeat>();
            if (heartbeat == null)
                heartbeat = target.AddComponent<NetworkLobbyHeartbeat>();

            heartbeat.Initialize(lobbyId);
        }
    }
}
