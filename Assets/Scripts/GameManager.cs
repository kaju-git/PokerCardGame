using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    private UIController uiController;
    public DeckController deckController;

    private NetworkVariable<GameStateData> currentGameState = new NetworkVariable<GameStateData>();
    private NetworkVariable<int> playerPoints = new NetworkVariable<int>(10);
    private NetworkVariable<int> opponentPoints = new NetworkVariable<int>(10);
    private NetworkVariable<int> potSize = new NetworkVariable<int>(0);
    private NetworkVariable<int> currentBet = new NetworkVariable<int>(0);
    private NetworkVariable<int> player0Bet = new NetworkVariable<int>(0);
    private NetworkVariable<int> player1Bet = new NetworkVariable<int>(0);

    private Dictionary<ulong, int> betsInRound = new Dictionary<ulong, int>();
    private List<Card> playerHand = new List<Card>();
    private List<Card> opponentHand = new List<Card>();
    private List<Card> communityCards = new List<Card>();
    private List<CardView> opponentCardViews = new List<CardView>();
    
    private List<ulong> readyClients = new List<ulong>();
    private ulong actionTargetId; // Player who made the last aggressive action (bet/raise)

    public struct GameStateData : INetworkSerializable, System.IEquatable<GameStateData>
    {
        public enum State { PreDeal, Betting, Dealing, Showdown, RoundOver }
        public State CurrentState;
        public int Round; // 0:Pre-Flop, 1:Flop, 2:Turn, 3:River
        public ulong ActivePlayerId;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref CurrentState);
            serializer.SerializeValue(ref Round);
            serializer.SerializeValue(ref ActivePlayerId);
        }
        public bool Equals(GameStateData other) => CurrentState == other.CurrentState && Round == other.Round && ActivePlayerId == other.ActivePlayerId;
        public override int GetHashCode() => base.GetHashCode();
    }

    private enum PlayerAction { CheckOrCall, Bet, Fold }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        uiController = FindAnyObjectByType<UIController>();
        if (uiController == null) { Debug.LogError("GameManager could not find a UIController in the scene!"); return; }

        uiController.AddGameActionListeners(
            () => OnActionButtonClickedServerRpc(PlayerAction.CheckOrCall),
            (amount) => OnActionButtonClickedServerRpc(PlayerAction.Bet, amount),
            () => OnActionButtonClickedServerRpc(PlayerAction.Fold)
        );

        currentGameState.OnValueChanged += OnGameStateChanged;
        playerPoints.OnValueChanged += (p, n) => UpdatePointsUI();
        opponentPoints.OnValueChanged += (p, n) => UpdatePointsUI();
        potSize.OnValueChanged += (p, n) => UpdatePointsUI();
        player0Bet.OnValueChanged += (p, n) => UpdateBetUI();
        player1Bet.OnValueChanged += (p, n) => UpdateBetUI();

        UpdatePointsUI();
        UpdateBetUI();
        
        if (IsServer)
        {
            playerPoints.Value = 10; // Increase starting points for more rounds
            opponentPoints.Value = 10;
        }

        ClientReadyServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void ClientReadyServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (!readyClients.Contains(clientId))
        {
            readyClients.Add(clientId);
        }

        if (IsServer && readyClients.Count == NetworkManager.Singleton.ConnectedClientsIds.Count && NetworkManager.Singleton.ConnectedClientsIds.Count >= 2)
        {
            StartNewRound();
        }
    }
    
    [ServerRpc(RequireOwnership = false)]
    private void OnActionButtonClickedServerRpc(PlayerAction action, int totalBetAmount = 0, ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        if (clientId != currentGameState.Value.ActivePlayerId) return;

        switch (action)
        {
            case PlayerAction.CheckOrCall:
                // If current bet is 0, it's a Check. Otherwise, it's a Call.
                int amountToCall = currentBet.Value - GetPlayerBet(clientId); // 変更: GetPlayerBet を使用
                if (amountToCall > 0)
                {
                    // Call
                    if (GetPlayerPoints(clientId) >= amountToCall)
                    {
                        SetPlayerPoints(clientId, GetPlayerPoints(clientId) - amountToCall);
                        SetPlayerBet(clientId, GetPlayerBet(clientId) + amountToCall); 
                        potSize.Value += amountToCall;
                    }
                }
                // Check is implicit (no points change)
                EndTurn();
                break;
            case PlayerAction.Bet:
                if (totalBetAmount > 5) totalBetAmount = 5; // Bet上限 (5) を強制
                int amountToRaise = totalBetAmount - GetPlayerBet(clientId); // 変更: GetPlayerBet を使用

                if (GetPlayerPoints(clientId) >= amountToRaise)
                {
                    currentBet.Value = totalBetAmount;
                    SetPlayerPoints(clientId, GetPlayerPoints(clientId) - amountToRaise);
                    SetPlayerBet(clientId, GetPlayerBet(clientId) + amountToRaise);
                    potSize.Value += amountToRaise;
                    actionTargetId = clientId; // This player is now the one to beat.
                    EndTurn();
                }
                break;
            case PlayerAction.Fold:
                ulong winnerId = NetworkManager.Singleton.ConnectedClientsIds.First(id => id != clientId);
                SetPlayerPoints(winnerId, GetPlayerPoints(winnerId) + potSize.Value);
                currentGameState.Value = new GameStateData { CurrentState = GameStateData.State.RoundOver, Round = currentGameState.Value.Round };
                AnnounceWinnerClientRpc("Player " + winnerId + " wins by fold!");
                break;
        }
    }
    
    private void OnGameStateChanged(GameStateData previous, GameStateData current)
    {
        bool isMyTurn = current.ActivePlayerId == NetworkManager.Singleton.LocalClientId;
        if (uiController != null)
        {
            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            int myPoints = GetPlayerPoints(localClientId);
            int myBet = GetPlayerBet(localClientId);
            uiController.UpdateActionPanel(
                current.CurrentState == GameStateData.State.Betting, 
                isMyTurn, 
                myPoints,
                myBet,
                currentBet.Value);
        }

        if (current.CurrentState == GameStateData.State.RoundOver && previous.CurrentState != GameStateData.State.RoundOver)
        {
            if(IsServer) readyClients.Clear();
            StartCoroutine(DelayedStartNewRound(3f));
        }
    }
    private void UpdateActionPanelUI() // 追加
    {
        if (uiController == null) return;
        var state = currentGameState.Value;
        ulong localClientId = NetworkManager.Singleton.LocalClientId;
        bool isMyTurn = state.ActivePlayerId == localClientId;
        int myPoints = GetPlayerPoints(localClientId);
        int myBet = GetPlayerBet(localClientId); // 変更: GetPlayerBet を使用
        
        uiController.UpdateActionPanel(state.CurrentState == GameStateData.State.Betting, isMyTurn, myPoints, myBet, currentBet.Value);
    }

    private IEnumerator DelayedStartNewRound(float delay)
    {
        if (uiController != null)
        {
            uiController.UpdateWinnerText(uiController.GetWinnerText() + "\nStarting new round in " + delay + "s...");
        }
        yield return new WaitForSeconds(delay);
        if(IsServer) StartNewRound();
    }
    
    private void UpdatePointsUI()
    {
        if (uiController != null)
        {
            uiController.UpdatePointsDisplay(playerPoints.Value, opponentPoints.Value, potSize.Value);
        }
    }

    private void UpdateBetUI()
    {
        if (uiController != null)
        {
            uiController.UpdateBetDisplay(player0Bet.Value, player1Bet.Value, NetworkManager.Singleton.LocalClientId);
        }
    }

    private void StartNewRound()
    {
        if (!IsServer || NetworkManager.Singleton.ConnectedClientsIds.Count < 2) return;
        
        communityCards.Clear();
        
        // Reset bets for the new round
        potSize.Value = 0;
        SetPlayerBet(0, 0); 
        SetPlayerBet(1, 0);
        betsInRound.Clear();

        deckController.ResetAndShuffleDeck();
        ClearCardsClientRpc();
        if (uiController != null) uiController.UpdateWinnerText("");

        // --- Blinds ---
        int sbAmount = 1;
        int bbAmount = 2;
        ulong sbPlayerId = 0;
        ulong bbPlayerId = 1;

        SetPlayerPoints(sbPlayerId, GetPlayerPoints(sbPlayerId) - sbAmount);
        SetPlayerBet(sbPlayerId, sbAmount);

        SetPlayerPoints(bbPlayerId, GetPlayerPoints(bbPlayerId) - bbAmount);
        SetPlayerBet(bbPlayerId, bbAmount);

        potSize.Value = sbAmount + bbAmount;
        currentBet.Value = bbAmount;
        
        // In pre-flop, the action starts left of the big blind. For 2 players, this is the small blind.
        // The betting round ends when action gets back to the big blind who can check or raise.
        actionTargetId = bbPlayerId; 

        playerHand.Clear();
        opponentHand.Clear();
        playerHand.Add(deckController.DealCard());
        playerHand.Add(deckController.DealCard());
        opponentHand.Add(deckController.DealCard());
        opponentHand.Add(deckController.DealCard());
        
        DealHandToClientRpc(playerHand[0], playerHand[1], new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { sbPlayerId } } });
        DealHandToClientRpc(opponentHand[0], opponentHand[1], new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { bbPlayerId } } });
        
        currentGameState.Value = new GameStateData { CurrentState = GameStateData.State.Betting, Round = 0, ActivePlayerId = sbPlayerId };
    }

    private void EndTurn()
    {
        ulong currentPlayerId = currentGameState.Value.ActivePlayerId;
        ulong nextPlayerId = (currentPlayerId == 0) ? 1ul : 0ul; // 次のプレイヤーのIDを直接取得

        bool allBetsEqual = (GetPlayerBet(0) == GetPlayerBet(1) && GetPlayerBet(0) == currentBet.Value); 

        if (allBetsEqual && GetPlayerBet(0) == GetPlayerBet(1)) // 全員が同じ額を賭けているか確認
        {
            if (currentGameState.Value.Round == 0 && GetPlayerBet(0) == GetPlayerBet(1) && GetPlayerBet(0) == currentBet.Value && currentPlayerId == 0 && actionTargetId == 1ul)
            {
                // SBがコール/チェックし、BBにターンが戻る
                // ここでは ProceedToNextDealingState() には行かない。BBがアクションする番
            } else if (currentPlayerId == actionTargetId)
            {
                ProceedToNextDealingState();
                return;
            }
        }
        var state = currentGameState.Value;
        state.ActivePlayerId = nextPlayerId;
        currentGameState.Value = state;
    }

    private void ProceedToNextDealingState()
    {
        var state = currentGameState.Value;
        int nextRound = state.Round + 1;
        
        // Reset bets for the new street
        player0Bet.Value = 0; 
        player1Bet.Value = 0;
        currentBet.Value = 0;
        actionTargetId = 0; // Post-flop, action always starts with player 0, target is player 1
        state.ActivePlayerId = 0;

        if (nextRound <= 3)
        {
            DealCommunityCards(nextRound == 1 ? 3 : 1);
            state.CurrentState = GameStateData.State.Betting;
            state.Round = nextRound;
        }
        else
        {
            state.CurrentState = GameStateData.State.Showdown;
            PerformShowdown();
        }
        currentGameState.Value = state;
    }

    // (Other methods remain largely the same)

    private void DealPlayerHands()
    {
        playerHand.Add(deckController.DealCard());
        playerHand.Add(deckController.DealCard());
        opponentHand.Add(deckController.DealCard());
        opponentHand.Add(deckController.DealCard());
        
        DealHandToClientRpc(playerHand[0], playerHand[1], new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { 0ul } } });
        DealHandToClientRpc(opponentHand[0], opponentHand[1], new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { 1ul } } });
    }

    private void DealCommunityCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Card card = deckController.DealCard();
            communityCards.Add(card);
            DealCommunityCardClientRpc(card);
        }
    }

    private void PerformShowdown()
    {
        RevealOpponentCardsClientRpc(playerHand[0], playerHand[1], new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { 1ul } } });
        RevealOpponentCardsClientRpc(opponentHand[0], opponentHand[1], new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { 0ul } } });
        
        HandRanking playerResult = HandEvaluator.EvaluateHand(playerHand.Concat(communityCards).ToList());
        HandRanking opponentResult = HandEvaluator.EvaluateHand(opponentHand.Concat(communityCards).ToList());
        
        string winnerString = DetermineWinner(playerResult, opponentResult);
        
        UpdateWinnerTextClientRpc("Player: " + playerResult.HandType, "Opponent: " + opponentResult.HandType, winnerString);
        currentGameState.Value = new GameStateData { CurrentState = GameStateData.State.RoundOver, Round = currentGameState.Value.Round };
    }
    
    private string DetermineWinner(HandRanking player, HandRanking opponent)
    {
        int winner = 0;
        if (player.HandType > opponent.HandType) { winner = 1; }
        else if (player.HandType < opponent.HandType) { winner = -1; }
        else {
            for (int i = 0; i < player.PrimaryRanks.Count; i++) {
                if (player.PrimaryRanks[i] > opponent.PrimaryRanks[i]) { winner = 1; break; }
                if (player.PrimaryRanks[i] < opponent.PrimaryRanks[i]) { winner = -1; break; }
            }
            if (winner == 0 && player.Kickers.Count > 0) {
                for (int i = 0; i < player.Kickers.Count; i++) {
                    if (player.Kickers[i] > opponent.Kickers[i]) { winner = 1; break; }
                    if (player.Kickers[i] < opponent.Kickers[i]) { winner = -1; break; }
                }
            }
        }

        if (winner == 1) { playerPoints.Value += potSize.Value; return "Player wins!"; }
        else if (winner == -1) { opponentPoints.Value += potSize.Value; return "Opponent wins!"; }
        else { playerPoints.Value += potSize.Value / 2; opponentPoints.Value += potSize.Value / 2; return "It's a tie!"; }
    }
    
    private int GetPlayerPoints(ulong clientId) => (clientId == 0) ? playerPoints.Value : opponentPoints.Value;
    private void SetPlayerPoints(ulong clientId, int value) { if (clientId == 0) playerPoints.Value = value; else opponentPoints.Value = value; }

    private int GetPlayerBet(ulong clientId) => (clientId == 0) ? player0Bet.Value : player1Bet.Value;
    private void SetPlayerBet(ulong clientId, int value) { if (clientId == 0) player0Bet.Value = value; else player1Bet.Value = value; }
    [ClientRpc]
    private void DealHandToClientRpc(Card c1, Card c2, ClientRpcParams clientRpcParams = default)
    {
        if (uiController == null) return;
        
        Transform playerArea = uiController.GetPlayerHandArea();
        uiController.ClearCardArea(playerArea);
        uiController.CreateCardObject(c1, playerArea, Resources.Load<Sprite>(c1.spriteName));
        uiController.CreateCardObject(c2, playerArea, Resources.Load<Sprite>(c2.spriteName));

        Transform opponentArea = uiController.GetOpponentHandArea();
        uiController.ClearCardArea(opponentArea);
        opponentCardViews.Clear();
        Card dummyCard = new Card();
        CardView cv1 = uiController.CreateCardObject(dummyCard, opponentArea, null);
        CardView cv2 = uiController.CreateCardObject(dummyCard, opponentArea, null);
        if (cv1 != null) { cv1.ShowCardBack(uiController.GetCardBackSprite()); opponentCardViews.Add(cv1); }
        if (cv2 != null) { cv2.ShowCardBack(uiController.GetCardBackSprite()); opponentCardViews.Add(cv2); }
    }

    [ClientRpc]
    private void DealCommunityCardClientRpc(Card card)
    {
        if (uiController == null) return;
        uiController.CreateCardObject(card, uiController.GetCommunityCardArea(), Resources.Load<Sprite>(card.spriteName));
    }

    [ClientRpc]
    private void RevealOpponentCardsClientRpc(Card c1, Card c2, ClientRpcParams clientRpcParams = default)
    {
        if (opponentCardViews.Count >= 2)
        {
            if(opponentCardViews[0] != null) opponentCardViews[0].SetSprite(Resources.Load<Sprite>(c1.spriteName));
            if(opponentCardViews[1] != null) opponentCardViews[1].SetSprite(Resources.Load<Sprite>(c2.spriteName));
        }
    }

    [ClientRpc]
    private void UpdateWinnerTextClientRpc(string pHand, string oHand, string winner) 
    {
        if (uiController != null) uiController.UpdateWinnerText(pHand + "\n" + oHand + "\n\n" + winner);
    }

    [ClientRpc]
    private void AnnounceWinnerClientRpc(string message)
    {
        if (uiController != null) uiController.UpdateWinnerText(message);
    }

    [ClientRpc]
    private void ClearCardsClientRpc()
    {
        if (uiController == null) return;
        uiController.ClearCardArea(uiController.GetPlayerHandArea());
        uiController.ClearCardArea(uiController.GetOpponentHandArea());
        uiController.ClearCardArea(uiController.GetCommunityCardArea());
    }
}
