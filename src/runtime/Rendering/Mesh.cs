using System.Numerics;

namespace AssemblyEngine.Rendering;

public readonly record struct MeshVertex(Vector3 Position);

public sealed class Mesh
{
    private static readonly Lazy<Mesh> UnitCubeMesh = new(CreateUnitCube);

    public IReadOnlyList<MeshVertex> Vertices { get; }
    public IReadOnlyList<int> Indices { get; }

    public Mesh(IReadOnlyList<MeshVertex> vertices, IReadOnlyList<int> indices)
    {
        Vertices = vertices;
        Indices = indices;
    }

    public static Mesh CreateCube() => UnitCubeMesh.Value;

    private static Mesh CreateUnitCube()
    {
        MeshVertex[] vertices =
        [
            new(new Vector3(-0.5f, -0.5f, -0.5f)),
            new(new Vector3(0.5f, -0.5f, -0.5f)),
            new(new Vector3(0.5f, 0.5f, -0.5f)),
            new(new Vector3(-0.5f, 0.5f, -0.5f)),
            new(new Vector3(-0.5f, -0.5f, 0.5f)),
            new(new Vector3(0.5f, -0.5f, 0.5f)),
            new(new Vector3(0.5f, 0.5f, 0.5f)),
            new(new Vector3(-0.5f, 0.5f, 0.5f))
        ];

        int[] indices =
        [
            0, 1, 2, 0, 2, 3,
            4, 6, 5, 4, 7, 6,
            0, 4, 5, 0, 5, 1,
            3, 2, 6, 3, 6, 7,
            1, 5, 6, 1, 6, 2,
            0, 3, 7, 0, 7, 4
        ];

        return new Mesh(vertices, indices);
    }
}