using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class HandImpactUtilityTests
{
    [Test]
    public void GetDirection_ReturnsNormalizedVectorFromSourceToTarget()
    {
        Vector2 direction = HandImpactUtility.GetDirection(new Vector2(1f, 2f), new Vector2(4f, 6f));

        Assert.That(direction.x, Is.EqualTo(0.6f).Within(0.0001f));
        Assert.That(direction.y, Is.EqualTo(0.8f).Within(0.0001f));
    }

    [Test]
    public void GetDirection_WhenPositionsMatch_ReturnsFallbackRight()
    {
        Vector2 direction = HandImpactUtility.GetDirection(Vector2.one, Vector2.one);

        Assert.That(direction, Is.EqualTo(Vector2.right));
    }

    [Test]
    public void GetImpactPoint_ReturnsMidpointBetweenColliderCenters()
    {
        Vector2 point = HandImpactUtility.GetImpactPoint(new Vector2(-2f, 1f), new Vector2(4f, 3f));

        Assert.That(point.x, Is.EqualTo(1f).Within(0.0001f));
        Assert.That(point.y, Is.EqualTo(2f).Within(0.0001f));
    }

    [Test]
    public void CanTrigger_RespectsCooldownWindow()
    {
        Assert.That(HandImpactUtility.CanTrigger(1f, 0.8f, 0.25f), Is.False);
        Assert.That(HandImpactUtility.CanTrigger(1.1f, 0.8f, 0.25f), Is.True);
    }

    [Test]
    public void FireBurstPrefab_HasTekkenStyleCompositeParticleLayers()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/VFX/RPS_HandImpact_FireBurst.prefab");

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.transform.Find("Impact Flash"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Fire Cross"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Fire Slash Accent"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Radial Sparks"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Wide Ember Scatter"), Is.Not.Null);
        Assert.That(prefab.transform.Find("Directional Ember Burst"), Is.Not.Null);

        ParticleSystem[] particleSystems = prefab.GetComponentsInChildren<ParticleSystem>(true);
        Assert.That(particleSystems.Length, Is.GreaterThanOrEqualTo(10));

        int maxParticleBudget = 0;
        foreach (ParticleSystem particleSystem in particleSystems)
        {
            ParticleSystem.MainModule main = particleSystem.main;
            maxParticleBudget += main.maxParticles;
            Assert.That(main.duration, Is.LessThanOrEqualTo(1.2f), particleSystem.name);
        }

        Assert.That(maxParticleBudget, Is.GreaterThanOrEqualTo(500));
        Assert.That(prefab.transform.Find("Wide Ember Scatter").localScale.x, Is.GreaterThanOrEqualTo(1.8f));
    }

    [Test]
    public void FireBurstPrefab_HasDirectionalEmbersStartingNearImpactPoint()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/VFX/RPS_HandImpact_FireBurst.prefab");

        Assert.That(prefab, Is.Not.Null);

        Transform directionalEmbers = prefab.transform.Find("Directional Ember Burst");
        Assert.That(directionalEmbers, Is.Not.Null);
        Assert.That(directionalEmbers.localPosition.magnitude, Is.LessThanOrEqualTo(0.15f));
        Assert.That(directionalEmbers.localScale.x, Is.GreaterThanOrEqualTo(1.3f));

        ParticleSystem[] particleSystems = directionalEmbers.GetComponentsInChildren<ParticleSystem>(true);
        Assert.That(particleSystems.Length, Is.GreaterThanOrEqualTo(1));

        foreach (ParticleSystem particleSystem in particleSystems)
        {
            ParticleSystem.MainModule main = particleSystem.main;
            Assert.That(main.startSpeed.constantMax, Is.GreaterThanOrEqualTo(2.8f), particleSystem.name);
            Assert.That(main.maxParticles, Is.GreaterThanOrEqualTo(120), particleSystem.name);
        }
    }

    [Test]
    public void FireBurstPrefab_UsesBattleSortingLayerForAllParticleRenderers()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/VFX/RPS_HandImpact_FireBurst.prefab");

        Assert.That(prefab, Is.Not.Null);

        ParticleSystemRenderer[] renderers = prefab.GetComponentsInChildren<ParticleSystemRenderer>(true);
        Assert.That(renderers.Length, Is.GreaterThan(0));

        foreach (ParticleSystemRenderer renderer in renderers)
        {
            Assert.That(renderer.sortingLayerName, Is.EqualTo("Battle"), renderer.name);
            Assert.That(renderer.sortingLayerID, Is.EqualTo(SortingLayer.NameToID("Battle")), renderer.name);
        }
    }
}
