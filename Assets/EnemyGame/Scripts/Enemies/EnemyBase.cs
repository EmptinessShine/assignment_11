using System.Collections.Generic;
using UnityEngine;

// common things for all enemies. every enemy type has its own state machine.
public abstract class EnemyBase : MonoBehaviour
{
    public static List<EnemyBase> allEnemies = new List<EnemyBase>();

    [SerializeField] protected EnemyStats stats; // numbers from the stats asset

    [Header("Visuals")]
    [SerializeField] protected CharacterVisual visual;
    [SerializeField] protected SpriteRenderer icon;
    [SerializeField] protected Sprite alertIcon;
    [SerializeField] protected Sprite searchIcon;
    [SerializeField] protected Sprite stunIcon;
    [SerializeField] protected TextMesh stateLabel;

    protected Rigidbody2D rb;
    protected Health health;
    protected VisionSensor vision;
    protected Telegraph telegraph;
    protected Transform player;
    protected float stateTimer;
    protected Vector2[] route; // patrol points from LevelData

    int waypointIndex;

    // true when the enemy knows about the player (fighting or searching)
    public abstract bool IsAlerted();

    // another enemy saw the player at this position
    public abstract void HearAlarm(Vector2 position);

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        vision = GetComponent<VisionSensor>();
        telegraph = GetComponent<Telegraph>();
        health.Died += Die;
        allEnemies.Add(this);
        ApplyStats();
    }

    protected virtual void Start()
    {
        player = PlayerController.instance.transform;
        stateLabel.gameObject.SetActive(SaveSystem.Data.showLabels); // setting from the menu
    }

    void OnDestroy()
    {
        allEnemies.Remove(this);
    }

    // takes the numbers from the stats asset. child classes add their own numbers.
    protected virtual void ApplyStats()
    {
        health.Init(stats.maxHealth, stats.invincibleTime);
        vision.Setup(stats.viewDistance, stats.viewAngle);
        float size = stats.size;
        if (size <= 0) size = 1; // a new asset made by hand has size 0
        transform.localScale = Vector3.one * size;
    }

    // GameManager calls this right after spawning the enemy from LevelData
    public void Setup(EnemySpawn spawn)
    {
        if (spawn.statsOverride != null)
        {
            stats = spawn.statsOverride;
            ApplyStats();
        }
        route = spawn.route;
        LookAt(spawn.lookDirection);
    }

    protected bool CanSeePlayer()
    {
        if (PlayerController.instance.IsDead()) return false;
        if (!IsOnScreen()) return false; // no attacks from off-screen
        return vision.CanSee(player);
    }

    bool IsOnScreen()
    {
        Vector3 p = Camera.main.WorldToViewportPoint(transform.position);
        return p.x > 0 && p.x < 1 && p.y > 0 && p.y < 1;
    }

    protected float DistanceToPlayer()
    {
        return Vector2.Distance(transform.position, player.position);
    }

    protected Vector2 DirectionToPlayer()
    {
        return (player.position - transform.position).normalized;
    }

    protected void MoveTo(Vector2 target, float speed)
    {
        Vector2 direction = (target - (Vector2)transform.position).normalized;
        rb.linearVelocity = direction * speed;
        LookAt(direction);
    }

    protected void StopMoving()
    {
        rb.linearVelocity = Vector2.zero;
    }

    protected void LookAt(Vector2 direction)
    {
        if (direction == Vector2.zero) return;
        vision.facing = direction.normalized;
        visual.LookAt(direction);
    }

    protected void LookAround()
    {
        StopMoving();
        LookAt(Quaternion.Euler(0, 0, stats.lookAroundSpeed * Time.deltaTime) * vision.facing);
    }

    protected bool Reached(Vector2 target)
    {
        return Vector2.Distance(transform.position, target) < 0.4f;
    }

    protected void Patrol()
    {
        if (route == null || route.Length == 0)
        {
            StopMoving();
            return;
        }

        Vector2 target = route[waypointIndex];
        if (Reached(target))
            waypointIndex = (waypointIndex + 1) % route.Length;

        MoveTo(target, stats.moveSpeed);
    }

    protected void RaiseAlarm()
    {
        AlertSystem.instance.RaiseAlarm(this, player.position);
    }

    protected void ShowIcon(Sprite sprite)
    {
        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    protected void SetLabel(string text)
    {
        stateLabel.text = text;
    }

    // for backstab: is the attacker behind this enemy?
    public bool IsLookingAwayFrom(Vector2 position)
    {
        Vector2 toAttacker = position - (Vector2)transform.position;
        return Vector2.Angle(vision.facing, toAttacker) > stats.backstabAngle;
    }

    void Die()
    {
        GameManager.instance.EnemyKilled(stats.scoreValue);
        Destroy(gameObject);
    }
}
