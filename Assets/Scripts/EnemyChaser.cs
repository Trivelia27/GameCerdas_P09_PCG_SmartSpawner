using System.Collections.Generic;
using UnityEngine;

public enum EnemyBehavior
{
    Chase,      // seek langsung ke player
    Pursuit,    // memprediksi posisi player berdasarkan kecepatannya
    Charger,    // mengejar, lalu windup dan menerjang
    Brute       // lambat, kuat, damage besar
}

/// <summary>
/// NPC dengan state sederhana: Wander (idle) -> Chase. Setiap jenis punya perilaku berbeda.
/// Menghindari obstacle dengan mencoba arah miring, dan saling menjauh (separation) antar-enemy.
/// </summary>
public class EnemyChaser : MonoBehaviour
{
    public static readonly List<EnemyChaser> All = new List<EnemyChaser>();

    private enum Phase
    {
        Move,
        Windup,
        Charge
    }

    [Header("Behavior")]
    [SerializeField] private EnemyBehavior behavior = EnemyBehavior.Chase;
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private int contactDamage = 1;
    [SerializeField] private int maxHealth = 1;
    [SerializeField] private int scoreValue = 10;
    [SerializeField] private float aggroRadius = 10f;
    [SerializeField] private float contactRadius = 1f;
    [SerializeField] private float bodyRadius = 0.5f;
    [SerializeField] private float wanderRadius = 3f;

    [Header("Visual")]
    [SerializeField] private Transform visual;
    [SerializeField] private Color tint = Color.red;
    [SerializeField] private float spinSpeed = 60f;

    private static readonly float[] SteerAngles = { 0f, 40f, -40f, 80f, -80f };

    private const float ArenaLimit = 14.4f;

    private Transform target;
    private SimplePlayerController playerController;
    private SpawnPop pop;
    private int obstacleMask;

    private Vector3 home;
    private Vector3 wanderTarget;
    private float wanderTimer;
    private Vector3 knock;

    private Phase phase = Phase.Move;
    private float phaseTimer;
    private float nextChargeTime;
    private Vector3 chargeDirection;

    private Vector3 visualBaseScale = Vector3.one;
    private float visualBaseY;
    private float bobOffset;

    private int health;
    private float hurtImmunity;
    private bool aggro;
    private bool dead;
    private float readyTime;

    public int ScoreValue => scoreValue;
    public Color Tint => tint;

    public void Configure(EnemyBehavior enemyBehavior, float speed, int damage, int hp, int score, float aggro, Color color, float spin)
    {
        behavior = enemyBehavior;
        moveSpeed = speed;
        contactDamage = damage;
        maxHealth = hp;
        scoreValue = score;
        aggroRadius = aggro;
        tint = color;
        spinSpeed = spin;
    }

    private void OnEnable()
    {
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
    }

    private void Awake()
    {
        pop = GetComponent<SpawnPop>();
        health = maxHealth;
        // Jangan memakai Random global di Awake: Awake berjalan saat Instantiate di tengah generation PCG.
        bobOffset = (GetInstanceID() & 0xFFFF) * 0.0007f;

        if (visual != null)
        {
            visualBaseScale = visual.localScale;
            visualBaseY = visual.localPosition.y;
        }
    }

    private void Start()
    {
        obstacleMask = LayerMask.GetMask("Obstacle");
        home = transform.position;
        wanderTarget = home;

        if (GameManager.Instance != null)
        {
            target = GameManager.Instance.Player;
            playerController = target.GetComponent<SimplePlayerController>();
        }
    }

    private void Update()
    {
        if (target == null || dead)
        {
            return;
        }

        if (pop != null && !pop.Done)
        {
            return;
        }

        GameManager gm = GameManager.Instance;

        // Saat Play dihentikan, Instance bisa sudah null sementara enemy masih sempat ter-update.
        if (gm == null)
        {
            return;
        }

        GameState state = gm.State;

        if (state != GameState.Playing && state != GameState.Title)
        {
            return;
        }

        float dt = Time.deltaTime;

        readyTime += dt;
        hurtImmunity = Mathf.Max(0f, hurtImmunity - dt);

        AnimateVisual(dt);

        if (knock.sqrMagnitude > 0.01f)
        {
            TryMove(knock * dt);
            knock = Vector3.MoveTowards(knock, Vector3.zero, 28f * dt);
        }

        if (readyTime < 1f)
        {
            return;
        }

        Vector3 toPlayer = target.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;

        bool aggroAllowed = state == GameState.Playing;

        if (aggroAllowed && distance <= contactRadius)
        {
            GameManager.Instance.DamagePlayer(contactDamage, transform.position);
            knock = -toPlayer.normalized * 7f;

            if (phase != Phase.Move)
            {
                phase = Phase.Move;
                nextChargeTime = Time.time + 2.5f;
            }

            return;
        }

        float range = aggro ? aggroRadius * 1.4f : aggroRadius;
        aggro = aggroAllowed && distance <= range;

        if (!aggro)
        {
            phase = Phase.Move;
            Wander(dt);
            return;
        }

        Vector3 direction = toPlayer / Mathf.Max(distance, 0.001f);

        switch (behavior)
        {
            case EnemyBehavior.Pursuit:
                Vector3 predicted = target.position + playerController.Velocity * Mathf.Clamp(distance / moveSpeed, 0f, 1.2f);
                Vector3 toPredicted = predicted - transform.position;
                toPredicted.y = 0f;
                Steer(toPredicted.normalized, moveSpeed, dt);
                break;

            case EnemyBehavior.Charger:
                UpdateCharger(direction, distance, dt);
                break;

            default:
                Steer(direction, moveSpeed, dt);
                break;
        }
    }

