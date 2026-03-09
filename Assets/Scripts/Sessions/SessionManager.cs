using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using Unity.Networking.Transport.Relay;

/// <summary>
/// Singleton that manages the full session lifecycle: Unity Gaming Services initialization,
/// lobby creation/joining, relay allocation, heartbeat, and lobby polling.
/// Persists across scene loads via DontDestroyOnLoad.
/// </summary>
public class SessionManager : MonoBehaviour
{
    /// <summary>Global singleton reference accessible from any script.</summary>
    public static SessionManager Instance { get; private set; }

    [Header("Lobby")]
    /// <summary>Maximum number of players allowed in the session (host + clients).</summary>
    public int MaxPlayers = 4;

    /// <summary>If true, the lobby will not appear in public listings.</summary>
    public bool IsPrivateLobby = false;

    /// <summary>The active lobby object returned by the Lobby Service.</summary>
    public Lobby CurrentLobby { get; private set; }

    /// <summary>The relay join code generated when hosting, stored for reference.</summary>
    public string CurrentRelayJoinCode { get; private set; }

    // Timers for periodic lobby operations
    float _heartbeatTimer;
    float _pollTimer;

    /// <summary>Interval in seconds between heartbeat pings sent by the host.</summary>
    const float HeartbeatInterval = 15f;

    /// <summary>Interval in seconds between lobby data refresh polls.</summary>
    const float PollInterval = 2.5f;

