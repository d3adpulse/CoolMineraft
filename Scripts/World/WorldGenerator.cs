using Godot;
using System.Text;

namespace VoxelCraft.World;

/// <summary>
/// Генератор рельефа на FastNoiseLite. Работает только с данными (BlockId[]),
/// ничего не создаёт в SceneTree — поэтому его можно вызывать из фонового потока.
/// </summary>
public partial class WorldGenerator : Node
{
    [Export] public FastNoiseLite HeightNoise;
    [Export] public FastNoiseLite DetailNoise;
    [Export] public FastNoiseLite CaveNoise;

    [Export] public int Seed = 1337;
    [Export] public int BaseHeight = 58;
    [Export] public int SeaLevel = 46;
    [Export] public int HeightAmplitude = 26;
    [Export] public int DirtDepth = 4;

    /// <summary>Печатать ASCII-карту высот при старте (этап 3).</summary>
    [Export] public bool PrintHeightMapOnStart = true;

    [Export] public int CaveThresholdPercent = 52; // 0..100, выше = меньше пещер

    public override void _Ready()
    {
        Configure(Seed);
    }

    public void Configure(int seed)
    {
        Seed = seed;

        HeightNoise ??= MakeNoise(0.0055f, 4);
        DetailNoise ??= MakeNoise(0.045f, 3);
        CaveNoise ??= MakeNoise3D(0.055f, 2);

        HeightNoise.Seed = seed;
        DetailNoise.Seed = seed + 101;
        CaveNoise.Seed = seed + 202;
    }

    private static FastNoiseLite MakeNoise(float frequency, int octaves)
    {
        return new FastNoiseLite
        {
            NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
            Frequency = frequency,
            FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
            FractalOctaves = octaves,
            FractalLacunarity = 2f,
            FractalGain = 0.5f,
        };
    }

    private static FastNoiseLite MakeNoise3D(float frequency, int octaves)
    {
        var noise = MakeNoise(frequency, octaves);
        noise.FractalType = FastNoiseLite.FractalTypeEnum.None;
        return noise;
    }

    /// <summary>Высота поверхности в мировых координатах.</summary>
    public int GetHeightAt(int worldX, int worldZ)
    {
        float height = BaseHeight;
        height += HeightNoise.GetNoise2D(worldX, worldZ) * HeightAmplitude;
        height += DetailNoise.GetNoise2D(worldX, worldZ) * 4f;
        return Mathf.Clamp(Mathf.RoundToInt(height), 3, Chunk.SizeY - 8);
    }

    /// <summary>Заполняет массив блоков чанка. Вызывается из фонового потока.</summary>
    public void FillChunk(BlockId[] blocks, Vector2I coord)
    {
        int worldX0 = coord.X * Chunk.SizeX;
        int worldZ0 = coord.Y * Chunk.SizeZ;
        float caveThreshold = CaveThresholdPercent / 100f;

        for (int x = 0; x < Chunk.SizeX; x++)
        {
            for (int z = 0; z < Chunk.SizeZ; z++)
            {
                int worldX = worldX0 + x;
                int worldZ = worldZ0 + z;
                int height = GetHeightAt(worldX, worldZ);
                bool beach = height <= SeaLevel + 2;

                for (int y = 0; y < Chunk.SizeY; y++)
                {
                    BlockId id;

                    if (y == 0)
                    {
                        id = BlockId.Stone; // дно мира
                    }
                    else if (y > height)
                    {
                        id = y <= SeaLevel ? BlockId.Water : BlockId.Air;
                    }
                    else if (y == height)
                    {
                        id = beach ? BlockId.Sand : BlockId.Grass;
                    }
                    else if (y > height - DirtDepth)
                    {
                        id = beach ? BlockId.Sand : BlockId.Dirt;
                    }
                    else
                    {
                        id = BlockId.Stone;
                    }

                    // Пещеры: 3D-шум вырезает пустоты внутри суши.
                    if (id != BlockId.Air && id != BlockId.Water && y > 1 && y < height - 1)
                    {
                        if (CaveNoise.GetNoise3D(worldX, y, worldZ) > caveThreshold)
                        {
                            id = BlockId.Air;
                        }
                    }

                    blocks[Chunk.Index(x, y, z)] = id;
                }
            }
        }
    }

    /// <summary>
    /// Этап 3: печатает карту высот 3x3 чанка в консоль ASCII-артом.
    /// Сразу видно, холмы это или белый шум.
    /// </summary>
    public void PrintHeightMap(int chunks = 3)
    {
        const string palette = " .:-=+*#%@";
        int half = chunks / 2;
        int width = chunks * Chunk.SizeX;
        int depth = chunks * Chunk.SizeZ;

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine($"=== Карта высот {chunks}x{chunks} чанка (seed {Seed}) ===");
        sb.AppendLine("    " + new string('-', width));

        for (int z = -half * Chunk.SizeZ; z < (chunks - half) * Chunk.SizeZ; z++)
        {
            sb.Append($"{z,4}");
            for (int x = -half * Chunk.SizeX; x < (chunks - half) * Chunk.SizeX; x++)
            {
                int h = GetHeightAt(x, z);
                int index = Mathf.Clamp((h - 20) * palette.Length / 70, 0, palette.Length - 1);
                sb.Append(palette[index]);
            }
            sb.AppendLine();
        }

        sb.AppendLine("Легенда: ' ' — низина/вода, '@' — высокие горы");
        GD.Print(sb.ToString());
    }
}
