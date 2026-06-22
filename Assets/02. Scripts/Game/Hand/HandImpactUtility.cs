using UnityEngine;

public static class HandImpactUtility
{
    public static Vector2 GetDirection(Vector2 sourcePosition, Vector2 targetPosition)
    {
        Vector2 direction = targetPosition - sourcePosition;
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            return Vector2.right;

        return direction.normalized;
    }

    public static Vector2 GetImpactPoint(Vector2 sourcePosition, Vector2 targetPosition)
    {
        return (sourcePosition + targetPosition) * 0.5f;
    }

    public static bool CanTrigger(float currentTime, float lastTriggerTime, float cooldown)
    {
        return currentTime - lastTriggerTime >= cooldown;
    }

    public static Quaternion GetRotation(Vector2 direction, float offsetDegrees)
    {
        if (direction.sqrMagnitude <= Mathf.Epsilon)
            direction = Vector2.right;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, 0f, angle + offsetDegrees);
    }
}
