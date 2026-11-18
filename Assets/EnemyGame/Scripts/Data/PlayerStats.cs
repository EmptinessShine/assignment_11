using UnityEngine;

// all balance numbers of the player. change them in PlayerStats.asset, not in the code.
[CreateAssetMenu(menuName = "Enemy Game/Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("Movement")]
    public float moveSpeed;

    [Header("Health")]
    public int maxHealth;
    public float invincibleTime;   // after a hit the player can't be hit again for this long

    [Header("Attack")]
    public float attackRadius;
    public int attackDamage;
    public float attackCooldown;
    public int backstabDamage;     // hit from behind
}