    private void UpdateCharger(Vector3 direction, float distance, float dt)
    {
        switch (phase)
        {
            case Phase.Move:
                Steer(direction, moveSpeed, dt);

                if (Time.time >= nextChargeTime && distance < 8f && distance > 2.5f)
                {
                    phase = Phase.Windup;
                    phaseTimer = 0.55f;
                }
                break;

            case Phase.Windup:
                chargeDirection = direction;
                FaceDirection(chargeDirection, dt, 18f);

                phaseTimer -= dt;

                if (phaseTimer <= 0f)
                {
                    phase = Phase.Charge;
                    phaseTimer = 0.7f;
                    Fx.Ring(transform.position, tint, 1.6f, 0.35f);
                }
                break;

            case Phase.Charge:
                TryMove(chargeDirection * moveSpeed * 3.4f * dt);
                FaceDirection(chargeDirection, dt, 18f);

                phaseTimer -= dt;

                if (phaseTimer <= 0f)
                {
                    phase = Phase.Move;
                    nextChargeTime = Time.time + 3.2f;
                }
                break;
        }
    }

    private void Wander(float dt)
    {
        wanderTimer -= dt;

        if (wanderTimer <= 0f || (wanderTarget - transform.position).sqrMagnitude < 0.2f)
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            wanderTarget = home + new Vector3(offset.x, 0f, offset.y);
            wanderTimer = Random.Range(2f, 4.5f);
        }

        Vector3 toTarget = wanderTarget - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > 0.05f)
        {
            Steer(toTarget.normalized, moveSpeed * 0.35f, dt);
        }
    }

    private void Steer(Vector3 desired, float speed, float dt)
    {
        Vector3 separation = Vector3.zero;

        for (int i = 0; i < All.Count; i++)
        {
            EnemyChaser other = All[i];

            if (other == this)
            {
                continue;
            }

            Vector3 away = transform.position - other.transform.position;
            away.y = 0f;
            float d = away.magnitude;

            if (d > 0.001f && d < 1.2f)
            {
                separation += away / d * (1.2f - d);
            }
        }

        Vector3 direction = (desired + separation * 1.6f).normalized;
        float step = speed * dt;

        foreach (float angle in SteerAngles)
        {
            Vector3 candidate = Quaternion.Euler(0f, angle, 0f) * direction;

            if (TryMove(candidate * step))
            {
                FaceDirection(candidate, dt, 12f);
                return;
            }
        }
    }

    private bool TryMove(Vector3 delta)
    {
        Vector3 next = transform.position + delta;
        next.x = Mathf.Clamp(next.x, -ArenaLimit, ArenaLimit);
        next.z = Mathf.Clamp(next.z, -ArenaLimit, ArenaLimit);

        if (Physics.CheckSphere(new Vector3(next.x, 0.5f, next.z), bodyRadius, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        transform.position = next;
        return true;
    }

    private void FaceDirection(Vector3 direction, float dt, float sharpness)
    {
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-sharpness * dt));
    }

    private void AnimateVisual(float dt)
    {
        if (visual == null)
        {
            return;
        }

        visual.localRotation *= Quaternion.Euler(0f, spinSpeed * dt, 0f);

        float bob = Mathf.Sin(Time.time * 3f + bobOffset) * 0.07f;
        visual.localPosition = new Vector3(0f, visualBaseY + bob, 0f);

        float pulse = 1f;

        if (phase == Phase.Windup)
        {
            pulse = 1.25f + Mathf.Sin(Time.time * 40f) * 0.12f;
        }
        else if (aggro)
        {
            pulse = 1f + Mathf.Sin(Time.time * 9f + bobOffset) * 0.05f;
        }

        visual.localScale = visualBaseScale * pulse;
    }

    /// <summary>Dipanggil saat player dash menabrak. Mengembalikan true jika enemy mati.</summary>
    public bool Hit(Vector3 fromPosition, int damage)
    {
        if (dead || hurtImmunity > 0f)
        {
            return false;
        }

        health -= damage;
        hurtImmunity = 0.4f;

        Vector3 away = transform.position - fromPosition;
        away.y = 0f;
        knock = away.normalized * 11f;

        Fx.Burst(transform.position + Vector3.up * 0.8f, tint, 10, 4f, 0.18f, 0.4f);

        if (health > 0)
        {
            AudioManager.Play(Sfx.Hit, 0.5f, 1.3f);
            return false;
        }

        Die();
        return true;
    }

    private void Die()
    {
        dead = true;

        Vector3 center = transform.position + Vector3.up * 0.8f;
        Fx.Burst(center, tint, 34, 8f, 0.3f, 0.8f);
        Fx.Burst(center, Color.white, 12, 5f, 0.15f, 0.5f, 0.2f);
        Fx.Ring(transform.position, tint, 2.6f, 0.5f);

        GameManager.Instance.OnEnemyKilled(this);

        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, aggroRadius);
    }
}
