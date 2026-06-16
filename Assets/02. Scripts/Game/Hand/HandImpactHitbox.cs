using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class HandImpactHitbox : MonoBehaviour
{
    [SerializeField] EHandImpactOwner owner;
    [SerializeField] HandImpactEffectSpawner effectSpawner;
    [SerializeField] bool canSpawnImpact = true;

    Collider2D hitbox;
    readonly HashSet<Collider2D> activeContacts = new();

    public EHandImpactOwner Owner => owner;
    public Vector2 Center => hitbox != null ? hitbox.bounds.center : transform.position;

    void Awake()
    {
        hitbox = GetComponent<Collider2D>();
        hitbox.isTrigger = true;

        if (effectSpawner == null)
            effectSpawner = GetComponentInParent<HandImpactEffectSpawner>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        TrySpawnImpact(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        TrySpawnImpact(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        activeContacts.Remove(other);
    }

    void TrySpawnImpact(Collider2D other)
    {
        if (!canSpawnImpact || effectSpawner == null)
            return;

        if (activeContacts.Contains(other))
            return;

        if (!other.TryGetComponent(out HandImpactHitbox otherHitbox))
            return;

        if (otherHitbox.Owner == owner)
            return;

        activeContacts.Add(other);
        Vector2 impactPoint = HandImpactUtility.GetImpactPoint(Center, otherHitbox.Center);
        Vector2 direction = HandImpactUtility.GetDirection(Center, otherHitbox.Center);
        effectSpawner.TrySpawn(impactPoint, direction);
    }
}
