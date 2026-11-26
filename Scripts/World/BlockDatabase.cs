using Godot;
using System.Collections.Generic;

namespace VoxelCraft.World;

/// <summary>
/// Автозагрузка (Singletons → BlockDatabase). Держит таблицу всех типов блоков,
/// атлас и общий материал для всех чанков.
///
/// Важно: блоки описаны данными (BlockDef), а не отдельными сценами/узлами.
/// </summary>
public partial class BlockDatabase : Node
{
    public static BlockDatabase Instance { get; private set; }

    public const int AtlasColumns = 4;
    public const int AtlasRows = 4;
    public const string AtlasPath = "res://Assets/Textures/block_atlas.png";

    // Индексы тайлов в атласе (порядок совпадает с block_atlas.png).
    public const int TileStone = 0;
    public const int TileDirt = 1;
    public const int TileGrassTop = 2;
    public const int TileGrassSide = 3;
    public const int TileSand = 4;
    public const int TileWater = 5;

    /// <summary>Таблица описаний, индексируется значением BlockId.</summary>
    public static BlockDef[] Definitions { get; private set; } = new BlockDef[256];

    public static Texture2D AtlasTexture { get; private set; }

    /// <summary>Размер одного текселя атласа в UV (нужен для отступа, чтобы не «подтекали» соседние тайлы).</summary>
    public static float AtlasTexelSize { get; private set; } = 1f / 64f;

    public static Material TerrainMaterial { get; private set; }

    /// <summary>Блоки, которые игрок может поставить (клавиши 1..N).</summary>
    public static readonly BlockId[] PlaceableBlocks =
    {
        BlockId.Stone,
        BlockId.Dirt,
        BlockId.Grass,
        BlockId.Sand,
        BlockId.Water,
    };

    public override void _Ready()
    {
        Instance = this;
        BuildDefaultDefinitions();
        LoadAtlas();
        BuildMaterial();
    }

    // ---------------------------------------------------------------- данные

    private static void BuildDefaultDefinitions()
    {
        Definitions = new BlockDef[256];

        Set(BlockId.Air, new BlockDef
        {
            DisplayName = "Воздух",
            Opaque = false,
            Solid = false,
            Hardness = 0f,
        });

        Set(BlockId.Stone, new BlockDef
        {
            DisplayName = "Камень",
            AtlasTop = TileStone,
            AtlasSide = TileStone,
            AtlasBottom = TileStone,
            Opaque = true,
            Solid = true,
            Hardness = 3f,
        });

        Set(BlockId.Dirt, new BlockDef
        {
            DisplayName = "Земля",
            AtlasTop = TileDirt,
            AtlasSide = TileDirt,
            AtlasBottom = TileDirt,
            Opaque = true,
            Solid = true,
            Hardness = 1f,
        });

        Set(BlockId.Grass, new BlockDef
        {
            DisplayName = "Трава",
            AtlasTop = TileGrassTop,
            AtlasSide = TileGrassSide,
            AtlasBottom = TileDirt,
            Opaque = true,
            Solid = true,
            Hardness = 1f,
        });

        Set(BlockId.Sand, new BlockDef
        {
            DisplayName = "Песок",
            AtlasTop = TileSand,
            AtlasSide = TileSand,
            AtlasBottom = TileSand,
            Opaque = true,
            Solid = true,
            Hardness = 0.8f,
        });

        Set(BlockId.Water, new BlockDef
        {
            DisplayName = "Вода",
            AtlasTop = TileWater,
            AtlasSide = TileWater,
            AtlasBottom = TileWater,
            Opaque = false,
            Solid = false,
            Liquid = true,
            Hardness = 0f,
        });
    }

    private static void Set(BlockId id, BlockDef def) => Definitions[(byte)id] = def;

    // ---------------------------------------------------------------- доступ

    public static BlockDef Get(BlockId id)
    {
        var def = Definitions[(byte)id];
        return def ?? Definitions[(byte)BlockId.Air];
    }

    public static bool IsOpaque(BlockId id)
    {
        if (id == BlockId.Air) return false;
        var def = Definitions[(byte)id];
        return def != null && def.Opaque;
    }

    public static bool IsSolid(BlockId id)
    {
        if (id == BlockId.Air) return false;
        var def = Definitions[(byte)id];
        return def != null && def.Solid;
    }

    public static string NameOf(BlockId id) => Get(id).DisplayName;

    // ---------------------------------------------------------------- ресурсы

    private static void LoadAtlas()
    {
        AtlasTexture = GD.Load<Texture2D>(AtlasPath);
        if (AtlasTexture == null)
        {
            GD.PushWarning($"[BlockDatabase] Не найден атлас {AtlasPath}. Блоки будут без текстуры.");
            return;
        }

        AtlasTexelSize = 1f / AtlasTexture.GetWidth();
    }

    private static void BuildMaterial()
    {
        var shader = GD.Load<Shader>("res://Assets/Shaders/terrain.gdshader");

        if (shader == null)
        {
            GD.PushError("[BlockDatabase] Не найден шейдер res://Assets/Shaders/terrain.gdshader");
            return;
        }

        var material = new ShaderMaterial { Shader = shader };
        material.SetShaderParameter("atlas", AtlasTexture);
        material.SetShaderParameter("grid", new Vector2(AtlasColumns, AtlasRows));
        material.SetShaderParameter("inset", 0.5f / 16f);
        TerrainMaterial = material;
    }

    private static readonly Dictionary<int, AtlasTexture> TileCache = new();

    /// <summary>Кусочек атласа как отдельная текстура — для иконок хотбара и инвентаря.</summary>
    public static Texture2D TileTexture(int tile)
    {
        if (AtlasTexture == null) return null;
        if (TileCache.TryGetValue(tile, out var cached)) return cached;

        int col = tile % AtlasColumns;
        int row = tile / AtlasColumns;
        int tileSize = AtlasTexture.GetWidth() / AtlasColumns;

        var texture = new AtlasTexture
        {
            Atlas = AtlasTexture,
            Region = new Rect2(col * tileSize, row * tileSize, tileSize, tileSize),
            FilterClip = true,
        };

        TileCache[tile] = texture;
        return texture;
    }

    /// <summary>Иконка блока для UI (по боковой грани — она самая узнаваемая).</summary>
    public static Texture2D IconOf(BlockId id) => TileTexture(Get(id).AtlasSide);
}
