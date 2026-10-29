using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimplePlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6.5f;
    [SerializeField] private float acceleration = 45f;
    [SerializeField] private float deceleration = 55f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 21f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 1.6f;
    [SerializeField] private float dashHitRadius = 1.5f;

    [Header("Visual")]
    [SerializeField] private Transform visual;
    [SerializeField] private TrailRenderer trail;

    private CharacterController controller;
    private AutoPilot autoPilot;

    private Vector3 velocity;
    private Vector3 knock;
    private Vector3 dashDirection;
    private float dashTimer;
    private float dashCooldownTimer;
    private float groundY;

    public Vector3 Velocity => velocity;
    public bool IsDashing => dashTimer > 0f;
    public float DashReady01 => dashCooldown <= 0f ? 1f : 1f - Mathf.Clamp01(dashCooldownTimer / dashCooldown);

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        autoPilot = FindFirstObjectByType<AutoPilot>();
        groundY = transform.position.y;
    }

    public void ResetState(Vector3 position)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;

        velocity = Vector3.zero;
        knock = Vector3.zero;
        dashTimer = 0f;
        dashCooldownTimer = 0f;

        if (trail != null)
        {
            trail.Clear();
        }
    }

    public void Knockback(Vector3 direction, float force)
    {
        direction.y = 0f;
        knock = direction.normalized * force;
    }

    private void Update()
    {
        bool active = GameManager.Instance == null || GameManager.Instance.IsPlaying;

        if (!active)
        {
            velocity = Vector3.zero;
            UpdateTrail(false);
            return;
        }

        Vector3 input = ReadInput(out bool wantsDash);
        float dt = Time.deltaTime;

        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - dt);

        if (wantsDash && dashCooldownTimer <= 0f && dashTimer <= 0f)
        {
            StartDash(input);
        }

        Vector3 move;

        if (dashTimer > 0f)
        {
            dashTimer -= dt;
            move = dashDirection * dashSpeed;
            HitEnemiesWhileDashing();
        }
        else
        {
            Vector3 target = input * moveSpeed;
            float rate = input.sqrMagnitude > 0.01f ? acceleration : deceleration;
            velocity = Vector3.MoveTowards(velocity, target, rate * dt);

            move = velocity + knock;
            knock = Vector3.MoveTowards(knock, Vector3.zero, 30f * dt);
        }

        controller.Move(move * dt);

        Vector3 position = transform.position;
        position.y = groundY;
        transform.position = position;

        RotateVisual(dt);
        UpdateTrail(IsDashing || velocity.sqrMagnitude > 4f);
    }

    private Vector3 ReadInput(out bool wantsDash)
    {
        if (autoPilot != null && autoPilot.Active)
        {
            wantsDash = autoPilot.WantsDash;
            return new Vector3(autoPilot.Move.x, 0f, autoPilot.Move.y);
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        wantsDash = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift);

        return new Vector3(horizontal, 0f, vertical).normalized;
    }

    private void StartDash(Vector3 input)
    {
        Vector3 forward = visual != null ? visual.forward : transform.forward;
        dashDirection = input.sqrMagnitude > 0.01f ? input.normalized : new Vector3(forward.x, 0f, forward.z).normalized;

        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;

        AudioManager.Play(Sfx.Dash);
        Fx.Burst(transform.position, new Color(0.4f, 0.95f, 1f), 18, 4f, 0.2f, 0.4f, 0f);
        Fx.Ring(transform.position - Vector3.up, new Color(0.4f, 0.95f, 1f), 2.2f, 0.4f);

        CameraRig.Kick(3.5f);
        PostFxController.Pulse(0.35f, 0.08f);
    }

    private void HitEnemiesWhileDashing()
    {
        for (int i = EnemyChaser.All.Count - 1; i >= 0; i--)
        {
            EnemyChaser enemy = EnemyChaser.All[i];

            if (enemy == null)
            {
                continue;
            }

            Vector3 delta = enemy.transform.position - transform.position;
            delta.y = 0f;

            if (delta.sqrMagnitude <= dashHitRadius * dashHitRadius)
            {
                enemy.Hit(transform.position, 1);
            }
        }
    }

    private void RotateVisual(float dt)
    {
        if (visual == null)
        {
            return;
        }

        Vector3 flat = IsDashing ? dashDirection : velocity;
        flat.y = 0f;

        if (flat.sqrMagnitude > 0.2f)
        {
            Quaternion target = Quaternion.LookRotation(flat.normalized, Vector3.up);
            visual.rotation = Quaternion.Slerp(visual.rotation, target, 1f - Mathf.Exp(-16f * dt));
        }
    }

    private void UpdateTrail(bool emitting)
    {
        if (trail != null)
        {
            trail.emitting = emitting;
        }
    }
}
