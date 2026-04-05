using System.Numerics;

namespace AssemblyEngine.Rendering;

public sealed class Camera3D
{
    public Vector3 Position { get; set; } = new(0f, 0f, 4f);
    public Vector3 Target { get; set; } = Vector3.Zero;
    public Vector3 Up { get; set; } = Vector3.UnitY;
    public float FieldOfView { get; set; } = MathF.PI / 3f;
    public float NearPlane { get; set; } = 0.1f;
    public float FarPlane { get; set; } = 100f;

    internal Matrix4x4 CreateViewMatrix() => Matrix4x4.CreateLookAt(Position, Target, Up);

    internal Matrix4x4 CreateProjectionMatrix(float aspectRatio)
    {
        var clampedAspect = aspectRatio <= 0f ? 1f : aspectRatio;
        return Matrix4x4.CreatePerspectiveFieldOfView(
            Math.Clamp(FieldOfView, 0.1f, MathF.PI - 0.1f),
            clampedAspect,
            Math.Max(0.001f, NearPlane),
            Math.Max(NearPlane + 0.001f, FarPlane));
    }
}