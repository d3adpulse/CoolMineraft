using VoxelCraft.World;

namespace VoxelCraft.Core;

/// <summary>
/// Инвентарь: 9 слотов хотбара. В креативе количество = -1 (бесконечно),
/// в выживании — реальные числа, блоки добываются ломанием.
/// </summary>
public sealed class Inventory
{
    public const int HotbarSize = 9;
    public const int Infinite = -1;

    public readonly BlockId[] Slots = new BlockId[HotbarSize];
    public readonly int[] Counts = new int[HotbarSize];
    public int Selected;

    public BlockId SelectedBlock => Slots[Selected];
    public int SelectedCount => Counts[Selected];

    public void FillCreativeDefaults()
    {
        var placeable = BlockDatabase.PlaceableBlocks;
        for (int i = 0; i < HotbarSize; i++)
        {
            Slots[i] = i < placeable.Length ? placeable[i] : BlockId.Air;
            Counts[i] = i < placeable.Length ? Infinite : 0;
        }
    }

    public int CountOf(BlockId id)
    {
        int total = 0;
        for (int i = 0; i < HotbarSize; i++)
        {
            if (Slots[i] != id || Counts[i] <= 0) continue;
            total += Counts[i];
        }
        return total;
    }

    /// <summary>Добавляет добытый блок. Возвращает true, если поместился.</summary>
    public bool Add(BlockId id, int amount = 1)
    {
        if (id == BlockId.Air || amount <= 0) return false;

        for (int i = 0; i < HotbarSize; i++)
        {
            if (Slots[i] != id || Counts[i] <= 0) continue;
            Counts[i] += amount;
            return true;
        }

        for (int i = 0; i < HotbarSize; i++)
        {
            if (Slots[i] != BlockId.Air && Counts[i] > 0) continue;
            Slots[i] = id;
            Counts[i] = amount;
            return true;
        }

        return false;
    }

    /// <summary>Списывает один блок из выбранного слота. В креативе всегда успешно.</summary>
    public bool ConsumeSelected()
    {
        if (Slots[Selected] == BlockId.Air) return false;
        if (Counts[Selected] == Infinite) return true;
        if (Counts[Selected] <= 0) return false;

        Counts[Selected]--;
        if (Counts[Selected] <= 0)
        {
            Slots[Selected] = BlockId.Air;
            Counts[Selected] = 0;
        }

        return true;
    }

    public void SetSlot(int index, BlockId id, int count)
    {
        if (index < 0 || index >= HotbarSize) return;
        Slots[index] = id;
        Counts[index] = count;
    }

    public void Select(int index)
    {
        Selected = (index % HotbarSize + HotbarSize) % HotbarSize;
    }
}
