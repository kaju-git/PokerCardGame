using System.Collections.Generic;
using System.Linq;

public static class HandEvaluator
{
    public static HandRanking EvaluateHand(List<Card> sevenCards)
    {
        var rankCounts = sevenCards.GroupBy(c => c.rank).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Key).ToDictionary(g => g.Key, g => g.Count());
        var suitCounts = sevenCards.GroupBy(c => c.suit).ToDictionary(g => g.Key, g => g.Count());

        var flushSuit = suitCounts.Where(sc => sc.Value >= 5).Select(sc => (Suit?)sc.Key).FirstOrDefault();
        var straightResult = CheckForStraight(sevenCards);

        // --- ストレートフラッシュ ---
        if (straightResult.isStraight && flushSuit.HasValue)
        {
            var flushCards = sevenCards.Where(c => c.suit == flushSuit.Value).ToList();
            var straightFlushResult = CheckForStraight(flushCards);
            if (straightFlushResult.isStraight)
            {
                var handType = straightFlushResult.highestRank == Rank.Ace ? HandType.RoyalFlush : HandType.StraightFlush;
                return new HandRanking(handType, new List<Rank> { straightFlushResult.highestRank }, new List<Rank>());
            }
        }

        // --- フォーカード ---
        if (rankCounts.Any(rc => rc.Value == 4))
        {
            var primary = rankCounts.Where(rc => rc.Value == 4).Select(rc => rc.Key).ToList();
            var kickers = GetKickers(sevenCards, primary, 1);
            return new HandRanking(HandType.FourOfAKind, primary, kickers);
        }

        // --- フルハウス ---
        var threes = rankCounts.Where(rc => rc.Value == 3).Select(rc => rc.Key).ToList();
        var pairs = rankCounts.Where(rc => rc.Value == 2).Select(rc => rc.Key).ToList();
        if (threes.Count >= 1 && pairs.Count >= 1)
        {
            var primary = new List<Rank> { threes[0], pairs[0] };
            return new HandRanking(HandType.FullHouse, primary, new List<Rank>());
        }
        if (threes.Count >= 2) // 7枚の中に3枚組が2つある場合
        {
            var primary = new List<Rank> { threes[0], threes[1] };
            return new HandRanking(HandType.FullHouse, primary, new List<Rank>());
        }

        // --- フラッシュ ---
        if (flushSuit.HasValue)
        {
            var flushCards = sevenCards.Where(c => c.suit == flushSuit.Value).OrderByDescending(c => c.rank).Take(5).Select(c => c.rank).ToList();
            return new HandRanking(HandType.Flush, flushCards, new List<Rank>());
        }

        // --- ストレート ---
        if (straightResult.isStraight)
        {
            return new HandRanking(HandType.Straight, new List<Rank> { straightResult.highestRank }, new List<Rank>());
        }

        // --- スリーカード ---
        if (threes.Count == 1)
        {
            var primary = threes;
            var kickers = GetKickers(sevenCards, primary, 2);
            return new HandRanking(HandType.ThreeOfAKind, primary, kickers);
        }

        // --- ツーペア ---
        if (pairs.Count >= 2)
        {
            var primary = pairs.Take(2).ToList();
            var kickers = GetKickers(sevenCards, primary, 1);
            return new HandRanking(HandType.TwoPair, primary, kickers);
        }

        // --- ワンペア ---
        if (pairs.Count == 1)
        {
            var primary = pairs;
            var kickers = GetKickers(sevenCards, primary, 3);
            return new HandRanking(HandType.OnePair, primary, kickers);
        }

        // --- ハイカード ---
        var highCardKickers = GetKickers(sevenCards, new List<Rank>(), 5);
        return new HandRanking(HandType.HighCard, new List<Rank>(), highCardKickers);
    }
    
    private static (bool isStraight, Rank highestRank) CheckForStraight(List<Card> cards)
    {
        var uniqueRanks = cards.Select(c => c.rank).Distinct().OrderByDescending(r => r).ToList();
        if (uniqueRanks.Count < 5) return (false, Rank.Two);

        // A-5 (Wheel) ストレート
        bool isWheel = uniqueRanks.Contains(Rank.Ace) && uniqueRanks.Contains(Rank.Five) && uniqueRanks.Contains(Rank.Four) && uniqueRanks.Contains(Rank.Three) && uniqueRanks.Contains(Rank.Two);
        if (isWheel) return (true, Rank.Five);
        
        // 通常のストレート
        for (int i = 0; i <= uniqueRanks.Count - 5; i++)
        {
            if ((int)uniqueRanks[i] - (int)uniqueRanks[i + 4] == 4)
            {
                return (true, uniqueRanks[i]);
            }
        }
        
        return (false, Rank.Two);
    }

    private static List<Rank> GetKickers(List<Card> allCards, List<Rank> primaryRanks, int count)
    {
        return allCards.Where(c => !primaryRanks.Contains(c.rank))
                       .OrderByDescending(c => c.rank)
                       .Select(c => c.rank)
                       .Take(count)
                       .ToList();
    }
}

