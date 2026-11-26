namespace VoxelCraft.World;

/// <summary>
/// Блок — это просто байт в массиве, а не узел сцены.
/// Узлы для каждого блока — путь к смерти производительности.
/// </summary>
public enum BlockId : byte
{
    Air = 0,
    Stone = 1,
    Dirt = 2,
    Grass = 3,
    Sand = 4,
    Water = 5,
}
