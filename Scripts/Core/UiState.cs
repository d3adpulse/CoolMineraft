namespace VoxelCraft.Core;

/// <summary>
/// Общее состояние UI, чтобы игрок, HUD и пауза не воевали за курсор.
/// Курсор захватывается только по клику — как в Minecraft.
/// </summary>
public static class UiState
{
    public static bool InventoryOpen;
    public static bool Paused;
    public static bool MenuOpen;

    public static bool AnyPanelOpen => InventoryOpen || Paused || MenuOpen;
}
