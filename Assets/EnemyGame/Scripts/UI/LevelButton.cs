using UnityEngine;
using UnityEngine.UI;

// one button in the level select. shows if the level is open and the best score.
public class LevelButton : MonoBehaviour
{
    [SerializeField] int levelIndex; // 0 = first level
    [SerializeField] Button button;
    [SerializeField] Text nameText;
    [SerializeField] Text infoText;

    public void Refresh(LevelData level)
    {
        nameText.text = level.levelName;

        bool unlocked = SaveSystem.IsUnlocked(levelIndex + 1);
        button.interactable = unlocked;

        if (!unlocked)
        {
            infoText.text = "Закрыт";
            return;
        }

        int best = SaveSystem.GetBestScore(levelIndex);
        if (best > 0)
            infoText.text = "Рекорд: " + best;
        else
            infoText.text = "Не пройден";
    }
}
