using System.Collections.Generic;

// everything that goes into save.json.
// JsonUtility can't save a Dictionary, so there are only lists here.
[System.Serializable]
public class SaveData
{
    public int version;
    public int selectedLevel;          // which level the Game scene loads (0 = first level)
    public List<int> unlockedLevels;   // level numbers: 1, 2, 3
    public List<int> bestScores;       // index 0 = level 1. 0 = not passed yet
    public List<int> attempts;         // how many times each level was started

    // settings
    public float soundVolume;          // 0..1
    public bool showLabels;            // state text under the enemies
}
