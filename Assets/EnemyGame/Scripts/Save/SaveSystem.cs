using System.Collections.Generic;
using System.IO;
using UnityEngine;

// reads and writes save.json in Application.persistentDataPath.
// if the file is missing or broken, the game starts with default values and doesn't crash.
public static class SaveSystem
{
    const int CurrentVersion = 1;
    const string FileName = "save.json";

    // message for the menu, for example when the file was broken
    public static string loadMessage = "";

    static SaveData data;

    // the first time someone asks for the data, the file is loaded
    public static SaveData Data
    {
        get
        {
            if (data == null) Load();
            return data;
        }
    }

    // in the Unity editor static fields can stay from the last Play
    // (when "Enter Play Mode Options" skip the domain reload).
    // this clears them, so every Play reads save.json again like a real app start.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        data = null;
        loadMessage = "";
    }

    static string FilePath()
    {
        return Path.Combine(Application.persistentDataPath, FileName);
    }

    static SaveData CreateDefault()
    {
        SaveData d = new SaveData();
        d.version = CurrentVersion;
        d.selectedLevel = 0;
        d.unlockedLevels = new List<int>();
        d.unlockedLevels.Add(1);
        d.bestScores = new List<int>();
        d.attempts = new List<int>();
        d.soundVolume = 1f;
        d.showLabels = true;
        return d;
    }

    public static void Load()
    {
        string path = FilePath();
        data = CreateDefault();
        loadMessage = "";

        if (!File.Exists(path))
        {
            Debug.Log("No save file yet, using defaults. Path: " + path);
        }
        else
        {
            try
            {
                string json = File.ReadAllText(path);
                if (json.Trim() == "")
                    throw new System.Exception("file is empty");

                // fields that are missing in the file keep the default values
                SaveData loaded = CreateDefault();
                JsonUtility.FromJsonOverwrite(json, loaded);

                if (loaded.version != CurrentVersion)
                    throw new System.Exception("unknown version " + loaded.version);

                data = loaded;
                Debug.Log("Save loaded: " + path);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Save file is broken, using defaults. " + e.Message);
                data = CreateDefault();
                loadMessage = "Файл сохранения повреждён, прогресс начат заново";
            }
        }

        FixValues();
        AudioListener.volume = data.soundVolume;
    }

    // the file can be valid JSON but with strange values inside
    static void FixValues()
    {
        if (data.unlockedLevels == null) data.unlockedLevels = new List<int>();
        if (data.bestScores == null) data.bestScores = new List<int>();
        if (data.attempts == null) data.attempts = new List<int>();
        if (!data.unlockedLevels.Contains(1)) data.unlockedLevels.Add(1);
        if (data.selectedLevel < 0) data.selectedLevel = 0;
        data.soundVolume = Mathf.Clamp01(data.soundVolume);
    }

    public static void Save()
    {
        if (data == null) return;

        string path = FilePath();
        string tempPath = path + ".tmp";
        try
        {
            // write to a temp file first, so the real file is never half-written
            File.WriteAllText(tempPath, JsonUtility.ToJson(data, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tempPath, path);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("Could not save: " + e.Message);
        }
    }

    // progress is reset, settings stay
    public static void ResetProgress()
    {
        SaveData fresh = CreateDefault();
        fresh.soundVolume = Data.soundVolume;
        fresh.showLabels = Data.showLabels;
        data = fresh;
        Save();
    }

    // levelNumber starts from 1
    public static bool IsUnlocked(int levelNumber)
    {
        return Data.unlockedLevels.Contains(levelNumber);
    }

    public static void Unlock(int levelNumber)
    {
        if (!IsUnlocked(levelNumber)) Data.unlockedLevels.Add(levelNumber);
    }

    // levelIndex starts from 0
    public static int GetBestScore(int levelIndex)
    {
        if (levelIndex >= Data.bestScores.Count) return 0;
        return Data.bestScores[levelIndex];
    }

    // returns true if it is a new record
    public static bool SetBestScore(int levelIndex, int score)
    {
        while (Data.bestScores.Count <= levelIndex) Data.bestScores.Add(0);

        if (score <= Data.bestScores[levelIndex]) return false;
        Data.bestScores[levelIndex] = score;
        return true;
    }

    // adds one attempt and returns the new number
    public static int AddAttempt(int levelIndex)
    {
        while (Data.attempts.Count <= levelIndex) Data.attempts.Add(0);

        Data.attempts[levelIndex]++;
        return Data.attempts[levelIndex];
    }
}
