using System.Numerics;

namespace FpsSample;

internal static class FpsCollision
{
    public static bool CircleIntersectsBlockXZ(Vector3 position, float radius, FpsArenaBlock block)
    {
        var min = block.Min;
        var max = block.Max;
        var clampedX = Math.Clamp(position.X, min.X, max.X);
        var clampedZ = Math.Clamp(position.Z, min.Z, max.Z);
        var dx = position.X - clampedX;
        var dz = position.Z - clampedZ;
        return ((dx * dx) + (dz * dz)) <= (radius * radius);
    }

    public static bool TryRaySphere(Vector3 origin, Vector3 direction, Vector3 center, float radius, float maxDistance, out float distance)
    {
        distance = 0f;
        var offset = center - origin;
        var projection = Vector3.Dot(offset, direction);
        if (projection < 0f)
            return false;

        var closestDistanceSquared = offset.LengthSquared() - (projection * projection);
        var radiusSquared = radius * radius;
        if (closestDistanceSquared > radiusSquared)
            return false;

        var thc = MathF.Sqrt(Math.Max(0f, radiusSquared - closestDistanceSquared));
        var near = projection - thc;
        var far = projection + thc;
        var hitDistance = near >= 0f ? near : far;
        if (hitDistance < 0f || hitDistance > maxDistance)
            return false;

        distance = hitDistance;
        return true;
    }

    public static bool TryRayBlock(Vector3 origin, Vector3 direction, FpsArenaBlock block, float maxDistance, out float distance) =>
        TryRayAabb(origin, direction, block.Min, block.Max, maxDistance, out distance);

    private static bool TryRayAabb(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max, float maxDistance, out float distance)
    {
        distance = 0f;
        var tMin = 0f;
        var tMax = maxDistance;

        if (!UpdateSlab(origin.X, direction.X, min.X, max.X, ref tMin, ref tMax) ||
            !UpdateSlab(origin.Y, direction.Y, min.Y, max.Y, ref tMin, ref tMax) ||
            !UpdateSlab(origin.Z, direction.Z, min.Z, max.Z, ref tMin, ref tMax))
        {
            return false;
        }

        distance = tMin;
        return tMin <= maxDistance && tMax >= 0f;
    }

    private static bool UpdateSlab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
    {
        const float Epsilon = 0.0001f;
        if (MathF.Abs(direction) < Epsilon)
            return origin >= min && origin <= max;

        var inverseDirection = 1f / direction;
        var near = (min - origin) * inverseDirection;
        var far = (max - origin) * inverseDirection;
        if (near > far)
            (near, far) = (far, near);

        tMin = Math.Max(tMin, near);
        tMax = Math.Min(tMax, far);
        return tMin <= tMax;
    }
}