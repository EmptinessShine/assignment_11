using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// main menu: level select, settings, reset progress.
// all panels are already in the Menu scene, this script only turns them on and off.
public class MainMenu : MonoBehaviour
{
    [SerializeField] LevelList levelList;

    [Header("Panels")]
    [SerializeField] GameObject mainPanel;
    [SerializeField] GameObject levelsPanel;
    [SerializeField] GameObject settingsPanel;
    [SerializeField] GameObject confirmPanel;

    [Header("Levels")]
    [SerializeField] LevelButton[] levelButtons;

    [Header("Settings")]
    [SerializeField] Slider volumeSlider;
    [SerializeField] Toggle labelsToggle;

    [SerializeField] Text infoText;

    void Start()
    {
        SaveData data = SaveSystem.Data; // the first call loads save.json
        infoText.text = SaveSystem.loadMessage;

        // WithoutNotify: just show the saved values, don't call OnVolumeChanged
        volumeSlider.SetValueWithoutNotify(data.soundVolume);
        labelsToggle.SetIsOnWithoutNotify(data.showLabels);

        ShowMain();
    }

    public void ShowMain()
    {
        mainPanel.SetActive(true);
        levelsPanel.SetActive(false);
        settingsPanel.SetActive(false);
        confirmPanel.SetActive(false);
    }

    public void ShowLevels()
    {
        mainPanel.SetActive(false);
        levelsPanel.SetActive(true);

        // locked / unlocked and records come from the save
        for (int i = 0; i < levelButtons.Length; i++)
            levelButtons[i].Refresh(levelList.levels[i]);
    }

    public void ShowSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void ShowConfirmReset()
    {
        confirmPanel.SetActive(true);
    }

    public void HideConfirmReset()
    {
        confirmPanel.SetActive(false);
    }

    public void ConfirmReset()
    {
        SaveSystem.ResetProgress();
        infoText.text = "Прогресс сброшен";
        confirmPanel.SetActive(false);
    }

    public void PlayLevel(int levelIndex)
    {
        SaveSystem.Data.selectedLevel = levelIndex;
        SaveSystem.Save();
        SceneManager.LoadScene("Game");
    }

    public void OnVolumeChanged(float value)
    {
        SaveSystem.Data.soundVolume = value;
        AudioListener.volume = value;
        SaveSystem.Save();
    }

    public void OnLabelsChanged(bool isOn)
    {
        SaveSystem.Data.showLabels = isOn;
        SaveSystem.Save();
    }

    void OnApplicationPause(bool paused)
    {
        if (paused) SaveSystem.Save();
    }
}
