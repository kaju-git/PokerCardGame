using UnityEngine;
using Unity.Netcode; // INetworkSerializable等を使用するために追加

// 命名規則に合わせてスートの順番を変更
// スペード(0)→クラブ(1)→ダイヤ(2)→ハート(3)
public enum Suit
{
    Spade,
    Club,
    Diamond,
    Heart
}

public enum Rank
{
    Ace = 1,
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13
}

// カード一枚を表すデータ構造
public class Card : INetworkSerializable, System.IEquatable<Card>
{
    public Suit suit;
    public Rank rank;
    public string spriteName;

    public Card(Suit s, Rank r, string sprite)
    {
        suit = s;
        rank = r;
        spriteName = sprite;
    }

    // デフォルトコンストラクタ (INetworkSerializableに必要)
    public Card()
    {
        suit = Suit.Spade;
        rank = Rank.Ace;
        spriteName = "";
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref suit);
        serializer.SerializeValue(ref rank);
        serializer.SerializeValue(ref spriteName);
    }

    public bool Equals(Card other)
    {
        return suit == other.suit && rank == other.rank;
    }

    public override int GetHashCode()
    {
        return suit.GetHashCode() ^ rank.GetHashCode();
    }
}
