using Godot;
using VoxelCraft.Core;
using VoxelCraft.Player;
using VoxelCraft.World;

namespace VoxelCraft.UI;

/// <summary>HUD: статистика, прицел, хотбар с иконками, здоровье, инвентарь (E) и прогресс ломки.</summary>
public partial class Hud : CanvasLayer
{
    private PanelContainer _infoPanel;
    private Label _info;
    private Label _hint;
    private bool _debugVisible = true;
    private Label _health;
    private ProgressBar _breakBar;
    private HBoxContainer _hotbar;
    private TextureRect[] _icons;
    private Label[] _counts;
    private Panel[] _slots;
    private Control _inventoryPanel;
    private GridContainer _inventoryGrid;

    private PlayerController _player;
    private BlockInteraction _interaction;
    private Inventory _inventory;
    private float _timer;
    private bool _inventoryOpen;

    public void Initialize(PlayerController player, BlockInteraction interaction, Inventory inventory)
    {
        _player = player;
        _interaction = interaction;
        _inventory = inventory;
        BuildUi();
        RefreshHotbar();
    }

    private void BuildUi()
    {
        _infoPanel = new PanelContainer
        {
            Position = new Vector2(10f, 8f),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var infoStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.07f, 0.1f, 0.45f),
            ContentMarginLeft = 8f,
            ContentMarginRight = 10f,
            ContentMarginTop = 6f,
            ContentMarginBottom = 6f,
        };
        _infoPanel.AddThemeStyleboxOverride("panel", infoStyle);
        AddChild(_infoPanel);

        _info = MakeLabel();
        _infoPanel.AddChild(_info);

        _hint = MakeLabel();
        _hint.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _hint.OffsetLeft = 12f;
        _hint.OffsetTop = -204f;
        _hint.OffsetRight = 800f;
        _hint.OffsetBottom = -86f;
        _hint.AddThemeFontSizeOverride("font_size", 13);
        _hint.Text = CreativeHint();
        AddChild(_hint);

        BuildCrosshair();

        _breakBar = new ProgressBar
        {
            MinValue = 0f,
            MaxValue = 1f,
            Value = 0f,
            ShowPercentage = false,
            CustomMinimumSize = new Vector2(180f, 8f),
        };
        _breakBar.SetAnchorsPreset(Control.LayoutPreset.Center);
        _breakBar.OffsetLeft = -90f;
        _breakBar.OffsetRight = 90f;
        _breakBar.OffsetTop = 18f;
        _breakBar.OffsetBottom = 26f;
        _breakBar.Visible = false;
        AddChild(_breakBar);

        _health = MakeLabel();
        _health.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _health.OffsetLeft = -220f;
        _health.OffsetRight = 220f;
        _health.OffsetTop = -152f;
        _health.OffsetBottom = -126f;
        _health.HorizontalAlignment = HorizontalAlignment.Center;
        _health.AddThemeFontSizeOverride("font_size", 20);
        AddChild(_health);

