using Godot;

namespace VoxelCraft.World;

/// <summary>
/// Один чанк: массив блоков + узел MeshInstance3D + коллизия.
/// Данные (BlockId[]) — «источник правды», меш всегда строится из них.
/// </summary>
public partial class Chunk : Node3D
{
    public const int SizeX = 16;
    public const int SizeY = 128;
    public const int SizeZ = 16;
    public const int Volume = SizeX * SizeY * SizeZ; // 32768 байт на чанк

    public static readonly Vector3I Size = new(SizeX, SizeY, SizeZ);

    public Vector2I Coord { get; private set; }
    public BlockId[] Blocks { get; private set; }
    public MeshInstance3D MeshInstance { get; private set; }
    public StaticBody3D Body { get; private set; }
    public CollisionShape3D Collision { get; private set; }

    public int VertexCount { get; private set; }
    public int TriangleCount { get; private set; }

    /// <summary>Плоский индекс в массиве блоков.</summary>
    public static int Index(int x, int y, int z) => x + SizeX * (z + SizeZ * y);

    public static bool InBounds(int x, int y, int z) =>
        x >= 0 && x < SizeX && y >= 0 && y < SizeY && z >= 0 && z < SizeZ;

    public void Init(Vector2I coord, BlockId[] blocks)
    {
        Coord = coord;
        Blocks = blocks;
        Name = $"Chunk_{coord.X}_{coord.Y}";
        Position = new Vector3(coord.X * SizeX, 0f, coord.Y * SizeZ);

        MeshInstance = new MeshInstance3D
        {
            Name = "Mesh",
            MaterialOverride = BlockDatabase.TerrainMaterial,
        };
        AddChild(MeshInstance);

        Body = new StaticBody3D { Name = "Body" };
        Collision = new CollisionShape3D { Name = "Collision" };
        Body.AddChild(Collision);
        AddChild(Body);
    }

    public BlockId GetBlockRaw(int x, int y, int z) => Blocks[Index(x, y, z)];

    public void SetBlockRaw(int x, int y, int z, BlockId id) => Blocks[Index(x, y, z)] = id;

    /// <summary>
    /// Чтение блока с выходом за границы чанка.
    /// Снизу — «дно мира» (камень), сверху — «небо» (воздух),
    /// по X/Z — спрашиваем ChunkManager (соседний чанк).
    /// </summary>
    public BlockId GetBlock(int x, int y, int z)
    {
        if (y < 0) return BlockId.Stone;
        if (y >= SizeY) return BlockId.Air;
        if (InBounds(x, y, z)) return Blocks[Index(x, y, z)];

        int worldX = Coord.X * SizeX + x;
        int worldZ = Coord.Y * SizeZ + z;
        var manager = ChunkManager.Instance;
        return manager != null ? manager.GetBlock(new Vector3I(worldX, y, worldZ)) : BlockId.Air;
    }

    /// <summary>
    /// Применяет готовый меш. Вызывать ТОЛЬКО в главном потоке:
    /// здесь создаются ArrayMesh и CollisionShape3D.
    /// </summary>
    public void ApplyMesh(ChunkMeshData data, bool buildCollision)
    {
        if (data == null || data.IndexCount == 0)
        {
            MeshInstance.Mesh = null;
            Collision.Shape = null;
            VertexCount = 0;
            TriangleCount = 0;
            return;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = data.Vertices;
        arrays[(int)Mesh.ArrayType.Normal] = data.Normals;
        arrays[(int)Mesh.ArrayType.TexUV] = data.Uvs;
        arrays[(int)Mesh.ArrayType.TexUV2] = data.Uv2;
        arrays[(int)Mesh.ArrayType.Color] = data.Colors;
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        MeshInstance.Mesh = mesh;

        // Треугольники коллизии уже посчитаны в фоновом потоке.
        // Здесь только создаём ресурс — это почти бесплатно и не вызывает фризов.
        if (buildCollision && data.CollisionTriangles != null && data.CollisionTriangles.Length > 0)
        {
            Collision.Shape = new ConcavePolygonShape3D { Data = data.CollisionTriangles };
        }
        else
        {
            Collision.Shape = null;
        }

        VertexCount = data.VertexCount;
        TriangleCount = data.IndexCount / 3;
    }
}