    /// <summary>
    /// Enforces the singleton pattern and marks this object to persist across scenes.
    /// Also ensures the NetworkManager survives scene transitions.
    /// </summary>
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (NetworkManager.Singleton != null)
            DontDestroyOnLoad(NetworkManager.Singleton.gameObject);
    }

    /// <summary>
    /// Subscribes to the NetworkManager's client connected callback once the scene is ready.
    /// </summary>
    void Start()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
    }

    /// <summary>
    /// Unsubscribes from the client connected callback to prevent memory leaks on destruction.
    /// </summary>
    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    /// <summary>
    /// Called whenever a client successfully connects to the session.
    /// Can be used to trigger UI updates or game logic.
    /// </summary>
    /// <param name="clientId">The network ID of the newly connected client.</param>
    void OnClientConnected(ulong clientId)
    {
        // UI UPDATE
    }

    /// <summary>
    /// Handles periodic lobby heartbeat (host only) and lobby data polling (all players).
    /// Heartbeat prevents the lobby from expiring on the UGS backend.
    /// Polling keeps the local lobby state in sync with the server.
    /// </summary>
    void Update()
    {
        // Only the host is responsible for sending heartbeat pings
        if (CurrentLobby != null && IsLobbyHost())
        {
            _heartbeatTimer -= Time.deltaTime;
            if (_heartbeatTimer <= 0f)
            {
                _heartbeatTimer = HeartbeatInterval;
                _ = SendHeartbeatAsync();
            }
        }

        // Both host and clients poll for lobby updates (e.g. new players joining)
        if (CurrentLobby != null)
        {
            _pollTimer -= Time.deltaTime;
            if (_pollTimer <= 0f)
            {
                _pollTimer = PollInterval;
                _ = RefreshLobbyAsync();
            }
        }
    }

    /// <summary>
    /// Initializes Unity Gaming Services and signs the player in anonymously if not already signed in.
    /// Safe to call multiple times — skips initialization if already done.
    /// </summary>
    public async Task EnsureServicesAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    /// <summary>
    /// Returns true if the local player is the host of the current lobby.
    /// Determined by comparing the local PlayerId with the lobby's HostId.
    /// </summary>
    bool IsLobbyHost()
    {
        return CurrentLobby != null && CurrentLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    /// <summary>
    /// Sends a heartbeat ping to the Lobby Service to prevent the lobby from expiring.
    /// Only called by the host.
    /// </summary>
    async Task SendHeartbeatAsync()
    {
        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Lobby heartbeat failed: {e.Message}");
        }
    }

    /// <summary>
    /// Fetches the latest lobby data from the Lobby Service and updates the local reference.
    /// Used to detect changes such as new players joining.
    /// </summary>
    async Task RefreshLobbyAsync()
    {
        try
        {
            CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Lobby refresh failed: {e.Message}");
        }
    }

    // ---------------------------
    // HOST FLOW
    // ---------------------------

    /// <summary>
    /// Full host setup flow:
    /// 1. Creates a Relay allocation and retrieves a join code.
    /// 2. Creates a public lobby with the relay join code embedded in its data.
    /// 3. Configures the Unity Transport with the relay server details.
    /// 4. Starts the NetworkManager as host and loads the game scene.
    /// </summary>
    /// <param name="sessionName">The display name for the lobby.</param>
    public async Task HostSessionAsync(string sessionName)
    {
        await EnsureServicesAsync();

        // 1) Create a Relay allocation for up to (MaxPlayers - 1) clients (host excluded)
        Allocation alloc = await RelayService.Instance.CreateAllocationAsync(MaxPlayers - 1);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
        CurrentRelayJoinCode = joinCode;

        // 2) Store the relay join code in public lobby data so joining clients can read it
        var data = new Dictionary<string, DataObject>
        {
            { "joinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
        };

        var options = new CreateLobbyOptions
        {
            IsPrivate = IsPrivateLobby,
            Data = data
        };

        CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(sessionName, MaxPlayers, options);
        _heartbeatTimer = HeartbeatInterval;
        _pollTimer = PollInterval;

        // 3) Configure the Unity Transport with the Relay server details, then start as host
        var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
        utp.SetHostRelayData(
            alloc.RelayServer.IpV4,
            (ushort)alloc.RelayServer.Port,
            alloc.AllocationIdBytes,
            alloc.Key,
            alloc.ConnectionData
        );

        NetworkManager.Singleton.StartHost();

        // Load the game scene for all connected clients via the NetworkManager scene manager
        NetworkManager.Singleton.SceneManager.LoadScene("Game", UnityEngine.SceneManagement.LoadSceneMode.Single);

        Debug.Log($"HOST started. SessionName='{sessionName}', LobbyCode={CurrentLobby.LobbyCode}, RelayJoinCode={joinCode}");
    }

    // ---------------------------
    // CLIENT FLOW
    // ---------------------------

    /// <summary>
    /// Full client join flow:
    /// 1. Joins the lobby using the human-readable lobby code.
    /// 2. Reads the relay join code from the lobby's public data.
    /// 3. Joins the Relay allocation using that code.
    /// 4. Configures the Unity Transport and starts the NetworkManager as a client.
    /// </summary>
    /// <param name="lobbyCode">The human-readable lobby code shared by the host.</param>
    public async Task JoinSessionByLobbyCodeAsync(string lobbyCode)
    {
        await EnsureServicesAsync();

        // 1) Join the lobby using the short human-friendly code
        CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
        _pollTimer = PollInterval;

        // 2) Retrieve the relay join code stored in the lobby's public data
        if (!CurrentLobby.Data.TryGetValue("joinCode", out var joinCodeObj))
            throw new Exception("Lobby does not contain 'joinCode' in Data.");

        string joinCode = joinCodeObj.Value;

        // 3) Join the relay allocation using the retrieved join code
        JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);

        // 4) Configure the Unity Transport with the Relay server details, then start as client
        var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
        utp.SetClientRelayData(
            joinAlloc.RelayServer.IpV4,
            (ushort)joinAlloc.RelayServer.Port,
            joinAlloc.AllocationIdBytes,
            joinAlloc.Key,
            joinAlloc.ConnectionData,
            joinAlloc.HostConnectionData  // Required by the client to reach the host through the relay
        );

        NetworkManager.Singleton.StartClient();

        Debug.Log($"CLIENT started. Joined LobbyCode={lobbyCode} via RelayJoinCode={joinCode}");
    }

    /// <summary>
    /// Gracefully leaves the current lobby.
    /// The host deletes the lobby entirely; clients simply remove themselves from it.
    /// Clears local session state regardless of outcome.
    /// </summary>
    public async Task LeaveLobbyAsync()
    {
        if (CurrentLobby == null) return;

        try
        {
            if (IsLobbyHost())
                await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
            else
                await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, AuthenticationService.Instance.PlayerId);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"LeaveLobby failed: {e.Message}");
        }
        finally
        {
            // Always clear local state, even if the service call failed
            CurrentLobby = null;
            CurrentRelayJoinCode = null;
        }
    }
}