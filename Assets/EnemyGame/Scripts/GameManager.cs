using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// the Game scene is the same for every level.
// GameManager takes the LevelData of the chosen level and builds the level from it,
// counts time and score and saves the progress.
public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Data")]
    [SerializeField] LevelList levelList;

    [Header("Enemy prefabs")]
    [SerializeField] GameObject patrollerPrefab;
    [SerializeField] GameObject shooterPrefab;
    [SerializeField] GameObject chargerPrefab;

    [SerializeField] float restartDelay;

    [Header("UI")]
    [SerializeField] Text levelText;
    [SerializeField] Text goalText;
    [SerializeField] Text triesText;
    [SerializeField] Text hpText;
    [SerializeField] Text timerText;
    [SerializeField] Text messageText;
    [SerializeField] GameObject winPanel;
    [SerializeField] Text winText;
    [SerializeField] GameObject nextButton;

    [Header("Sounds")]
    [SerializeField] AudioClip winSound;
    [SerializeField] AudioClip loseSound;

    public bool levelFinished;

    LevelData level;
    int levelIndex;
    float timeLeft;
    int enemiesLeft;
    int killScore;

    void Awake()
    {
        instance = this;
        Application.targetFrameRate = 60;
    }

    void Start()
    {
        // which level to play is stored in the save
        levelIndex = SaveSystem.Data.selectedLevel;
        if (levelIndex >= levelList.levels.Length || !SaveSystem.IsUnlocked(levelIndex + 1))
            levelIndex = 0;
        level = levelList.levels[levelIndex];

        Instantiate(level.layout);
        PlayerController.instance.transform.position = level.playerStart;
        CameraFollow.instance.SnapToTarget();
        AlertSystem.instance.Setup(level.alertRadius, level.helpersCount);

        for (int i = 0; i < level.enemies.Length; i++)
            SpawnEnemy(level.enemies[i]);
        enemiesLeft = level.enemies.Length;

        timeLeft = level.timeLimit;
        int tries = SaveSystem.AddAttempt(levelIndex);

        levelText.text = level.levelName;
        triesText.text = "Попытка " + tries;
        messageText.text = "";
        winPanel.SetActive(false);
        UpdateGoalText();
    }

    void SpawnEnemy(EnemySpawn spawn)
    {
        GameObject prefab = null;
        switch (spawn.type)
        {
            case EnemyType.Patroller: prefab = patrollerPrefab; break;
            case EnemyType.Shooter: prefab = shooterPrefab; break;
            case EnemyType.Charger: prefab = chargerPrefab; break;
        }

        GameObject enemy = Instantiate(prefab, spawn.position, Quaternion.identity);
        enemy.GetComponent<EnemyBase>().Setup(spawn);
    }

    void Update()
    {
        Health hp = PlayerController.instance.health;
        hpText.text = "Жизни " + hp.currentHealth + "/" + hp.maxHealth;

        if (levelFinished) return;

        timeLeft -= Time.deltaTime;
        if (timeLeft <= 0)
        {
            timeLeft = 0;
            LevelFailed("Время вышло");
        }
        timerText.text = Mathf.CeilToInt(timeLeft) + " с";
    }

    void UpdateGoalText()
    {
        if (level.goal == LevelGoal.ReachExit)
            goalText.text = "Цель: дойти до выхода";
        else
            goalText.text = "Цель: победить всех. Осталось " + enemiesLeft;
    }

    public void EnemyKilled(int points)
    {
        if (levelFinished) return;

        killScore += points;
        enemiesLeft--;
        UpdateGoalText();

        if (level.goal == LevelGoal.KillAll && enemiesLeft <= 0)
            LevelWon();
    }

    public void PlayerReachedExit()
    {
        if (level.goal == LevelGoal.ReachExit) LevelWon();
    }

    public void PlayerDied()
    {
        LevelFailed("Ты погиб");
    }

    void LevelFailed(string message)
    {
        if (levelFinished) return;
        levelFinished = true;
        Sound.Play(loseSound);
        messageText.text = message;
        SaveSystem.Save(); // the attempt counter is saved
        Invoke("Restart", restartDelay);
    }

    void LevelWon()
    {
        if (levelFinished) return;
        levelFinished = true;
        Sound.Play(winSound);

        int timeScore = Mathf.FloorToInt(timeLeft) * level.pointsPerSecondLeft;
        int score = killScore + timeScore;
        bool newRecord = SaveSystem.SetBestScore(levelIndex, score);

        bool hasNext = levelIndex + 1 < levelList.levels.Length;
        if (hasNext) SaveSystem.Unlock(levelIndex + 2); // level numbers start from 1
        SaveSystem.Save();

        winPanel.SetActive(true);
        nextButton.SetActive(hasNext);

        string text = "Уровень пройден\n\n";
        text += "За врагов: " + killScore + "\n";
        text += "За время: " + timeScore + "\n";
        text += "Очки: " + score + "\n";
        if (newRecord) text += "Новый рекорд!";
        else text += "Рекорд: " + SaveSystem.GetBestScore(levelIndex);
        winText.text = text;
    }

    public void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadNext()
    {
        SaveSystem.Data.selectedLevel = levelIndex + 1;
        SaveSystem.Save();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void LoadMenu()
    {
        SaveSystem.Save();
        SceneManager.LoadScene("Menu");
    }

    // on a phone this is called when the app goes to the background.
    // after that Android can close the app without asking, so we save here.
    void OnApplicationPause(bool paused)
    {
        if (paused) SaveSystem.Save();
    }

    void OnApplicationQuit()
    {
        SaveSystem.Save();
    }
}
