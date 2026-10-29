using UnityEngine;

/// <summary>
/// Bot sederhana (F2) untuk demo/pengujian: mengejar item terdekat lalu Exit,
/// sambil menjauhi enemy, menghindari obstacle, dan dash saat terjepit.
/// </summary>
public class AutoPilot : MonoBehaviour
{
    private const string StartKey = "P09_AutoPilot";

    [SerializeField] private float enemyAvoidRadius = 4.5f;
    [SerializeField] private float dashTriggerDistance = 1.9f;

    private static readonly float[] SteerAngles = { 0f, 35f, -35f, 70f, -70f, 110f, -110f };

    private Transform player;
    private SimplePlayerController controller;
    private int obstacleMask;

    public bool Active { get; private set; }
    public Vector2 Move { get; private set; }
    public bool WantsDash { get; private set; }

    private void Awake()
    {
        // Editor menu "Open And Play (Autopilot)" menyalakan bot lewat PlayerPrefs.
        if (PlayerPrefs.GetInt(StartKey, 0) == 1)
        {
            PlayerPrefs.SetInt(StartKey, 0);
            Active = true;
        }
    }

    private void Start()
    {
        player = GameManager.Instance.Player;
        controller = player.GetComponent<SimplePlayerController>();
        obstacleMask = LayerMask.GetMask("Obstacle");
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
        {
            Active = !Active;
        }

        Move = Vector2.zero;
        WantsDash = false;

        if (!Active || !GameManager.Instance.IsPlaying)
        {
            return;
        }

        Vector3 position = player.position;
        Vector3 desired = Vector3.zero;

        if (GameManager.Instance.TryGetGuideTarget(out Vector3 target))
        {
            desired = target - position;
            desired.y = 0f;
            desired = desired.normalized;
        }

        Vector3 avoid = Vector3.zero;
        float nearest = float.MaxValue;
        Vector3 nearestEnemy = Vector3.zero;

        for (int i = 0; i < EnemyChaser.All.Count; i++)
        {
            Vector3 away = position - EnemyChaser.All[i].transform.position;
            away.y = 0f;
            float distance = away.magnitude;

            if (distance < nearest)
            {
                nearest = distance;
                nearestEnemy = EnemyChaser.All[i].transform.position;
            }

            if (distance < enemyAvoidRadius && distance > 0.01f)
            {
                avoid += away / distance * ((enemyAvoidRadius - distance) / enemyAvoidRadius);
            }
        }

        Vector3 direction = (desired + avoid * 2.4f).normalized;

        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        foreach (float angle in SteerAngles)
        {
            Vector3 candidate = Quaternion.Euler(0f, angle, 0f) * direction;
            Vector3 probe = position + candidate * 1.8f;

            if (!Physics.CheckSphere(new Vector3(probe.x, 0.5f, probe.z), 0.9f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                direction = candidate;
                break;
            }
        }

        Move = new Vector2(direction.x, direction.z);

        // Dash menembus enemy yang terlalu dekat (dash kebal dan menghancurkan enemy).
        if (nearest < dashTriggerDistance && controller.DashReady01 >= 1f)
        {
            Vector3 toEnemy = nearestEnemy - position;
            toEnemy.y = 0f;
            Move = new Vector2(toEnemy.x, toEnemy.z).normalized;
            WantsDash = true;
        }
    }
}
