using UnityEngine;

/// <summary>Kamera sinematik: orbit di layar judul, mengikuti player saat bermain, plus shake dan FOV kick.</summary>
[RequireComponent(typeof(Camera))]
public class CameraRig : MonoBehaviour
{
    private static CameraRig instance;

    [SerializeField] private Transform target;
    [SerializeField] private Vector3 center = Vector3.zero;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 21f, -13f);
    [SerializeField] [Range(0f, 1f)] private float followStrength = 0.45f;
    [SerializeField] private float baseFov = 52f;
    [SerializeField] private float orbitRadius = 25f;
    [SerializeField] private float orbitHeight = 17f;
    [SerializeField] private float orbitSpeed = 7f;

    private Camera cam;
    private Vector3 currentPosition;
    private Vector3 currentLook;
    private float orbitAngle = 200f;
    private float shake;
    private float fovKick;

    public static void Shake(float amount)
    {
        if (instance != null)
        {
            instance.shake = Mathf.Max(instance.shake, amount);
        }
    }

    public static void Kick(float fov)
    {
        if (instance != null)
        {
            instance.fovKick = Mathf.Max(instance.fovKick, fov);
        }
    }

    private void Awake()
    {
        instance = this;
        cam = GetComponent<Camera>();
        currentPosition = transform.position;
        currentLook = center;
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        GameManager gm = GameManager.Instance;
        bool orbit = gm == null || gm.State == GameState.Title;

        Vector3 desiredLook;
        Vector3 desiredPosition;

        if (orbit)
        {
            orbitAngle += orbitSpeed * dt;
            float rad = orbitAngle * Mathf.Deg2Rad;

            desiredLook = center;
            desiredPosition = center + new Vector3(Mathf.Sin(rad) * orbitRadius, orbitHeight, Mathf.Cos(rad) * orbitRadius);
        }
        else
        {
            Vector3 focus = target != null ? target.position : center;
            focus.y = 0f;

            desiredLook = center + (focus - center) * followStrength;
            desiredPosition = desiredLook + followOffset;
        }

        float blend = 1f - Mathf.Exp(-3.2f * dt);
        currentPosition = Vector3.Lerp(currentPosition, desiredPosition, blend);
        currentLook = Vector3.Lerp(currentLook, desiredLook, blend);

        shake = Mathf.MoveTowards(shake, 0f, dt * 2.4f);
        fovKick = Mathf.MoveTowards(fovKick, 0f, dt * 14f);

        Vector3 offset = Random.insideUnitSphere * (shake * 0.45f);

        transform.position = currentPosition + offset;
        transform.rotation = Quaternion.LookRotation(currentLook - currentPosition, Vector3.up);

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, baseFov + fovKick, 1f - Mathf.Exp(-14f * dt));
    }
}
