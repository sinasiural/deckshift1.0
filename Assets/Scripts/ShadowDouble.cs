using System.Collections;
using UnityEngine;

/// <summary>
/// One of the Kagemusha's body doubles. Design doc: BossDesign_Samurai.md §4 and §8.
///
/// A double is a SECOND STRIPPED RIG — the same Cainos preset the boss wears — with no Rigidbody2D,
/// a trigger collider, and this component. It is not an enemy: it has no EnemyHealth and nothing
/// the player throws at it registers. It has exactly three jobs:
///
///   MIRROR   copy the boss's animator parameters so it moves when he moves.
///   STRIKE   perform the same lane cut he does, at the same moment, from where it stands.
///   SHATTER  burst when the player TOUCHES it before it strikes — the fight's card-free damage
///            route: the shard flies home and hurts the real one, and a Shift crystal drops.
///
/// ⚠️ THE RIG CANNOT BE TINTED, ONLY FADED. `_Color` does not exist on 15 of the 16 renderers
/// (Alpha Cut and Body expose only `_Alpha`), and setting a missing shader property is a silent
/// no-op — the exact bug that kept the gravity-reversal flash invisible for months. A "shadow"
/// double is therefore a TRANSLUCENT one, via the `_Alpha` handle every Cainos rig shader shares.
/// The finale twist (doubles turn solid at 40% HP) is the same handle set to 1.
///
/// ⚠️ IT IS NOT A CHILD OF THE BOSS AT RUNTIME. It ships as one in the prefab so the stripping is
/// done once in the editor, but the boss re-parents it to the room in Awake — a child would travel
/// with him through every dash and swap. It carries TemporaryObject so the room sweep gets it.
/// </summary>
public class ShadowDouble : MonoBehaviour
{
    public enum State { Hidden, Kneeling, Armed, Striking, Standing }

    [Tooltip("The rig child that gets flipped for facing. Empty = the first child.")]
    public Transform visualModel;

    [Tooltip("Damage to the player for touching a double AFTER it has struck (or while it strikes).")]
    public float touchDamage = 12f;
    public float touchKnockback = 6f;

    public State Current { get; private set; } = State.Hidden;

    private KagemushaBoss owner;
    private Animator animator;
    private Collider2D trigger;
    private SkinnedMeshRenderer[] skins;
    private SpriteRenderer[] sprites;          // the weapon, if any — a SpriteRenderer, not a skin
    private MaterialPropertyBlock mpb;
    private SpriteRenderer mark;               // contact mark: absent by default (a shadow casts none)
    private float alpha = 0.5f;
    private bool facingRight = true;
    private float visualScaleX = 1f;
    private float nextTouchTime;
    private static readonly int AlphaId = Shader.PropertyToID("_Alpha");

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>(true);
        trigger = GetComponent<Collider2D>();
        if (trigger != null) trigger.isTrigger = true;

        if (visualModel == null && transform.childCount > 0) visualModel = transform.GetChild(0);
        if (visualModel != null) visualScaleX = Mathf.Abs(visualModel.localScale.x);

        skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        sprites = GetComponentsInChildren<SpriteRenderer>(true);
        mpb = new MaterialPropertyBlock();

