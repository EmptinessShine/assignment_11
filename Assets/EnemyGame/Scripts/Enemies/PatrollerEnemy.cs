using UnityEngine;

// Patrol -> Chase -> WindUp -> Attack -> Cooldown -> Chase / Search -> Patrol
public class PatrollerEnemy : EnemyBase
{
    enum State { Patrol, Chase, WindUp, Attack, Cooldown, Search }

    [Header("Patroller")]
    [SerializeField] GameObject slashEffect;
    [SerializeField] AudioClip slashSound;

    PatrollerStats data; // the same asset as "stats", but with the patroller numbers
    State state;
    string[] stateNames = { "Патруль", "Погоня", "Замах", "Удар", "Отдых", "Поиск" };

    protected override void ApplyStats()
    {
        base.ApplyStats();
        data = (PatrollerStats)stats;
    }

    protected override void Start()
    {
        base.Start();
        ChangeState(State.Patrol);
    }

    public override bool IsAlerted()
    {
        return state != State.Patrol;
    }

    public override void HearAlarm(Vector2 position)
    {
        vision.lastSeenPosition = position;
        ChangeState(State.Search);
    }

    void Update()
    {
        stateTimer -= Time.deltaTime;

        switch (state)
        {
            case State.Patrol:
                Patrol();
                if (CanSeePlayer())
                {
                    RaiseAlarm();
                    ChangeState(State.Chase);
                }
                break;

            case State.Chase:
                if (!CanSeePlayer())
                    ChangeState(State.Search);
                else if (DistanceToPlayer() < data.attackRange)
                    ChangeState(State.WindUp);
                else
                    MoveTo(player.position, data.chaseSpeed);
                break;

            case State.WindUp:
                StopMoving();
                LookAt(DirectionToPlayer());
                if (stateTimer <= 0) ChangeState(State.Attack);
                break;

            case State.Attack:
                HitPlayer();
                ChangeState(State.Cooldown);
                break;

            case State.Cooldown:
                StopMoving();
                if (stateTimer <= 0)
                {
                    if (CanSeePlayer()) ChangeState(State.Chase);
                    else ChangeState(State.Search);
                }
                break;

            case State.Search:
                if (CanSeePlayer())
                    ChangeState(State.Chase);
                else if (!Reached(vision.lastSeenPosition))
                    MoveTo(vision.lastSeenPosition, data.chaseSpeed);
                else
                    LookAround();

                if (stateTimer <= 0) ChangeState(State.Patrol);
                break;
        }
    }

    void HitPlayer()
    {
        Vector2 dir = DirectionToPlayer();
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Instantiate(slashEffect, (Vector2)transform.position + dir * 0.7f, Quaternion.Euler(0, 0, angle));
        visual.Lunge(dir);
        Sound.Play(slashSound, 0.8f);

        if (DistanceToPlayer() < data.hitRange)
            PlayerController.instance.health.TakeDamage(data.damage);
    }

    void ChangeState(State newState)
    {
        state = newState;
        SetLabel(stateNames[(int)newState]);
        telegraph.Hide();
        ShowIcon(null);

        if (newState == State.Chase) ShowIcon(alertIcon);
        if (newState == State.Search)
        {
            stateTimer = data.searchTime;
            ShowIcon(searchIcon);
        }
        if (newState == State.WindUp)
        {
            stateTimer = data.windUpTime;
            telegraph.Show();
            visual.Shake(data.windUpTime);
        }
        if (newState == State.Cooldown) stateTimer = data.cooldownTime;
    }
}
