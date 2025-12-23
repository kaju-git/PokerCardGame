using System.Collections.Generic;
using System.Linq;

// 役の種類を強さの順で定義します
public enum HandType
{
    HighCard,       // ハイカード
    OnePair,        // ワンペア
    TwoPair,        // ツーペア
    ThreeOfAKind,   // スリーカード
    Straight,       // ストレート
    Flush,          // フラッシュ
    FullHouse,      // フルハウス
    FourOfAKind,    // フォーカード
    StraightFlush,  // ストレートフラッシュ
    RoyalFlush      // ロイヤルストレートフラッシュ
}

// 判定結果を格納するクラス
public class HandRanking
{
    // 役の種類
    public HandType HandType { get; set; }
    
    // 役を構成する主要ランク（例:フルハウスなら3枚組とペアのランク）
    // 強い順に格納する
    public List<Rank> PrimaryRanks { get; set; }
    
    // キッカー（役に関わらないカードで勝敗を決めるもの）
    // 強い順に格納する
    public List<Rank> Kickers { get; set; }

    public HandRanking(HandType type, List<Rank> primaryRanks, List<Rank> kickers)
    {
        this.HandType = type;
        this.PrimaryRanks = primaryRanks;
        this.Kickers = kickers;
    }

    // デバッグ用に詳細な情報を返す
    public override string ToString()
    {
        string primary = PrimaryRanks != null ? string.Join(", ", PrimaryRanks) : "N/A";
        string kickers = Kickers != null ? string.Join(", ", Kickers) : "N/A";
        return $"Hand: {HandType}, Primary Ranks: [{primary}], Kickers: [{kickers}]";
    }
}