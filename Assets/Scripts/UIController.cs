using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;
using System;

public class UIController : MonoBehaviour
{
    private RectTransform lobbyPanel;
    private RectTransform gamePanel;
    private Button createLobbyButton;
    private TextMeshProUGUI statusText;
    private RectTransform lobbyListScrollView;
    private Transform lobbyListContent;
    [SerializeField] private GameObject lobbyButtonPrefab;
    private GameObject cardPrefab;
    private TextMeshProUGUI playerPointsText;
    private TextMeshProUGUI opponentPointsText;
    private TextMeshProUGUI potText;
    private TextMeshProUGUI winnerText;
    private RectTransform actionPanel;
    private Button checkButton;
    private Button betButton;
    private Button foldButton;
    private TextMeshProUGUI checkButtonText;
    private Transform playerHandArea;
    private Transform opponentHandArea;
    private Transform communityCardArea;
    private Sprite cardBackSprite;

    // --- New Bet UI References ---
    private Button bet2Button, bet3Button, bet4Button, bet5Button;
    private TextMeshProUGUI playerBetText, opponentBetText;
    
    private void Awake()
    {
        AutoAssignReferences();
        AdjustUILayout();
    }

    private void AutoAssignReferences()
    {
        lobbyPanel = transform.Find("LobbyPanel")?.GetComponent<RectTransform>();
        gamePanel = transform.Find("GamePanel")?.GetComponent<RectTransform>();
        cardPrefab = Resources.Load<GameObject>("Prefabs/CardPrefab");
        cardBackSprite = Resources.Load<Sprite>("Sprites/Cards/haikei");

        if (lobbyPanel != null)
        {
            createLobbyButton = lobbyPanel.transform.Find("CreateLobbyButton")?.GetComponent<Button>();
            statusText = lobbyPanel.transform.Find("StatusText")?.GetComponent<TextMeshProUGUI>();
            lobbyListScrollView = lobbyPanel.transform.Find("LobbyListScrollView")?.GetComponent<RectTransform>();
            if (lobbyListScrollView != null)
                lobbyListContent = lobbyListScrollView.transform.Find("Viewport/Content");
        }
        
        if (gamePanel != null)
        {
            bool wasGamePanelActive = gamePanel.gameObject.activeSelf;
            gamePanel.gameObject.SetActive(true);
            playerPointsText = gamePanel.transform.Find("PlayerPointsText")?.GetComponent<TextMeshProUGUI>();
            opponentPointsText = gamePanel.transform.Find("OpponentPointsText")?.GetComponent<TextMeshProUGUI>();
            potText = gamePanel.transform.Find("PotText")?.GetComponent<TextMeshProUGUI>();
            winnerText = gamePanel.transform.Find("WinnerText")?.GetComponent<TextMeshProUGUI>();
            actionPanel = gamePanel.transform.Find("ActionPanel")?.GetComponent<RectTransform>();
            playerHandArea = gamePanel.transform.Find("PlayerHandArea");
            opponentHandArea = gamePanel.transform.Find("OpponentHandArea");
            communityCardArea = gamePanel.transform.Find("CommunityCardArea");
            playerBetText = gamePanel.transform.Find("PlayerBetText")?.GetComponent<TextMeshProUGUI>();
            opponentBetText = gamePanel.transform.Find("OpponentBetText")?.GetComponent<TextMeshProUGUI>();
            
            if (actionPanel != null)
            {
                checkButton = actionPanel.transform.Find("CheckButton")?.GetComponent<Button>();
                betButton = actionPanel.transform.Find("BetButton")?.GetComponent<Button>();
                foldButton = actionPanel.transform.Find("FoldButton")?.GetComponent<Button>();
                if(checkButton != null)
                    checkButtonText = checkButton.GetComponentInChildren<TextMeshProUGUI>();

                // Find new bet buttons
                bet2Button = actionPanel.transform.Find("Bet2Button")?.GetComponent<Button>();
                bet3Button = actionPanel.transform.Find("Bet3Button")?.GetComponent<Button>();
                bet4Button = actionPanel.transform.Find("Bet4Button")?.GetComponent<Button>();
                bet5Button = actionPanel.transform.Find("Bet5Button")?.GetComponent<Button>();
            }
            gamePanel.gameObject.SetActive(wasGamePanelActive);
        }
    }

