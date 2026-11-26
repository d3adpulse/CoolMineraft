using Godot;
using System.Collections.Generic;

namespace VoxelCraft.World;

/// <summary>
/// Строит меш чанка. Два режима:
///   • greedy — склеивает соседние одинаковые грани в один квад (по умолчанию);
///   • per-face — по кваду на грань (простой эталон для сравнения).
///
/// Оба работают только с массивами, и их можно вызывать из фонового потока.
/// UV здесь — локальные координаты тайла (0..N), индекс тайла лежит в UV2.
/// Шейтер сам повторяет тайл, поэтому длинный greedy-квад выглядит правильно.
/// </summary>
public static class ChunkMeshBuilder
{
    /// <summary>Меняется в настройках; false = эталонный режим без склейки.</summary>
    public static bool UseGreedy = true;

    // Направления совпадают с индексами в таблицах ниже.
    // 0:+Z 1:-Z 2:+X 3:-X 4:+Y 5:-Y
    private static readonly Vector3I[] FaceOffsets =
    {
        new Vector3I(0, 0, 1),
        new Vector3I(0, 0, -1),
        new Vector3I(1, 0, 0),
        new Vector3I(-1, 0, 0),
        new Vector3I(0, 1, 0),
        new Vector3I(0, -1, 0),
    };

    private static readonly Vector3[] FaceNormals =
    {
        new Vector3(0, 0, 1),
        new Vector3(0, 0, -1),
        new Vector3(1, 0, 0),
        new Vector3(-1, 0, 0),
        new Vector3(0, 1, 0),
        new Vector3(0, -1, 0),
    };

    private static readonly float[] FaceShade = { 0.95f, 0.85f, 0.90f, 0.82f, 1.00f, 0.70f };

    /// <summary>
    /// Угол, от которого считается квад, и два ребра (U, V).
    /// Порядок TL → TR → BR → BL даёт индексы 0,1,2 / 0,2,3 (CW = лицевая сторона в Godot).
    /// Проверено по эталонной реализации voxel-greedy.
    /// </summary>
    private static readonly Vector3[] QuadBase =
    {
        new Vector3(0, 1, 1), // +Z
        new Vector3(1, 1, 0), // -Z
        new Vector3(1, 1, 1), // +X
        new Vector3(0, 1, 0), // -X
        new Vector3(0, 1, 0), // +Y
        new Vector3(0, 0, 1), // -Y
    };

    private static readonly Vector3[] QuadU =
    {
        new Vector3(1, 0, 0),
        new Vector3(-1, 0, 0),
        new Vector3(0, 0, -1),
        new Vector3(0, 0, 1),
        new Vector3(1, 0, 0),
        new Vector3(1, 0, 0),
    };

    private static readonly Vector3[] QuadV =
    {
        new Vector3(0, -1, 0),
        new Vector3(0, -1, 0),
        new Vector3(0, -1, 0),
        new Vector3(0, -1, 0),
        new Vector3(0, 0, 1),
        new Vector3(0, 0, -1),
    };

    private struct MaskCell
    {
        public BlockId Id;
        public int Tile;
    }

    private sealed class Buffers
    {
        public readonly List<Vector3> Vertices = new(8192);
        public readonly List<Vector3> Normals = new(8192);
        public readonly List<Vector2> Uvs = new(8192);
        public readonly List<Vector2> Uv2 = new(8192);
        public readonly List<Color> Colors = new(8192);
        public readonly List<int> Indices = new(12288);
        public readonly List<Vector3> Collision = new(12288);

        public ChunkMeshData ToData()
        {
            return new ChunkMeshData
            {
                Vertices = Vertices.ToArray(),
                Normals = Normals.ToArray(),
                Uvs = Uvs.ToArray(),
                Uv2 = Uv2.ToArray(),
                Colors = Colors.ToArray(),
                Indices = Indices.ToArray(),
                CollisionTriangles = Collision.ToArray(),
                VertexCount = Vertices.Count,
                IndexCount = Indices.Count,
            };
        }
    }

    public static ChunkMeshData Build(ChunkMeshInput input) =>
        UseGreedy ? BuildGreedy(input) : BuildPerFace(input);

    // ------------------------------------------------------- greedy meshing

