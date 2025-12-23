using System.Collections.Generic;
using UnityEngine;

public class DeckController : MonoBehaviour
{
    private List<Card> deck = new List<Card>();
    private bool isInitialized = false;

    void Awake()
    {
        InitializeDeck();
    }
    
    private void InitializeDeck()
    {
        if (isInitialized) return;
        CreateDeck();
        isInitialized = true;
    }

    private void CreateDeck()
    {
        deck.Clear();
        foreach (Suit s in System.Enum.GetValues(typeof(Suit)))
        {
            foreach (Rank r in System.Enum.GetValues(typeof(Rank)))
            {
                int spriteNumber = ((int)s * 13) + (int)r;
                string spritePath = "Sprites/Cards/torannpu-illust" + spriteNumber;
                deck.Add(new Card(s, r, spritePath));
            }
        }
    }

    public void ShuffleDeck()
    {
        if (!isInitialized) InitializeDeck();
        
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            Card temp = deck[i];
            deck[i] = deck[j];
            deck[j] = temp;
        }
    }

    // 新しく追加するメソッド：デッキを完全にリセットしてシャッフルする
    public void ResetAndShuffleDeck()
    {
        CreateDeck(); // 52枚のカードでデッキを再構築
        ShuffleDeck(); // それをシャッフルする
    }

    public Card DealCard()
    {
        if (!isInitialized) InitializeDeck();
        
        if (deck.Count == 0)
        {
            Debug.LogWarning("Deck is empty! Resetting and shuffling.");
            ResetAndShuffleDeck(); // デッキが空になったらリセット＆シャッフル
        }

        Card cardToDeal = deck[0];
        deck.RemoveAt(0);
        return cardToDeal;
    }

    public int GetDeckCount()
    {
        return deck.Count;
    }
}
