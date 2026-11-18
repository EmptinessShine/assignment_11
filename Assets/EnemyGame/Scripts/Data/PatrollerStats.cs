using UnityEngine;

[CreateAssetMenu(menuName = "Enemy Game/Patroller Stats")]
public class PatrollerStats : EnemyStats
{
    [Header("Patroller")]
    public float chaseSpeed;
    public float attackRange;      // starts the wind-up at this distance
    public float hitRange;         // the hit lands if the player is still closer than this
    public float windUpTime;
    public float cooldownTime;
    public float searchTime;
}
