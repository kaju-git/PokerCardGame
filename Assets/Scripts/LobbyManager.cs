using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>
/// Handles lobby creation, joining, and the transition to the game scene.
/// It automatically finds the UIController to issue commands.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    // Core Dependencies
    private UIController uiController;
    [SerializeField] private GameObject gameManagerPrefab;

    [Header("Game References (to pass to GameManager)")]
    public DeckController deckController;
    private GameObject cardPrefab;
    
    private Lobby hostLobby;
    private Lobby joinedLobby;
    private bool uiEnabled = false;
    private Coroutine refreshLobbyCoroutine;

    private void Start()
    {
        // Automatically find the UIController in the scene
        uiController = FindAnyObjectByType<UIController>();
        if (uiController == null)
        {
            Debug.LogError("LobbyManager could not find a UIController in the scene! The game cannot start.");
            return;
        }

        // Automatically load the card prefab from Resources
        cardPrefab = Resources.Load<GameObject>("Prefabs/CardPrefab");
        if (cardPrefab == null)
        {
            Debug.LogError("Card prefab not found at 'Resources/Prefabs/CardPrefab'. Please ensure the prefab exists at this path.");
        }
        
        // Pass the logic (the CreateLobby method) to the UIController.
        uiController.AddCreateLobbyListener(CreateLobby);
        
        // Initial UI state
        uiController.ShowLobbyUI();
        uiController.UpdateStatusText("Signing in...");
        uiController.SetLobbyButtonsInteractable(false);
    }

    private void Update()
    {
        if (uiController == null) return;

        if (!uiEnabled && AuthenticationServiceWrapper.IsSignedIn)
        {
            uiEnabled = true;
            uiController.UpdateStatusText("Signed in. Create a lobby or join one.");
            uiController.SetLobbyButtonsInteractable(true);
            StartPollingLobbyList();
        }
    }
    
    public async void CreateLobby()
    {
        StopPollingLobbyList();
        uiController.SetLobbyButtonsInteractable(false);
        uiController.UpdateStatusText("Creating lobby...");

        try
        {
            string lobbyName = "GameRoom_" + Random.Range(1000, 9999);
            int maxPlayers = 2;
            CreateLobbyOptions options = new CreateLobbyOptions { IsPrivate = false };
            hostLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            uiController.UpdateStatusText("Lobby '" + lobbyName + "' created. Waiting for player...");
            StartCoroutine(HeartbeatLobbyCoroutine(hostLobby.Id, 15f));
            await PollForSecondPlayer();
        }
        catch (LobbyServiceException e)
        {
            uiController.UpdateStatusText("Failed to create lobby.");
            Debug.LogError("Failed to create lobby: " + e.Message);
            uiController.SetLobbyButtonsInteractable(true);
            StartPollingLobbyList();
        }
    }

    public async void JoinLobbyById(string lobbyId)
    {
        StopPollingLobbyList();
        uiController.SetLobbyButtonsInteractable(false);
        uiController.UpdateStatusText("Joining lobby...");

        try
        {
            joinedLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
            uiController.UpdateStatusText("Joined lobby '" + joinedLobby.Name + "'. Waiting for host...");
            StartCoroutine(PollForRelayCode());
        }
        catch (LobbyServiceException e)
        {
            uiController.UpdateStatusText("Failed to join lobby.");
            Debug.LogError("Failed to join lobby: " + e.Message);
            uiController.SetLobbyButtonsInteractable(true);
            StartPollingLobbyList();
        }
    }

    private async Task PollForSecondPlayer()
    {
        float timeout = 60f;
        while (hostLobby != null && hostLobby.Players.Count < 2 && timeout > 0)
        {
            await Task.Delay(1500);
            timeout -= 1.5f;
            if (hostLobby == null) return;
            hostLobby = await LobbyService.Instance.GetLobbyAsync(hostLobby.Id);
        }

        if (hostLobby == null || hostLobby.Players.Count < 2)
        {
            uiController.UpdateStatusText("Timed out waiting for player.");
            if (hostLobby != null) await LobbyService.Instance.DeleteLobbyAsync(hostLobby.Id);
            uiController.SetLobbyButtonsInteractable(true);
            StartPollingLobbyList();
            return;
        }

        uiController.UpdateStatusText("Player joined! Starting game...");
        
        string relayJoinCode = await AllocateRelayServerAndGetJoinCode();
        await LobbyService.Instance.UpdateLobbyAsync(hostLobby.Id, new UpdateLobbyOptions
        {
            Data = new Dictionary<string, DataObject>
            { { "RelayJoinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) } }
        });

        NetworkManager.Singleton.StartHost();
        SpawnAndSetupGameManager();
        uiController.ShowGameUI();
    }
    
    private IEnumerator PollForRelayCode()
    {
        while (joinedLobby != null)
        {
            var task = Task.Run(async () => await LobbyService.Instance.GetLobbyAsync(joinedLobby.Id));
            yield return new WaitUntil(() => task.IsCompleted);
            joinedLobby = task.Result;

            if (joinedLobby != null && joinedLobby.Data != null && joinedLobby.Data.ContainsKey("RelayJoinCode"))
            {
                string relayJoinCode = joinedLobby.Data["RelayJoinCode"].Value;
                if (!string.IsNullOrEmpty(relayJoinCode))
                {
                    JoinRelayAndStartClient(relayJoinCode);
                    yield break;
                }
            }
            yield return new WaitForSeconds(1f);
        }
    }

    private async void JoinRelayAndStartClient(string relayJoinCode)
    {
        try
        {
            await JoinRelayServerFromJoinCode(relayJoinCode);
            NetworkManager.Singleton.StartClient();
            uiController.ShowGameUI();
        }
        catch (System.Exception e)
        {
            uiController.UpdateStatusText("Failed to start client.");
            Debug.LogError("Failed to start Netcode client: " + e.Message);
        }
    }

    private void SpawnAndSetupGameManager()
    {
        if (!NetworkManager.Singleton.IsServer) return;
        GameObject gameManagerInstance = Instantiate(gameManagerPrefab);
        
        GameManager gm = gameManagerInstance.GetComponent<GameManager>();
        gm.deckController = this.deckController;

        gameManagerInstance.GetComponent<NetworkObject>().Spawn();
    }
    
    #region Lobby List Logic
    private void StartPollingLobbyList()
    {
        if (refreshLobbyCoroutine != null) StopCoroutine(refreshLobbyCoroutine);
        refreshLobbyCoroutine = StartCoroutine(RefreshLobbyListCoroutine(5f));
    }

    private void StopPollingLobbyList()
    {
        if (refreshLobbyCoroutine != null) StopCoroutine(refreshLobbyCoroutine);
        refreshLobbyCoroutine = null;
    }

    private IEnumerator RefreshLobbyListCoroutine(float waitTimeSeconds)
    {
        while (true)
        {
            QueryAndRefreshLobbyList();
            yield return new WaitForSeconds(waitTimeSeconds);
        }
    }

    private async void QueryAndRefreshLobbyList()
    {
        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                }
            };
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            
            uiController.UpdateLobbyList(response.Results, JoinLobbyById);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("Failed to query lobbies: " + e.Message);
        }
    }
    #endregion
    
    #region Network Helpers
    private async Task<string> AllocateRelayServerAndGetJoinCode()
    {
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(2);
        string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetHostRelayData(
            allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData);
        return relayJoinCode;
    }

    private async Task JoinRelayServerFromJoinCode(string joinCode)
    {
        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(
            joinAllocation.RelayServer.IpV4, (ushort)joinAllocation.RelayServer.Port,
            joinAllocation.AllocationIdBytes, joinAllocation.Key,
            joinAllocation.ConnectionData, joinAllocation.HostConnectionData);
    }

    private IEnumerator HeartbeatLobbyCoroutine(string lobbyId, float waitTimeSeconds)
    {
        var delay = new WaitForSeconds(waitTimeSeconds);
        while (hostLobby != null)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return delay;
        }
    }
    #endregion
}