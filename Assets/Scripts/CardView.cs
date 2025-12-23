using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class CardView : MonoBehaviour
{
    private Image cardImage;
    private Sprite originalSprite; // カードの表向きのスプライトを記憶する

    void Awake()
    {
        // 念のためAwakeでも取得
        if (cardImage == null)
        {
            cardImage = GetComponent<Image>();
        }
    }

    // カードの表向きのスプライトを設定する（そして記憶する）
    public void SetSprite(Sprite sprite)
    {
        // 安全対策：もしcardImageがnullなら、ここで取得する
        if (cardImage == null)
        {
            cardImage = GetComponent<Image>();
        }
        
        originalSprite = sprite; // 表向きのスプライトを記憶
        if (sprite != null)
        {
            cardImage.sprite = sprite;
        }
        else
        {
            Debug.LogWarning("スプライトが見つからなかったため、表示できませんでした。");
        }
    }

    // カードの裏面スプライトを表示する
    public void ShowCardBack(Sprite cardBackSprite)
    {
        if (cardImage == null)
        {
            cardImage = GetComponent<Image>();
        }

        if (cardBackSprite != null)
        {
            cardImage.sprite = cardBackSprite;
        }
        else
        {
            Debug.LogError("カードの裏面スプライトが指定されていません。");
        }
    }

    // カードを裏面から表向きに戻す
    public void RevealCard()
    {
        // 記憶した表向きのスプライトに戻す
        // SetSprite(originalSprite); // SetSpriteを呼ぶとoriginalSpriteが上書きされるので直接代入
        if (cardImage == null)
        {
            cardImage = GetComponent<Image>();
        }
        cardImage.sprite = originalSprite;
    }
}