using UnityEngine;

public class HandImpactEffectSpawner : MonoBehaviour
{
    [SerializeField] GameObject impactEffectPrefab;
    [SerializeField] float cooldown = 0.12f;
    [SerializeField] float destroyAfterSeconds = 2f;
    [SerializeField] float rotationOffsetDegrees;
    [SerializeField] float zOffset = -0.5f;

    float lastSpawnTime = -999f;

    public bool TrySpawn(Vector2 impactPoint, Vector2 direction)
    {
        if (impactEffectPrefab == null)
            return false;

        if (!HandImpactUtility.CanTrigger(Time.time, lastSpawnTime, cooldown))
            return false;

        lastSpawnTime = Time.time;

        Vector3 position = new(impactPoint.x, impactPoint.y, zOffset);
        Quaternion rotation = HandImpactUtility.GetRotation(direction, rotationOffsetDegrees);
        GameObject instance = Instantiate(impactEffectPrefab, position, rotation);
        Destroy(instance, destroyAfterSeconds);
        return true;
    }
}
