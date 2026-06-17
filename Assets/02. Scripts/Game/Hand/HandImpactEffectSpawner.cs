using UnityEngine;

public class HandImpactEffectSpawner : MonoBehaviour
{
    [SerializeField] GameObject impactEffectPrefab;
    [SerializeField] float cooldown = 0.12f;
    [SerializeField] Vector2 offset;

    float lastSpawnTime = -999f;

    public bool TrySpawn(Vector2 impactPoint, Vector2 direction)
    {
        if (impactEffectPrefab == null)
            return false;

        if (!HandImpactUtility.CanTrigger(Time.time, lastSpawnTime, cooldown))
            return false;

        lastSpawnTime = Time.time;

        Vector3 position = new(impactPoint.x + offset.x, impactPoint.y + offset.y, 0f);
        Quaternion rotation = HandImpactUtility.GetRotation(direction, 0f);
        Instantiate(impactEffectPrefab, position, rotation);
        return true;
    }
}
