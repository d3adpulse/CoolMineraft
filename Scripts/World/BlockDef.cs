using Godot;

namespace VoxelCraft.World;

/// <summary>
/// Описание типа блока: какие тайлы атласа брать для верха/боков/низа и как с блоком взаимодействовать.
/// Атлас: 4x4 тайла по 16x16 пикселей (Assets/Textures/block_atlas.png).
/// </summary>
[GlobalClass]
public partial class BlockDef : Resource
{
    [Export] public string DisplayName = "Block";

    /// <summary>Индекс тайла в атласе для верхней грани.</summary>
    [Export] public int AtlasTop;

    /// <summary>Индекс тайла в атласе для боковых граней.</summary>
    [Export] public int AtlasSide;

    /// <summary>Индекс тайла в атласе для нижней грани.</summary>
    [Export] public int AtlasBottom;

    /// <summary>Перекрывает ли блок грань соседа (камень/земля — да, вода — нет).</summary>
    [Export] public bool Opaque = true;

    /// <summary>Есть ли у блока коллизия.</summary>
    [Export] public bool Solid = true;

    [Export] public bool Liquid;

    [Export] public float Hardness = 1.0f;
}