    public void AdjustUILayout()
    {
        if (lobbyPanel != null) lobbyPanel.gameObject.SetActive(true);
        if (gamePanel != null) gamePanel.gameObject.SetActive(true);

        StretchToFullScreen(lobbyPanel);
        StretchToFullScreen(gamePanel);

        if (statusText != null) {
            statusText.rectTransform.localScale = Vector3.one;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.fontSize = 42;
            SetAnchorsAndPosition(statusText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -100));
            statusText.rectTransform.sizeDelta = new Vector2(1200, 60);
        }
        if (createLobbyButton != null) {
            createLobbyButton.transform.localScale = Vector3.one;
            SetAnchorsAndPosition(createLobbyButton.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180));
            createLobbyButton.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 60);
        }
        if (lobbyListScrollView != null) {
            lobbyListScrollView.localScale = Vector3.one;
            lobbyListScrollView.anchorMin = new Vector2(0, 0);
            lobbyListScrollView.anchorMax = new Vector2(1, 1);
            lobbyListScrollView.offsetMin = new Vector2(200, 100);
            lobbyListScrollView.offsetMax = new Vector2(-200, -250);
        }
        
        // GamePanel elements are now handled by anchors and Layout Groups set in the Editor.
        // We no longer adjust them via script.

        ShowLobbyUI();
    }

    #region UI State & Control Methods
    public void ShowLobbyUI()
    {
        if (lobbyPanel != null) lobbyPanel.gameObject.SetActive(true);
        if (gamePanel != null) gamePanel.gameObject.SetActive(false);
    }
    public void ShowGameUI()
    {
        if (lobbyPanel != null) lobbyPanel.gameObject.SetActive(false);
        if (gamePanel != null) gamePanel.gameObject.SetActive(true);
    }
    public void SetLobbyButtonsInteractable(bool interactable)
    {
        if(createLobbyButton != null) createLobbyButton.interactable = interactable;
        if (lobbyListContent == null) return;
        foreach (Transform child in lobbyListContent)
            child.GetComponent<Button>().interactable = interactable;
    }
    public void UpdateStatusText(string message)
    {
        if(statusText != null) statusText.text = message;
    }
    public void UpdateLobbyList(List<Lobby> lobbies, Action<string> joinLobbyAction)
    {
        if (lobbyListContent == null || lobbyButtonPrefab == null) return;
        foreach (Transform child in lobbyListContent) Destroy(child.gameObject);
        foreach (Lobby lobby in lobbies)
        {
            GameObject buttonObj = Instantiate(lobbyButtonPrefab, lobbyListContent);
            buttonObj.GetComponentInChildren<TextMeshProUGUI>().text = lobby.Name;
            buttonObj.GetComponent<Button>().onClick.AddListener(() => joinLobbyAction(lobby.Id));
        }
    }
    public void AddCreateLobbyListener(UnityEngine.Events.UnityAction action)
    {
        if (createLobbyButton != null)
        {
            createLobbyButton.onClick.RemoveAllListeners();
            createLobbyButton.onClick.AddListener(action);
        }
    }
    public string GetWinnerText()
    {
        if (winnerText != null) return winnerText.text;
        return string.Empty;
    }
    public void AddGameActionListeners(UnityEngine.Events.UnityAction checkCallAction, Action<int> betAction, UnityEngine.Events.UnityAction foldAction)
    {
        if (checkButton != null)
        {
            checkButton.onClick.RemoveAllListeners();
            checkButton.onClick.AddListener(checkCallAction);
        }
        if (foldButton != null)
        {
            foldButton.onClick.RemoveAllListeners();
            foldButton.onClick.AddListener(foldAction);
        }
        // Hook up the new bet buttons
        if (bet2Button != null) { bet2Button.onClick.RemoveAllListeners(); bet2Button.onClick.AddListener(() => betAction(2)); }
        if (bet3Button != null) { bet3Button.onClick.RemoveAllListeners(); bet3Button.onClick.AddListener(() => betAction(3)); }
        if (bet4Button != null) { bet4Button.onClick.RemoveAllListeners(); bet4Button.onClick.AddListener(() => betAction(4)); }
        if (bet5Button != null) { bet5Button.onClick.RemoveAllListeners(); bet5Button.onClick.AddListener(() => betAction(5)); }
    }
    public void UpdatePointsDisplay(int player, int opponent, int pot)
    {
        if(playerPointsText != null) playerPointsText.text = "Player: " + player + "pt";
        if(opponentPointsText != null) opponentPointsText.text = "Opponent: " + opponent + "pt";
        if(potText != null) potText.text = "Pot: " + pot + "pt";
    }
    public void UpdateWinnerText(string text)
    {
        if(winnerText != null) winnerText.text = text;
    }

    public void UpdateBetDisplay(int player0Bet, int player1Bet, ulong localPlayerId)
    {
        int myBet = localPlayerId == 0 ? player0Bet : player1Bet;
        int opponentBet = localPlayerId == 0 ? player1Bet : player0Bet;

        if (playerBetText != null) playerBetText.text = "My Bet: " + myBet;
        if (opponentBetText != null) opponentBetText.text = "Opponent Bet: " + opponentBet;
    }

    public void UpdateActionPanel(bool show, bool isMyTurn, int myPoints, int myCurrentBet, int roundCurrentBet)
    {
        if (actionPanel == null) return;
        actionPanel.gameObject.SetActive(show && isMyTurn);

        if (isMyTurn)
        {
            if (checkButtonText != null)
                checkButtonText.text = (roundCurrentBet > myCurrentBet) ? "Call" : "Check";
            
            // Set interactable state for each bet button based on game rules
            if (bet2Button != null) bet2Button.interactable = 2 > roundCurrentBet && myPoints >= (2 - myCurrentBet);
            if (bet3Button != null) bet3Button.interactable = 3 > roundCurrentBet && myPoints >= (3 - myCurrentBet);
            if (bet4Button != null) bet4Button.interactable = 4 > roundCurrentBet && myPoints >= (4 - myCurrentBet);
            if (bet5Button != null) bet5Button.interactable = 5 > roundCurrentBet && myPoints >= (5 - myCurrentBet);
        }
    }
    public void ClearCardArea(Transform area) 
    {
        if (area == null) return;
        foreach (Transform child in area) Destroy(child.gameObject);
    }
    public Transform GetPlayerHandArea() => playerHandArea;
    public Transform GetOpponentHandArea() => opponentHandArea;
    public Transform GetCommunityCardArea() => communityCardArea;

    public CardView CreateCardObject(Card card, Transform area, Sprite cardFaceSprite)
    {
        if (cardPrefab == null) { Debug.LogError("Card prefab is null."); return null; }
        if (area == null) { Debug.LogError("Target area is null."); return null; }
        
        GameObject cardObj = Instantiate(cardPrefab);
        cardObj.name = card.rank + " of " + card.suit;
        cardObj.transform.SetParent(area, false);

        CardView cardView = cardObj.GetComponent<CardView>();
        if (cardView != null)
        {
            cardView.SetSprite(cardFaceSprite);
        }
        
        LayoutElement layoutElement = cardObj.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = cardObj.AddComponent<LayoutElement>();
        }
        layoutElement.preferredWidth = 140;
        layoutElement.preferredHeight = 200;

        return cardView;
    }

    public Sprite GetCardBackSprite() => cardBackSprite;
    #endregion

    #region Helpers
    private void StretchToFullScreen(RectTransform targetRect)
    {
        if (targetRect == null) return;
        targetRect.localScale = Vector3.one;
        targetRect.anchorMin = Vector2.zero;
        targetRect.anchorMax = Vector2.one;
        targetRect.offsetMin = Vector2.zero;
        targetRect.offsetMax = Vector2.zero;
    }
    private void SetAnchorsAndPosition(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition)
    {
        if (rect == null) return;
        rect.localScale = Vector3.one;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
    }
    #endregion
}