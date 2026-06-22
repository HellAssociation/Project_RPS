using System.Collections;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class HandImpactPlayModeTests
{
    const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnityTest]
    public IEnumerator OpposingHitboxes_SpawnImpactEffectWhenTheyOverlapInPlayMode()
    {
        GameObject effectPrefab = CreateEffectPrefab();
        Type spawnerType = RequireType("HandImpactEffectSpawner");
        Type hitboxType = RequireType("HandImpactHitbox");
        Type ownerType = RequireType("EHandImpactOwner");

        GameObject playerRoot = CreateHand("TestPlayerHand", spawnerType, hitboxType, ownerType, 0, Vector2.zero, true, effectPrefab, out _);
        GameObject enemyRoot = CreateHand("TestEnemyHand", spawnerType, hitboxType, ownerType, 1, new Vector2(6f, 0f), false, effectPrefab, out Rigidbody2D enemyBody);

        yield return new WaitForFixedUpdate();

        enemyBody.MovePosition(new Vector2(1f, 0f));

        yield return new WaitForFixedUpdate();
        yield return null;

        GameObject spawned = FindSpawnedEffect();
        Assert.That(spawned, Is.Not.Null);
        Assert.That(spawned.transform.position.x, Is.EqualTo(0.5f).Within(0.1f));
        Assert.That(spawned.transform.position.y, Is.EqualTo(0f).Within(0.1f));
        Assert.That(Mathf.DeltaAngle(0f, spawned.transform.eulerAngles.z), Is.EqualTo(0f).Within(0.1f));

        UnityEngine.Object.Destroy(playerRoot);
        UnityEngine.Object.Destroy(enemyRoot);
        UnityEngine.Object.Destroy(effectPrefab);
        UnityEngine.Object.Destroy(spawned);
    }

    [UnityTest]
    public IEnumerator StayingOverlapped_DoesNotSpawnRepeatedImpactEffects()
    {
        GameObject effectPrefab = CreateEffectPrefab();
        Type spawnerType = RequireType("HandImpactEffectSpawner");
        Type hitboxType = RequireType("HandImpactHitbox");
        Type ownerType = RequireType("EHandImpactOwner");

        GameObject playerRoot = CreateHand("TestPlayerHand", spawnerType, hitboxType, ownerType, 0, Vector2.zero, true, effectPrefab, out _);
        GameObject enemyRoot = CreateHand("TestEnemyHand", spawnerType, hitboxType, ownerType, 1, new Vector2(6f, 0f), false, effectPrefab, out Rigidbody2D enemyBody);

        yield return new WaitForFixedUpdate();

        enemyBody.MovePosition(new Vector2(1f, 0f));

        for (int i = 0; i < 6; i++)
            yield return new WaitForFixedUpdate();

        Assert.That(CountSpawnedEffects(), Is.EqualTo(1));

        UnityEngine.Object.Destroy(playerRoot);
        UnityEngine.Object.Destroy(enemyRoot);
        UnityEngine.Object.Destroy(effectPrefab);
        DestroySpawnedEffects();
    }

    [UnityTest]
    public IEnumerator FireBurstPrefab_SpawnsWhenHitboxesOverlapInPlayMode()
    {
        GameObject effectPrefab = Resources.Load<GameObject>("VFX/RPS_HandImpact_FireBurst");
        Assert.That(effectPrefab, Is.Not.Null);

        Type spawnerType = RequireType("HandImpactEffectSpawner");
        Type hitboxType = RequireType("HandImpactHitbox");
        Type ownerType = RequireType("EHandImpactOwner");

        GameObject playerRoot = CreateHand("TestPlayerHand", spawnerType, hitboxType, ownerType, 0, Vector2.zero, true, effectPrefab, out _);
        GameObject enemyRoot = CreateHand("TestEnemyHand", spawnerType, hitboxType, ownerType, 1, new Vector2(6f, 0f), false, effectPrefab, out Rigidbody2D enemyBody);

        yield return new WaitForFixedUpdate();

        enemyBody.MovePosition(new Vector2(1f, 0f));

        yield return new WaitForFixedUpdate();
        yield return null;

        GameObject spawned = FindSpawnedEffect("RPS_HandImpact_FireBurst(Clone)");
        Assert.That(spawned, Is.Not.Null);
        Assert.That(spawned.transform.position.x, Is.EqualTo(0.5f).Within(0.1f));
        Assert.That(spawned.transform.position.y, Is.EqualTo(0f).Within(0.1f));
        Assert.That(spawned.GetComponentsInChildren<ParticleSystem>(true).Length, Is.GreaterThanOrEqualTo(10));
        Assert.That(spawned.transform.Find("Impact Flash"), Is.Not.Null);
        Assert.That(spawned.transform.Find("Fire Cross"), Is.Not.Null);
        Assert.That(spawned.transform.Find("Wide Ember Scatter"), Is.Not.Null);
        Assert.That(spawned.transform.Find("Directional Ember Burst"), Is.Not.Null);

        Bounds visualBounds = GetCombinedRendererBounds(spawned);
        Assert.That(Mathf.Max(visualBounds.size.x, visualBounds.size.y), Is.GreaterThanOrEqualTo(5.5f));

        Transform directionalEmbers = spawned.transform.Find("Directional Ember Burst");
        Assert.That(directionalEmbers.position.x, Is.GreaterThan(spawned.transform.position.x));

        UnityEngine.Object.Destroy(playerRoot);
        UnityEngine.Object.Destroy(enemyRoot);
        UnityEngine.Object.Destroy(spawned);
    }

    [UnityTest]
    public IEnumerator ImpactEffect_DoesNotApplyDirectionalParticleVelocity()
    {
        GameObject effectPrefab = CreateEffectPrefab();
        Type spawnerType = RequireType("HandImpactEffectSpawner");
        Type hitboxType = RequireType("HandImpactHitbox");
        Type ownerType = RequireType("EHandImpactOwner");

        GameObject playerRoot = CreateHand("TestPlayerHand", spawnerType, hitboxType, ownerType, 0, Vector2.zero, true, effectPrefab, out _);
        GameObject enemyRoot = CreateHand("TestEnemyHand", spawnerType, hitboxType, ownerType, 1, new Vector2(6f, 0f), false, effectPrefab, out Rigidbody2D enemyBody);

        yield return new WaitForFixedUpdate();

        enemyBody.MovePosition(new Vector2(1f, 0f));

        yield return new WaitForFixedUpdate();
        yield return null;

        GameObject spawned = FindSpawnedEffect();
        Assert.That(spawned, Is.Not.Null);

        ParticleSystem particleSystem = spawned.GetComponent<ParticleSystem>();
        Assert.That(particleSystem, Is.Not.Null);
        Assert.That(particleSystem.velocityOverLifetime.enabled, Is.False);

        UnityEngine.Object.Destroy(playerRoot);
        UnityEngine.Object.Destroy(enemyRoot);
        UnityEngine.Object.Destroy(effectPrefab);
        UnityEngine.Object.Destroy(spawned);
    }

    static GameObject CreateEffectPrefab()
    {
        GameObject prefab = new("CFXR2 Hit PlayMode Test");
        prefab.AddComponent<ParticleSystem>();
        return prefab;
    }

    static GameObject CreateHand(
        string name,
        Type spawnerType,
        Type hitboxType,
        Type ownerType,
        int ownerValue,
        Vector2 position,
        bool canSpawn,
        GameObject effectPrefab,
        out Rigidbody2D body)
    {
        GameObject root = new(name);
        root.transform.position = position;

        body = root.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;

        Component spawner = root.AddComponent(spawnerType);
        SetField(spawner, "impactEffectPrefab", effectPrefab);
        SetField(spawner, "cooldown", 0f);
        SetField(spawner, "destroyAfterSeconds", 5f);

        GameObject hitboxObject = new("ImpactHitbox");
        hitboxObject.transform.SetParent(root.transform, false);
        BoxCollider2D collider = hitboxObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(2f, 2f);

        Component hitbox = hitboxObject.AddComponent(hitboxType);
        SetField(hitbox, "owner", Enum.ToObject(ownerType, ownerValue));
        SetField(hitbox, "effectSpawner", spawner);
        SetField(hitbox, "canSpawnImpact", canSpawn);

        return root;
    }

    static Type RequireType(string typeName)
    {
        Type type = Type.GetType(typeName + ", Assembly-CSharp");
        Assert.That(type, Is.Not.Null, typeName);
        return type;
    }

    static void SetField<T>(object target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, FieldFlags);
        Assert.That(field, Is.Not.Null, fieldName);
        field.SetValue(target, value);
    }

    static int CountSpawnedEffects()
    {
        int count = 0;
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject obj in objects)
        {
            if (obj.name == "CFXR2 Hit PlayMode Test(Clone)")
                count++;
        }

        return count;
    }

    static GameObject FindSpawnedEffect()
    {
        return FindSpawnedEffect("CFXR2 Hit PlayMode Test(Clone)");
    }

    static GameObject FindSpawnedEffect(string objectName)
    {
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject obj in objects)
        {
            if (obj.name == objectName)
                return obj;
        }

        return null;
    }

    static Bounds GetCombinedRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Assert.That(renderers.Length, Is.GreaterThan(0));

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return bounds;
    }

    static void DestroySpawnedEffects()
    {
        GameObject[] objects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (GameObject obj in objects)
        {
            if (obj.name == "CFXR2 Hit PlayMode Test(Clone)")
                UnityEngine.Object.Destroy(obj);
        }
    }
}
