using UnityEngine;

[CreateAssetMenu(menuName = "Enemy Game/Charger Stats")]
public class ChargerStats : EnemyStats
{
    [Header("Charger")]
    public float triggerRange;     // starts the charge if the player is closer
    public float windUpTime;
    public float chargeSpeed;
    public float chargeTime;
    public float hitDistance;      // hits the player during the charge at this distance
    public float stunTime;
    public float investigateTime;
    public float investigateSpeed;
}
