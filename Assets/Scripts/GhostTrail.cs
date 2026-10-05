using UnityEngine;

/// <summary>
/// Freezes a Cainos character rig at one instant into a translucent copy that fades and removes
/// itself. Drop a few of these along a dash and you get an afterimage trail.
///
/// ⚠️ THE BODY IS 16 SkinnedMeshRenderers AND ONLY THE WEAPON IS A SpriteRenderer. A ghost built
/// from SpriteRenderers alone produces a floating WEAPON and no character — the same mistake that
/// made the gravity-reversal flash invisible for months. Skinned parts must go through
/// `SkinnedMeshRenderer.BakeMesh`, which is what CardAimIndicator's dash preview already does.
///
/// ⚠️ AND THE GHOSTS CANNOT REUSE THE RIG'S MATERIALS. The pack's "Alpha Cut" and "Body" shaders
/// expose no `_Color` at all, so tinting or fading through them is a silent no-op. Each ghost gets a
/// plain `Sprites/Default` material carrying the source's `_MainTex`, which is tintable and blends.
/// </summary>
public class GhostTrail : MonoBehaviour
{
    private Renderer[] parts;
    private MaterialPropertyBlock block;
    private float life;
    private float age;
    private Color tint;
    private bool fadeIn;

    /// <summary>
    /// Snapshot `rigRoot` where it stands right now. Returns the ghost, or null if empty.
    /// </summary>
    /// <param name="fadeIn">
    /// Reverses the fade: the ghost RESOLVES over its lifetime instead of dissipating, then removes
    /// itself at the end exactly as usual.
    ///
    /// ⚠️ This is not a cosmetic option — it changes what a ghost MEANS. A ghost that fades out is
    /// somewhere he WAS; a ghost that fades in is somewhere he is ABOUT TO BE. The dash telegraph
    /// uses it to stand a premonition of him at the end of the lane, arriving fully just as he
    /// launches. Because the lifetime still governs it, the caller cannot leak one.
    /// </param>
    public static GhostTrail Snapshot(Transform rigRoot, Color tint, float life, int sortingOrder = 4,
                                      bool fadeIn = false)
    {
        if (rigRoot == null) return null;

        var root = new GameObject("Ghost");
        root.transform.position = rigRoot.position;
        root.transform.rotation = rigRoot.rotation;
        root.AddComponent<TemporaryObject>();

        var made = new System.Collections.Generic.List<Renderer>();

        foreach (var smr in rigRoot.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (!smr.enabled || !smr.gameObject.activeInHierarchy) continue;

            var mesh = new Mesh();
            smr.BakeMesh(mesh, true);          // `true` bakes in the transform scale, carrying the facing flip

            var go = new GameObject(smr.name);
            go.transform.SetParent(root.transform, false);
            // BakeMesh output is already in the renderer's own space, so the copy takes the
            // renderer's world POSITION and ROTATION but never its scale — that is baked in already,
            // and applying it again doubles the flip and turns the ghost inside out.
            go.transform.position = smr.transform.position;
            go.transform.rotation = smr.transform.rotation;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = BuildGhostMaterial(smr.sharedMaterial);
            mr.sortingLayerID = smr.sortingLayerID;
            mr.sortingOrder = sortingOrder;
            made.Add(mr);
        }

        foreach (var sr in rigRoot.GetComponentsInChildren<SpriteRenderer>(false))
        {
            if (!sr.enabled || sr.sprite == null || !sr.gameObject.activeInHierarchy) continue;

            var go = new GameObject(sr.name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = sr.transform.position;
            go.transform.rotation = sr.transform.rotation;
            go.transform.localScale = sr.transform.lossyScale;

            var copy = go.AddComponent<SpriteRenderer>();
            copy.sprite = sr.sprite;
            copy.flipX = sr.flipX;
            copy.flipY = sr.flipY;
            copy.sortingLayerID = sr.sortingLayerID;
            copy.sortingOrder = sortingOrder;
            made.Add(copy);
        }

        if (made.Count == 0) { Destroy(root); return null; }

        var g = root.AddComponent<GhostTrail>();
        g.parts = made.ToArray();
        g.life = Mathf.Max(0.01f, life);
        g.tint = tint;
        g.fadeIn = fadeIn;
        g.block = new MaterialPropertyBlock();
        g.Apply(fadeIn ? 0f : 1f);
        return g;
    }

    private static Material BuildGhostMaterial(Material source)
    {
        var m = new Material(Shader.Find("Sprites/Default"));
        if (source != null && source.HasProperty("_MainTex"))
            m.mainTexture = source.mainTexture;
        return m;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float k = Mathf.Clamp01(age / life);
        if (k >= 1f) { Destroy(gameObject); return; }
        // Out: fades fast at the end so the tail does not linger as a smear.
        // In:  resolves fast at the start so it is READABLE immediately — a telegraph that is still
        //      arriving halfway through the wind-up has spent half its warning saying nothing.
        Apply(fadeIn ? Mathf.Sqrt(k) : 1f - k * k);
    }

    private void Apply(float fade)
    {
        Color c = new Color(tint.r, tint.g, tint.b, tint.a * fade);
        foreach (var r in parts)
        {
            if (r == null) continue;
            if (r is SpriteRenderer sr) { sr.color = c; continue; }
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }
    }

    private void OnDestroy()
    {
        // Both the baked meshes and the per-ghost materials are created at runtime and owned by
        // nothing else, so they leak unless explicitly released.
        foreach (var r in parts)
        {
            if (r == null) continue;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
            if (r is MeshRenderer && r.sharedMaterial != null) Destroy(r.sharedMaterial);
        }
    }
}