    private static ChunkMeshData BuildGreedy(ChunkMeshInput input)
    {
        var buffers = new Buffers();

        for (int dir = 0; dir < 6; dir++)
        {
            int slices = dir < 2 ? Chunk.SizeZ : dir < 4 ? Chunk.SizeX : Chunk.SizeY;
            int width = dir < 2 ? Chunk.SizeX : dir < 4 ? Chunk.SizeZ : Chunk.SizeX;
            int height = dir < 2 ? Chunk.SizeY : dir < 4 ? Chunk.SizeY : Chunk.SizeZ;
            var mask = new MaskCell[width * height];

            for (int slice = 0; slice < slices; slice++)
            {
                FillMask(input, dir, slice, width, height, mask);
                EmitMerged(dir, slice, width, height, mask, buffers);
            }
        }

        return buffers.ToData();
    }

    private static void FillMask(
        ChunkMeshInput input, int dir, int slice, int width, int height, MaskCell[] mask)
    {
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                MapCell(dir, i, j, slice, out int x, out int y, out int z);

                var id = input.Blocks[Chunk.Index(x, y, z)];
                if (id == BlockId.Air)
                {
                    mask[j * width + i] = default;
                    continue;
                }

                var offset = FaceOffsets[dir];
                var neighbor = Sample(input, x + offset.X, y + offset.Y, z + offset.Z);
                if (!ShouldRenderFace(id, neighbor))
                {
                    mask[j * width + i] = default;
                    continue;
                }

                mask[j * width + i] = new MaskCell { Id = id, Tile = TileFor(dir, BlockDatabase.Get(id)) };
            }
        }
    }

    private static void EmitMerged(
        int dir, int slice, int width, int height, MaskCell[] mask, Buffers buffers)
    {
        for (int j = 0; j < height; j++)
        {
            for (int i = 0; i < width; i++)
            {
                var cell = mask[j * width + i];
                if (cell.Id == BlockId.Air) continue;

                int w = 1;
                while (i + w < width && Same(mask[j * width + i + w], cell)) w++;

                int h = 1;
                while (j + h < height && RowMatches(mask, width, i, j + h, w, cell)) h++;

                for (int jj = 0; jj < h; jj++)
                for (int ii = 0; ii < w; ii++)
                {
                    mask[(j + jj) * width + i + ii] = default;
                }

                MapCell(dir, i, j, slice, out int x, out int y, out int z);
                EmitQuad(buffers, dir, x, y, z, w, h, cell.Tile, cell.Id != BlockId.Water);
                i += w - 1;
            }
        }
    }

    private static bool Same(MaskCell a, MaskCell b) => a.Id == b.Id && a.Tile == b.Tile;

    private static bool RowMatches(MaskCell[] mask, int width, int i, int j, int w, MaskCell cell)
    {
        for (int k = 0; k < w; k++)
        {
            if (!Same(mask[j * width + i + k], cell)) return false;
        }
        return true;
    }

    /// <summary>Переводит координаты маски (i,j,срез) в координаты блока чанка.</summary>
    private static void MapCell(int dir, int i, int j, int slice, out int x, out int y, out int z)
    {
        switch (dir)
        {
            case 0: // +Z: U=+X, V=-Y
                x = i; y = Chunk.SizeY - 1 - j; z = slice;
                break;
            case 1: // -Z: U=-X, V=-Y
                x = Chunk.SizeX - 1 - i; y = Chunk.SizeY - 1 - j; z = slice;
                break;
            case 2: // +X: U=-Z, V=-Y
                x = slice; y = Chunk.SizeY - 1 - j; z = Chunk.SizeZ - 1 - i;
                break;
            case 3: // -X: U=+Z, V=-Y
                x = slice; y = Chunk.SizeY - 1 - j; z = i;
                break;
            case 4: // +Y: U=+X, V=+Z
                x = i; y = slice; z = j;
                break;
            default: // -Y: U=+X, V=-Z
                x = i; y = slice; z = Chunk.SizeZ - 1 - j;
                break;
        }
    }

    // ----------------------------------------------------- по одной грани

    private static ChunkMeshData BuildPerFace(ChunkMeshInput input)
    {
        var buffers = new Buffers();

        for (int y = 0; y < Chunk.SizeY; y++)
        for (int z = 0; z < Chunk.SizeZ; z++)
        for (int x = 0; x < Chunk.SizeX; x++)
        {
            var id = input.Blocks[Chunk.Index(x, y, z)];
            if (id == BlockId.Air) continue;

            var def = BlockDatabase.Get(id);
            if (def == null) continue;

            for (int dir = 0; dir < 6; dir++)
            {
                var offset = FaceOffsets[dir];
                var neighbor = Sample(input, x + offset.X, y + offset.Y, z + offset.Z);
                if (!ShouldRenderFace(id, neighbor)) continue;

                EmitQuad(buffers, dir, x, y, z, 1, 1, TileFor(dir, def), id != BlockId.Water);
            }
        }

        return buffers.ToData();
    }

    // -------------------------------------------------------------- helpers

    private static void EmitQuad(
        Buffers b, int dir, int x, int y, int z, int w, int h, int tile, bool collide)
    {
        Vector3 origin = new Vector3(x, y, z) + QuadBase[dir];
        Vector3 u = QuadU[dir] * w;
        Vector3 v = QuadV[dir] * h;

        Vector3 p0 = origin;
        Vector3 p1 = origin + u;
        Vector3 p2 = origin + u + v;
        Vector3 p3 = origin + v;

        int start = b.Vertices.Count;
        b.Vertices.Add(p0);
        b.Vertices.Add(p1);
        b.Vertices.Add(p2);
        b.Vertices.Add(p3);

        var normal = FaceNormals[dir];
        var shade = FaceShade[dir];
        var color = new Color(shade, shade, shade, 1f);
        var tileUv = new Vector2(tile, 0f);

        for (int i = 0; i < 4; i++)
        {
            b.Normals.Add(normal);
            b.Uvs.Add(i switch
            {
                0 => new Vector2(0f, 0f),
                1 => new Vector2(w, 0f),
                2 => new Vector2(w, h),
                _ => new Vector2(0f, h),
            });
            b.Uv2.Add(tileUv);
            b.Colors.Add(color);
        }

        b.Indices.Add(start);
        b.Indices.Add(start + 1);
        b.Indices.Add(start + 2);
        b.Indices.Add(start);
        b.Indices.Add(start + 2);
        b.Indices.Add(start + 3);

        // Коллизия: тот же квад, разбитый на два треугольника. Воду пропускаем —
        // иначе игрок ходит по поверхности воды.
        if (!collide) return;
        b.Collision.Add(p0); b.Collision.Add(p1); b.Collision.Add(p2);
        b.Collision.Add(p0); b.Collision.Add(p2); b.Collision.Add(p3);
    }

    private static int TileFor(int dir, BlockDef def) =>
        dir == 4 ? def.AtlasTop : dir == 5 ? def.AtlasBottom : def.AtlasSide;

    /// <summary>Грани между одинаковыми прозрачными блоками не рисуем.</summary>
    private static bool ShouldRenderFace(BlockId self, BlockId other)
    {
        if (other == BlockId.Air) return true;
        if (other == self) return false;
        if (BlockDatabase.IsOpaque(other)) return false;
        return BlockDatabase.IsOpaque(self);
    }

    private static BlockId Sample(ChunkMeshInput input, int x, int y, int z)
    {
        if (y < 0) return BlockId.Stone;
        if (y >= Chunk.SizeY) return BlockId.Air;

        if (x < 0)
        {
            return x >= -1 && input.NegX != null
                ? input.NegX[Chunk.Index(x + Chunk.SizeX, y, z)]
                : BlockId.Air;
        }

        if (x >= Chunk.SizeX)
        {
            return x <= Chunk.SizeX + 1 && input.PosX != null
                ? input.PosX[Chunk.Index(x - Chunk.SizeX, y, z)]
                : BlockId.Air;
        }

        if (z < 0)
        {
            return z >= -1 && input.NegZ != null
                ? input.NegZ[Chunk.Index(x, y, z + Chunk.SizeZ)]
                : BlockId.Air;
        }

        if (z >= Chunk.SizeZ)
        {
            return z <= Chunk.SizeZ + 1 && input.PosZ != null
                ? input.PosZ[Chunk.Index(x, y, z - Chunk.SizeZ)]
                : BlockId.Air;
        }

        return input.Blocks[Chunk.Index(x, y, z)];
    }
}
