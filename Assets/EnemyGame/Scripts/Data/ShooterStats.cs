using UnityEngine;

[CreateAssetMenu(menuName = "Enemy Game/Shooter Stats")]
public class ShooterStats : EnemyStats
{
    [Header("Shooter")]
    public float scanSpeed;        // degrees per second while guarding
    public float scanTime;         // turns one way for this long, then the other way
    public float aimTime;          // red laser before the shot
    public float aimLockTime;      // last part of aiming: stops following the player
    public float cooldownTime;
    public float bulletSpeed;
    public float bulletLifetime;
    public float investigateTime;
}
