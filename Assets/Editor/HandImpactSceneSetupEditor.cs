using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HandImpactSceneSetupEditor
{
    const string EffectPath = "Assets/Resources/VFX/RPS_HandImpact_FireBurst.prefab";
    const string HitboxName = "ImpactHitbox";

    [MenuItem("Tools/Project RPS/Configure Hand Impact Hitboxes")]
    public static void ConfigureActiveScene()
    {
        GameObject effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath);
        if (effectPrefab == null)
        {
            Debug.LogError($"[HandImpactSceneSetup] Missing effect prefab: {EffectPath}");
            return;
        }

        GameObject playerRoot = FindRootWithComponent<PlayerHand>();
        GameObject enemyRoot = FindRootWithComponent<EnemyHand>();

        if (playerRoot == null || enemyRoot == null)
        {
            Debug.LogError($"[HandImpactSceneSetup] Missing hand roots. player={playerRoot != null}, enemy={enemyRoot != null}");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(playerRoot, "Configure Player Hand Impact");
        Undo.RegisterFullObjectHierarchyUndo(enemyRoot, "Configure Enemy Hand Impact");

        HandImpactEffectSpawner playerSpawner = ConfigurePlayerHand(playerRoot, effectPrefab);
        ConfigureHandHitbox(playerRoot, EHandImpactOwner.Player, true, playerSpawner);
        ConfigureRigidbody(playerRoot);

        ConfigureHandHitbox(enemyRoot, EHandImpactOwner.Enemy, false, playerSpawner);
        ConfigureRigidbody(enemyRoot);

        EditorUtility.SetDirty(playerRoot);
        EditorUtility.SetDirty(enemyRoot);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log($"[HandImpactSceneSetup] Configured {playerRoot.name} and {enemyRoot.name} with {EffectPath}");
    }

    static GameObject FindRootWithComponent<T>() where T : Component
    {
        T[] components = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (T component in components)
        {
            if (component.transform.parent == null)
                return component.gameObject;
        }

        return components.Length > 0 ? components[0].gameObject : null;
    }

    static HandImpactEffectSpawner ConfigurePlayerHand(GameObject root, GameObject effectPrefab)
    {
        HandImpactEffectSpawner spawner = root.GetComponent<HandImpactEffectSpawner>();
        if (spawner == null)
            spawner = root.AddComponent<HandImpactEffectSpawner>();

        SetObject(spawner, "impactEffectPrefab", effectPrefab);
        SetFloat(spawner, "cooldown", 0.12f);
        SetFloat(spawner, "destroyAfterSeconds", 2f);
        SetFloat(spawner, "rotationOffsetDegrees", 0f);
        SetFloat(spawner, "zOffset", -0.5f);
        return spawner;
    }

    static void ConfigureRigidbody(GameObject root)
    {
        Rigidbody2D body = root.GetComponent<Rigidbody2D>();
        if (body == null)
            body = root.AddComponent<Rigidbody2D>();

        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.freezeRotation = true;
    }

    static void ConfigureHandHitbox(GameObject root, EHandImpactOwner owner, bool canSpawnImpact, HandImpactEffectSpawner spawner)
    {
        GameObject hitboxObject = FindOrCreateChild(root.transform, HitboxName);
        SpriteRenderer spriteRenderer = root.GetComponent<SpriteRenderer>();

        Vector2 size = new(3f, 3f);
        Vector3 localCenter = Vector3.zero;
        if (spriteRenderer != null && spriteRenderer.sprite != null)
        {
            Bounds localBounds = spriteRenderer.localBounds;
            size = new Vector2(localBounds.size.x * 0.75f, localBounds.size.y * 0.75f);
            localCenter = localBounds.center;
        }

        hitboxObject.transform.localPosition = localCenter;
        hitboxObject.transform.localRotation = Quaternion.identity;
        hitboxObject.transform.localScale = Vector3.one;
        hitboxObject.layer = root.layer;

        BoxCollider2D collider = hitboxObject.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = hitboxObject.AddComponent<BoxCollider2D>();

        collider.isTrigger = true;
        collider.size = size;
        collider.offset = Vector2.zero;

        HandImpactHitbox hitbox = hitboxObject.GetComponent<HandImpactHitbox>();
        if (hitbox == null)
            hitbox = hitboxObject.AddComponent<HandImpactHitbox>();

        SetEnum(hitbox, "owner", (int)owner);
        SetObject(hitbox, "effectSpawner", spawner);
        SetBool(hitbox, "canSpawnImpact", canSpawnImpact);
    }

    static GameObject FindOrCreateChild(Transform parent, string childName)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
            return existing.gameObject;

        GameObject child = new(childName);
        child.transform.SetParent(parent, false);
        return child;
    }

    static void SetObject(Object target, string propertyName, Object value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null) return;
        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetFloat(Object target, string propertyName, float value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null) return;
        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetBool(Object target, string propertyName, bool value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null) return;
        property.boolValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetEnum(Object target, string propertyName, int value)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null) return;
        property.enumValueIndex = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