        BuildHotbar();
        BuildInventoryPanel();
    }

    private void BuildCrosshair()
    {
        var crosshair = new Control
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        crosshair.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(crosshair);

        var white = new Color(1f, 1f, 1f, 0.9f);

        var horizontal = new ColorRect { Color = white, MouseFilter = Control.MouseFilterEnum.Ignore };
        horizontal.SetAnchorsPreset(Control.LayoutPreset.Center);
        horizontal.OffsetLeft = -10f;
        horizontal.OffsetRight = 10f;
        horizontal.OffsetTop = -1f;
        horizontal.OffsetBottom = 1f;
        crosshair.AddChild(horizontal);

        var vertical = new ColorRect { Color = white, MouseFilter = Control.MouseFilterEnum.Ignore };
        vertical.SetAnchorsPreset(Control.LayoutPreset.Center);
        vertical.OffsetLeft = -1f;
        vertical.OffsetRight = 1f;
        vertical.OffsetTop = -10f;
        vertical.OffsetBottom = 10f;
        crosshair.AddChild(vertical);
    }

    private void BuildHotbar()
    {
        _hotbar = new HBoxContainer();
        _hotbar.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _hotbar.OffsetLeft = -246f;
        _hotbar.OffsetRight = 246f;
        _hotbar.OffsetTop = -70f;
        _hotbar.OffsetBottom = -10f;
        _hotbar.Alignment = BoxContainer.AlignmentMode.Center;
        _hotbar.AddThemeConstantOverride("separation", 4);
        AddChild(_hotbar);

        _icons = new TextureRect[Inventory.HotbarSize];
        _counts = new Label[Inventory.HotbarSize];
        _slots = new Panel[Inventory.HotbarSize];

        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            var slot = new Panel { CustomMinimumSize = new Vector2(50f, 50f) };
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.12f, 0.12f, 0.14f, 0.8f),
                BorderColor = new Color(0.45f, 0.45f, 0.5f, 0.9f),
                BorderWidthTop = 2,
                BorderWidthBottom = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
            };
            slot.AddThemeStyleboxOverride("panel", style);

            var icon = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            icon.OffsetLeft = 7f;
            icon.OffsetRight = -7f;
            icon.OffsetTop = 5f;
            icon.OffsetBottom = -10f;
            icon.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            slot.AddChild(icon);

            var number = new Label
            {
                Text = (i + 1).ToString(),
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            number.AddThemeFontSizeOverride("font_size", 10);
            number.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.7f));
            number.Position = new Vector2(3f, 1f);
            slot.AddChild(number);

            var count = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
            count.AddThemeFontSizeOverride("font_size", 12);
            count.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
            count.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
            count.OffsetLeft = -22f;
            count.OffsetTop = -18f;
            slot.AddChild(count);

            _hotbar.AddChild(slot);
            _slots[i] = slot;
            _icons[i] = icon;
            _counts[i] = count;
        }
    }

    private void BuildInventoryPanel()
    {
        _inventoryPanel = new PanelContainer { CustomMinimumSize = new Vector2(520f, 240f) };
        _inventoryPanel.SetAnchorsPreset(Control.LayoutPreset.Center);
        _inventoryPanel.OffsetLeft = -260f;
        _inventoryPanel.OffsetRight = 260f;
        _inventoryPanel.OffsetTop = -120f;
        _inventoryPanel.OffsetBottom = 120f;
        _inventoryPanel.Visible = false;
        AddChild(_inventoryPanel);

        var box = new VBoxContainer();
        _inventoryPanel.AddChild(box);

        var title = new Label { Text = "Инвентарь — клик по блоку выбирает слот" };
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 18);
        box.AddChild(title);

        _inventoryGrid = new GridContainer { Columns = 5 };
        _inventoryGrid.AddThemeConstantOverride("h_separation", 6);
        _inventoryGrid.AddThemeConstantOverride("v_separation", 6);
        box.AddChild(_inventoryGrid);

        foreach (var id in BlockDatabase.PlaceableBlocks)
        {
            int index = (int)id;
            var button = new Button();
            button.CustomMinimumSize = new Vector2(88f, 72f);

            var icon = new TextureRect
            {
                Texture = BlockDatabase.IconOf(id),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Position = new Vector2(28f, 4f),
                CustomMinimumSize = new Vector2(32f, 32f),
                TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            };
            button.AddChild(icon);

            var label = new Label
            {
                Text = $"{BlockDatabase.NameOf(id)}\n{(_inventory?.CountOf(id) ?? 0)}",
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AddThemeFontSizeOverride("font_size", 11);
            label.Position = new Vector2(0f, 38f);
            label.CustomMinimumSize = new Vector2(88f, 32f);
            button.AddChild(label);

            button.Pressed += () =>
            {
                if (_inventory == null) return;
                var count = GameSettings.IsCreative ? Inventory.Infinite : _inventory.CountOf(id);
                if (count > 0) _inventory.SetSlot(_inventory.Selected, (BlockId)index, count);
                RefreshHotbar();
            };

            _inventoryGrid.AddChild(button);
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (GetTree().Paused) return;

        if (@event.IsActionPressed("toggle_debug"))
        {
            _debugVisible = !_debugVisible;
            _infoPanel.Visible = _debugVisible;
            return;
        }

        if (@event.IsActionPressed("toggle_inventory"))
        {
            SetInventoryOpen(!_inventoryOpen);
        }
    }

    private void SetInventoryOpen(bool open)
    {
        _inventoryOpen = open;
        UiState.InventoryOpen = open;
        _inventoryPanel.Visible = open;

        if (open)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
            RefreshInventory();
        }
        else if (!GetTree().Paused)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
        }
    }

    public override void _Process(double delta)
    {
        if (_info == null) return;

        if (UiState.Paused && _inventoryOpen)
        {
            SetInventoryOpen(false);
        }

        _timer -= (float)delta;
        if (_timer > 0f) return;
        _timer = 0.2f;

        var manager = ChunkManager.Instance;
        var position = _player != null ? _player.GlobalPosition : Vector3.Zero;
        var chunk = ChunkManager.WorldToChunk(position);

        bool creative = GameSettings.IsCreative;

        _info.Text =
            $"FPS: {Engine.GetFramesPerSecond():0}\n" +
            $"Позиция: {position.X:0.0}  {position.Y:0.0}  {position.Z:0.0}\n" +
            $"Чанк: {chunk.X}, {chunk.Y}\n" +
            $"Чанков: {manager?.LoadedChunks ?? 0}  в очереди: {manager?.PendingChunks ?? 0}\n" +
            $"Треугольников: {manager?.Triangles ?? 0}\n" +
            $"Режим: {(creative ? "ТВОРЧЕСКИЙ" : "ВЫЖИВАНИЕ")}" +
            (creative ? "" : $"\nЗдоровье: {_player?.Health ?? 0f:0}/20");

        bool needClick = Input.MouseMode == Input.MouseModeEnum.Visible &&
                         !_inventoryOpen && !UiState.Paused;

        _hint.Text = (needClick ? "Кликните по окну, чтобы играть\n" : "") +
                     (creative ? CreativeHint() : SurvivalHint());

        _health.Visible = !creative;
        if (!creative && _player != null)
        {
            int hearts = Mathf.CeilToInt(_player.Health / 2f);
            _health.Text = new string('♥', hearts) + new string('♡', 10 - hearts);
        }

        _breakBar.Visible = _interaction != null && _interaction.IsBreaking;
        if (_breakBar.Visible) _breakBar.Value = _interaction.BreakProgress;

        RefreshHotbar();
        if (_inventoryOpen) RefreshInventory();
    }

    private static string CreativeHint() =>
        "WASD — движение   Space — вверх   Shift — вниз   Ctrl — ускорение   F — полёт/ходьба\n" +
        "ЛКМ — сломать   ПКМ — поставить   1..9 / колесо — выбор блока   E — инвентарь\n" +
        "Esc — пауза   F5 — сохранить";

    private static string SurvivalHint() =>
        "WASD — движение   Space — прыжок   Ctrl — бег   F5 — сохранить\n" +
        "ЛКМ (зажать) — сломать   ПКМ — поставить   1..9 / колесо — блок   E — инвентарь\n" +
        "Esc — пауза";

    private void RefreshHotbar()
    {
        if (_inventory == null) return;

        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            var id = _inventory.Slots[i];
            _icons[i].Texture = id == BlockId.Air ? null : BlockDatabase.IconOf(id);

            int count = _inventory.Counts[i];
            _counts[i].Text = count == Inventory.Infinite ? "" : count > 1 ? count.ToString() : "";

            var style = (StyleBoxFlat)_slots[i].GetThemeStylebox("panel");
            if (style != null)
            {
                style.BorderColor = i == _inventory.Selected
                    ? new Color(1f, 0.92f, 0.45f)
                    : new Color(0.45f, 0.45f, 0.5f, 0.9f);
            }
        }
    }

    private void RefreshInventory()
    {
        if (_inventoryGrid == null || _inventory == null) return;

        int i = 0;
        foreach (var id in BlockDatabase.PlaceableBlocks)
        {
            if (i >= _inventoryGrid.GetChildCount()) break;
            var button = _inventoryGrid.GetChild<Button>(i);
            var label = button.GetChild<Label>(1);
            label.Text = $"{BlockDatabase.NameOf(id)}\n" +
                         (GameSettings.IsCreative ? "∞" : _inventory.CountOf(id).ToString());
            i++;
        }
    }

    private static Label MakeLabel()
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f, 0.95f));
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.85f));
        label.AddThemeConstantOverride("outline_size", 4);
        return label;
    }
}
