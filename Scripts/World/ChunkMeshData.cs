using Godot;

namespace VoxelCraft.World;

/// <summary>
/// Снимок данных для построения меша: сам чанк + до четырёх соседей.
/// Массивы соседей — копии, поэтому объект можно безопасно отдавать в фоновый поток.
/// </summary>
public sealed class ChunkMeshInput
{
    public BlockId[] Blocks;

    public BlockId[] NegX;
    public BlockId[] PosX;
    public BlockId[] NegZ;
    public BlockId[] PosZ;
}

/// <summary>
/// Готовый «сырой» меш: обычные массивы, без объектов Godot.
/// Создаётся в фоновом потоке, а ArrayMesh из него собирается уже в главном.
/// </summary>
public sealed class ChunkMeshData
{
    public Vector3[] Vertices;
    public Vector3[] Normals;
    public Vector2[] Uvs;
    public Vector2[] Uv2;
    public Color[] Colors;
    public int[] Indices;

    /// <summary>Треугольники для коллизии «супом» (по 3 вершины). Готовятся в фоне.</summary>
    public Vector3[] CollisionTriangles;

    public int VertexCount;
    public int IndexCount;
}
