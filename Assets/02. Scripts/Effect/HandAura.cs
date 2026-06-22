using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class HandAura : MonoBehaviour
{
    [Header("Color")]
    [ColorUsage(true, true)] [SerializeField] Color auraColor = new Color(0.25f, 0.6f, 1f, 1f);
    [SerializeField] float rimIntensity = 3f;
    [SerializeField] float flameIntensity = 2f;

    [Header("Rim (duplicate silhouette glow)")]
    [SerializeField] Transform sourceRootOverride;
    [SerializeField] float rimScale = 1.06f;
    [SerializeField] int rimSortingOrder = -1;
    [SerializeField] string rimShaderName = "RPS/AuraSilhouette";

    [Header("Flame (particles)")]
    [SerializeField] ParticleSystem flame;
    [SerializeField] int flameSortingOrder = -10;

    Material rimMaterial;
    readonly List<RimClone> clones = new();

    struct RimClone
    {
        public SpriteRenderer src;
        public SpriteRenderer dst;
    }

    void OnEnable()
    {
        BuildRim();
        ApplyColor();
        ApplyFlameSorting();
        if (flame != null) flame.Play(true);
    }

    void OnDisable()
    {
        if (flame != null) flame.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        DestroyRim();
    }

    void OnDestroy()
    {
        if (rimMaterial != null) Destroy(rimMaterial);
    }

    Transform SourceRoot
    {
        get
        {
            if (sourceRootOverride != null) return sourceRootOverride;
            return transform.parent != null ? transform.parent : transform;
        }
    }

    void BuildRim()
    {
        DestroyRim();

        if (rimMaterial == null)
        {
            var shader = Shader.Find(rimShaderName);
            if (shader == null)
            {
                Debug.LogError($"[HandAura] Shader not found: {rimShaderName}");
                return;
            }
            rimMaterial = new Material(shader) { hideFlags = HideFlags.DontSave };
        }

        var sources = SourceRoot.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var src in sources)
        {
            if (src.transform.IsChildOf(transform)) continue;
            if (src.GetComponent<AuraRimTag>() != null) continue;
            if (src.sharedMaterial == rimMaterial) continue;

            var go = new GameObject("AuraRim");
            go.transform.SetParent(src.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = RimScaleFor(src);
            go.AddComponent<AuraRimTag>();

            var dst = go.AddComponent<SpriteRenderer>();
            dst.sharedMaterial = rimMaterial;
            dst.sortingLayerID = src.sortingLayerID;
            dst.sortingOrder = rimSortingOrder;
            dst.sprite = src.sprite;

            clones.Add(new RimClone { src = src, dst = dst });
        }
    }

    void DestroyRim()
    {
        for (int i = 0; i < clones.Count; i++)
        {
            if (clones[i].dst != null) Destroy(clones[i].dst.gameObject);
        }
        clones.Clear();
    }

    void ApplyColor()
    {
        if (rimMaterial != null) rimMaterial.SetColor("_Color", Tint(rimIntensity));

        if (flame != null)
        {
            var tint = new ParticleSystem.MinMaxGradient(Tint(flameIntensity));
            foreach (var ps in flame.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.startColor = tint;
            }
        }
    }

    void ApplyFlameSorting()
    {
        if (flame == null) return;

        int layerId = clones.Count > 0 ? clones[0].src.sortingLayerID : 0;
        foreach (var psr in flame.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            psr.sortingLayerID = layerId;
            psr.sortingOrder = flameSortingOrder;
        }
    }

    Color Tint(float intensity)
    {
        Color c = auraColor * intensity;
        c.a = auraColor.a;
        return c;
    }

    Vector3 RimScaleFor(SpriteRenderer src)
    {
        return new Vector3(
            rimScale * (src.flipX ? -1f : 1f),
            rimScale * (src.flipY ? -1f : 1f),
            rimScale);
    }

    void LateUpdate()
    {
        for (int i = 0; i < clones.Count; i++)
        {
            var c = clones[i];
            if (c.src == null || c.dst == null) continue;

            c.dst.sprite = c.src.sprite;
            c.dst.transform.localScale = RimScaleFor(c.src);
            c.dst.enabled = c.src.enabled && c.src.sprite != null;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying || rimMaterial == null) return;
        rimMaterial.SetColor("_Color", Tint(rimIntensity));
        ApplyColor();
    }
#endif
}

public class AuraRimTag : MonoBehaviour { }