        if (GetComponent<TemporaryObject>() == null) gameObject.AddComponent<TemporaryObject>();
    }

    public void Bind(KagemushaBoss boss)
    {
        owner = boss;
        // Out from under the boss — see the class header. Parented to whatever HE is parented to
        // (the room), so it is destroyed with the room like everything else in it.
        transform.SetParent(boss.transform.parent, true);
    }

    // ---- visibility ----------------------------------------------------------------------------

    public void SetAlpha(float a)
    {
        alpha = a;
        if (skins != null)
            foreach (var s in skins)
            {
                if (s == null) continue;
                s.GetPropertyBlock(mpb);
                mpb.SetFloat(AlphaId, a);
                s.SetPropertyBlock(mpb);
            }
        if (sprites != null)
            foreach (var sr in sprites)
            {
                if (sr == null || sr == mark) continue;
                Color c = sr.color; c.a = a; sr.color = c;
            }
    }

    /// <summary>The warm mark under the real one's feet. Doubles get it only for the finale twist.</summary>
    public void ShowMark(bool on, Color colour)
    {
        if (on && mark == null)
        {
            var go = new GameObject("Mark");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0.08f, 0.1f);
            go.transform.localScale = new Vector3(1.9f, 0.55f, 1f);
            mark = go.AddComponent<SpriteRenderer>();
            mark.sprite = FlatUI.SoftGlow();
            mark.sortingOrder = -1;
        }
        if (mark != null)
        {
            mark.enabled = on;
            mark.color = new Color(colour.r, colour.g, colour.b, 0.55f);
        }
    }

    public void Appear(Vector3 position, bool faceRight, float withAlpha)
    {
        transform.position = new Vector3(position.x, position.y, PlayPlane.Z);
        gameObject.SetActive(true);
        Face(faceRight);
        SetAlpha(withAlpha);
        Current = State.Standing;
        if (trigger != null) trigger.enabled = true;
    }

    public void Vanish()
    {
        Current = State.Hidden;
        if (trigger != null) trigger.enabled = false;
        gameObject.SetActive(false);
    }

    /// <summary>Fade out over `seconds`, then hide. Used by the awaken (two of three dissolve).</summary>
    public IEnumerator Dissolve(float seconds)
    {
        Current = State.Hidden;
        if (trigger != null) trigger.enabled = false;
        float from = alpha, t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            SetAlpha(Mathf.Lerp(from, 0f, t / seconds));
            yield return null;
        }
        gameObject.SetActive(false);
    }

    public void SetState(State s) => Current = s;

    public void Face(bool right)
    {
        facingRight = right;
        if (visualModel == null) return;
        Vector3 s = visualModel.localScale;
        s.x = visualScaleX * (right ? 1f : -1f);
        visualModel.localScale = s;
    }

    public bool FacingRight => facingRight;

    // ---- mirroring the boss ----------------------------------------------------------------------
    // Copies the handful of parameters that drive his poses. Called by the boss every frame the
    // doubles are up, so a double crouches when he crouches and swings when he swings.
    private static readonly string[] Bools  = { "IsMoving", "IsAttacking", "IsCrouching", "IsGrounded", "IsDashing" };
    private static readonly string[] Floats = { "MoveBlendX", "MoveSpeedMul", "VelocityY", "AttackSpeedMul" };

    public void Mirror(Animator source)
    {
        if (animator == null || source == null) return;
        foreach (string b in Bools) animator.SetBool(b, source.GetBool(b));
        foreach (string f in Floats) animator.SetFloat(f, source.GetFloat(f));
        animator.SetInteger("AttackAction", source.GetInteger("AttackAction"));
    }

    public void Pose(bool crouching, bool attacking, int attackAction, bool dashing)
    {
        if (animator == null) return;
        animator.SetBool("IsCrouching", crouching);
        animator.SetBool("IsAttacking", attacking);
        animator.SetInteger("AttackAction", attackAction);
        animator.SetBool("IsDashing", dashing);
        animator.SetBool("IsGrounded", true);
        animator.SetFloat("VelocityY", 0f);
        animator.SetFloat("AttackSpeedMul", 1f);
    }

    // ---- the strike ------------------------------------------------------------------------------
    /// <summary>
    /// The same cut the boss makes: travel `length` along `dir` at `speed`, hitting the player once.
    /// No physics body, so the transform is driven directly and walls are checked by ray.
    /// </summary>
    public IEnumerator Draw(float dir, float length, float speed, float damage, float knockback, float height)
    {
        Current = State.Striking;
        Face(dir > 0f);
        Pose(false, true, KagemushaBoss.SWIPE_ACTION, true);

        float travelled = 0f;
        bool struck = false;
        float halfW = 0.32f;
        while (travelled < length)
        {
            float step = speed * Time.fixedDeltaTime;
            Vector2 chest = (Vector2)transform.position + Vector2.up * 1.05f;
            if (Physics2D.Raycast(chest, new Vector2(dir, 0f), halfW + step + 0.2f, LayerMask.GetMask("Ground")).collider != null)
                break;

            transform.position += new Vector3(dir * step, 0f, 0f);
            travelled += step;

            if (!struck && EnemyMelee.TryHit(transform, dir, 1.5f, damage, knockback, height))
                struck = true;

            yield return new WaitForFixedUpdate();
        }

        Pose(false, false, 0, false);
        Current = State.Standing;
    }

    // ---- touch -----------------------------------------------------------------------------------
    private void OnTriggerEnter2D(Collider2D other) => Touch(other);
    private void OnTriggerStay2D(Collider2D other)  => Touch(other);

    private void Touch(Collider2D other)
    {
        if (owner == null || Current == State.Hidden || Current == State.Kneeling) return;
        if (!other.CompareTag("Player")) return;
        var pc = other.GetComponentInParent<PlayerController>();
        if (pc == null) return;

        if (Current == State.Armed)
        {
            // ⚠️ THE FIGHT'S ECONOMY. Touched before it swings, the fake is hollow: it bursts, the
            // shard goes home to the real one, and a crystal drops where it stood. See the doc §4.
            owner.OnDoubleShattered(this);
            return;
        }

        // Touched at or after the strike: it cuts like he does. Rate-limited, because a player
        // standing inside a Standing double would otherwise be hit every physics step.
        if (Time.time < nextTouchTime) return;
        nextTouchTime = Time.time + 0.6f;
        float dirX = Mathf.Sign(pc.transform.position.x - transform.position.x);
        if (Mathf.Approximately(dirX, 0f)) dirX = 1f;
        pc.TakeDamage(touchDamage);
        pc.ApplyKnockback(new Vector2(dirX * touchKnockback, touchKnockback * 0.6f));
    }
}
