using UnityEngine;

// Patrol -> WindUp -> Charge -> Stunned -> Patrol ; alarm: Patrol -> Investigate
// after the charge it is stunned: good moment to hit it from behind
public class ChargerEnemy : EnemyBase
{
    enum State { Patrol, Investigate, WindUp, Charge, Stunned }

    [Header("Charger")]
    [SerializeField] LineRenderer pathLine;  // shows where it will run
    [SerializeField] LayerMask wallMask;
    [SerializeField] GameObject dustEffect;
    [SerializeField] AudioClip chargeSound;
    [SerializeField] AudioClip wallHitSound;

    ChargerStats data; // the same asset as "stats", but with the charger numbers
    State state;
    string[] stateNames = { "Патруль", "Проверяет", "Замах", "Рывок", "Оглушён" };
    Vector2 chargeDirection;
    bool hitPlayer;
    float dustTimer;

    protected override void ApplyStats()
    {
        base.ApplyStats();
        data = (ChargerStats)stats;
    }

    protected override void Start()
    {
        base.Start();
        ChangeState(State.Patrol);
    }

    public override bool IsAlerted()
    {
        return state == State.Investigate || state == State.WindUp || state == State.Charge;
    }

    public override void HearAlarm(Vector2 position)
    {
        if (state != State.Patrol) return;
        vision.lastSeenPosition = position;
        ChangeState(State.Investigate);
    }

    void Update()
    {
        stateTimer -= Time.deltaTime;

        switch (state)
        {
            case State.Patrol:
                Patrol();
                if (CanSeePlayer() && DistanceToPlayer() < data.triggerRange)
                {
                    RaiseAlarm();
                    ChangeState(State.WindUp);
                }
                break;

            case State.Investigate:
                if (CanSeePlayer() && DistanceToPlayer() < data.triggerRange)
                    ChangeState(State.WindUp);
                else if (!Reached(vision.lastSeenPosition))
                    MoveTo(vision.lastSeenPosition, data.investigateSpeed);
                else
                    LookAround();

                if (stateTimer <= 0) ChangeState(State.Patrol);
                break;

            case State.WindUp:
                StopMoving();
                if (stateTimer <= 0) ChangeState(State.Charge);
                break;

            case State.Charge:
                rb.linearVelocity = chargeDirection * data.chargeSpeed;

                dustTimer -= Time.deltaTime;
                if (dustTimer <= 0)
                {
                    Instantiate(dustEffect, transform.position, Quaternion.identity);
                    dustTimer = 0.05f;
                }

                if (!hitPlayer && DistanceToPlayer() < data.hitDistance)
                {
                    PlayerController.instance.health.TakeDamage(data.damage);
                    hitPlayer = true;
                }

                if (stateTimer <= 0) ChangeState(State.Stunned);
                break;

            case State.Stunned:
                StopMoving();
                if (stateTimer <= 0) ChangeState(State.Patrol);
                break;
        }
    }

    // running into a wall also stuns it
    void OnCollisionEnter2D(Collision2D collision)
    {
        bool isWall = ((1 << collision.gameObject.layer) & wallMask) != 0;
        if (state == State.Charge && isWall)
        {
            Sound.Play(wallHitSound);
            CameraFollow.instance.Shake(0.2f);
            ChangeState(State.Stunned);
        }
    }

    void ChangeState(State newState)
    {
        state = newState;
        SetLabel(stateNames[(int)newState]);
        pathLine.enabled = false;
        telegraph.Hide();
        ShowIcon(null);

        if (newState == State.Investigate)
        {
            stateTimer = data.investigateTime;
            ShowIcon(searchIcon);
        }
        if (newState == State.WindUp)
        {
            stateTimer = data.windUpTime;
            chargeDirection = DirectionToPlayer(); // direction is fixed now, the red line shows it
            LookAt(chargeDirection);
            ShowPath();
            telegraph.Show();
            visual.Shake(data.windUpTime);
            ShowIcon(alertIcon);
        }
        if (newState == State.Charge)
        {
            stateTimer = data.chargeTime;
            hitPlayer = false;
            Sound.Play(chargeSound);
        }
        if (newState == State.Stunned)
        {
            stateTimer = data.stunTime;
            StopMoving();
            visual.Shake(data.stunTime);
            ShowIcon(stunIcon);
        }
    }

    void ShowPath()
    {
        float length = data.chargeSpeed * data.chargeTime;
        Vector2 start = transform.position;
        Vector2 end = start + chargeDirection * length;
        RaycastHit2D hit = Physics2D.Raycast(start, chargeDirection, length, wallMask);
        if (hit.collider != null) end = hit.point;

        pathLine.enabled = true;
        pathLine.SetPosition(0, start);
        pathLine.SetPosition(1, end);
    }
}
