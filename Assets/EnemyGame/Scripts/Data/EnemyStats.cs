using UnityEngine;

// numbers that every enemy has. each enemy type has its own class with extra numbers.
public class EnemyStats : ScriptableObject
{
    [Header("Common")]
    public int maxHealth;
    public float invincibleTime;
    public float moveSpeed;        // walking on patrol
    public int damage;
    public float size;             // 1 = normal size, the boss is bigger
    public int scoreValue;         // points for killing this enemy

    [Header("Vision")]
    public float viewDistance;
    public float viewAngle;
    public float lookAroundSpeed;  // degrees per second while searching
    public float backstabAngle;    // the player is "behind" if the angle is bigger than this
}
