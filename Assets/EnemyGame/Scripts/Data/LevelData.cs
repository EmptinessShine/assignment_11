using UnityEngine;

public enum EnemyType { Patroller, Shooter, Charger }
public enum LevelGoal { ReachExit, KillAll }

// one enemy in a level
[System.Serializable]
public class EnemySpawn
{
    public EnemyType type;
    public EnemyStats statsOverride;   // empty = stats from the prefab. the boss uses its own asset here.
                                       // must be the same kind: ChargerStats for a charger and so on
    public Vector2 position;
    public Vector2 lookDirection;
    public Vector2[] route;            // patrol points, can be empty
}

// one level. the Game scene reads this asset and builds the level from it.
[CreateAssetMenu(menuName = "Enemy Game/Level Data")]
public class LevelData : ScriptableObject
{
    public string levelName;
    public GameObject layout;          // prefab with floor, walls, boxes, medkits and the exit
    public Vector2 playerStart;
    public LevelGoal goal;
    public float timeLimit;            // seconds
    public EnemySpawn[] enemies;

    [Header("Alarm")]
    public float alertRadius;          // calm enemies closer than this come to help
    public int helpersCount;           // how many of them come

    [Header("Score")]
    public int pointsPerSecondLeft;
}
