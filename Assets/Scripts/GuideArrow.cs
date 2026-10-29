using UnityEngine;

/// <summary>Panah di sekitar player yang menunjuk item terdekat (atau Exit saat terbuka).</summary>
public class GuideArrow : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Renderer arrowRenderer;
    [SerializeField] private float radius = 2.1f;

    private Vector3 direction = Vector3.forward;

    private void LateUpdate()
    {
        GameManager gm = GameManager.Instance;

        bool visible = gm != null && gm.TryGetGuideTarget(out Vector3 target);

        arrowRenderer.enabled = visible;

        if (!visible)
        {
            return;
        }

        gm.TryGetGuideTarget(out target);

        Vector3 delta = target - player.position;
        delta.y = 0f;

        if (delta.sqrMagnitude < 4f)
        {
            arrowRenderer.enabled = false;
            return;
        }

        direction = Vector3.Slerp(direction, delta.normalized, 1f - Mathf.Exp(-12f * Time.deltaTime));

        float pulse = 1f + Mathf.Sin(Time.time * 6f) * 0.12f;

        transform.position = new Vector3(player.position.x, 0.06f, player.position.z) + direction * (radius * pulse);
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(90f, 0f, 0f);
    }
}
