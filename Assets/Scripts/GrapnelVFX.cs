using UnityEngine;

/// <summary>
/// The rope for Grapnel (boss relic). Procedural, no prefab — same house pattern as DashAfterimage,
/// ShockwaveVFX and BouncePad.
///
/// ⚠️ IT MUST BE A SCENE-ROOT OBJECT, NOT A CHILD OF THE PLAYER. A LineRenderer parented to a moving
/// transform inherits that transform every frame, so the rope would swim as the player is reeled in
/// instead of staying pinned to the anchor. It follows by reading the player's position instead.
///
/// Carries TemporaryObject so a room change sweeps it, and self-destructs if its owner disappears —
/// the same two guards EnemyHealthBar needed for the same reason.
/// </summary>
public class GrapnelVFX : MonoBehaviour
{
    private Transform owner;
    private Vector2 anchor;
    private LineRenderer line;
    private float born;
    private bool finishing;

    public static GrapnelVFX Play(Transform owner, Vector2 anchor)
    {
        GameObject go = new GameObject("GrapnelVFX");
        go.AddComponent<TemporaryObject>();
        GrapnelVFX fx = go.AddComponent<GrapnelVFX>();
        fx.owner = owner;
        fx.anchor = anchor;
        return fx;
    }

    private void Awake()
    {
        born = Time.time;

        line = gameObject.AddComponent<LineRenderer>();
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.numCapVertices = 2;
        line.sortingOrder = 60;

        // Rope, not energy: warm hemp against the dungeon's cool stone, thicker at the hand than at
        // the hook so it reads as being thrown away from you.
        line.startWidth = 0.10f;
        line.endWidth = 0.05f;
        line.startColor = new Color(0.78f, 0.62f, 0.36f, 1f);
        line.endColor = new Color(0.55f, 0.42f, 0.24f, 1f);
    }

    private void Update()
    {
        if (owner == null) { Destroy(gameObject); return; }

        Vector3 hand = owner.position + Vector3.up * 0.85f;
        hand.z = PlayPlane.Z - 0.1f;                       // just in front of the actors
        Vector3 tip = new Vector3(anchor.x, anchor.y, PlayPlane.Z - 0.1f);

        line.SetPosition(0, hand);
        line.SetPosition(1, tip);

        if (finishing)
        {
            // Snaps slack rather than fading: a rope that dissolves reads as magic, and this one is
            // deliberately the only non-magical traversal tool in the game.
            float k = Mathf.Clamp01((Time.time - born) * 6f);
            line.startWidth = Mathf.Lerp(0.10f, 0f, k);
            line.endWidth = Mathf.Lerp(0.05f, 0f, k);
            if (k >= 1f) Destroy(gameObject);
        }

        // Hard ceiling: the pull routine has its own timeout, but nothing should be able to leave a
        // rope hanging in the room for the rest of the run.
        if (Time.time - born > 4f) Destroy(gameObject);
    }

    public void Finish()
    {
        finishing = true;
        born = Time.time;
    }
}
