using Godot;
using VoxelCraft.Core;
using VoxelCraft.Data;

namespace VoxelCraft.UI;

/// <summary>Главное меню: сид, режим, настройки. Стилизовано под Minecraft.</summary>
public partial class MainMenu : Control
{
    private Control _main;
    private Control _world;
    private Control _settings;
    private LineEdit _seed;
    private OptionButton _mode;
    private Label _modeInfo;
    private Button _continue;

    public override void _Ready()
    {
        UiState.MenuOpen = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        BuildBackground();

        _main = BuildMainMenu();
        _world = BuildWorldMenu();
        _settings = BuildSettingsMenu();

        AddChild(_main);
        AddChild(_world);
        AddChild(_settings);

        ShowOnly(_main);
    }

    private void BuildBackground()
    {
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(0.16f, 0.3f, 0.52f));
        gradient.SetColor(1, new Color(0.03f, 0.05f, 0.09f));

        var texture = new GradientTexture2D
        {
            Gradient = gradient,
            FillFrom = new Vector2(0.5f, 0f),
            FillTo = new Vector2(0.5f, 1f),
            Width = 8,
            Height = 256,
        };

        var background = new TextureRect
        {
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.Scale,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        background.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(background);

        var version = new Label
        {
            Text = "VoxelCraft 1.0  •  Godot 4.5 / C#",
            Modulate = new Color(1f, 1f, 1f, 0.45f),
        };
        version.AddThemeFontSizeOverride("font_size", 12);
        version.SetAnchorsPreset(LayoutPreset.BottomLeft);
        version.OffsetLeft = 14f;
        version.OffsetTop = -26f;
        version.OffsetRight = 420f;
        version.OffsetBottom = -8f;
        AddChild(version);
    }

    private Control BuildMainMenu()
    {
        var panel = CenteredPanel(400f, 420f);
        var box = panel.GetChild<VBoxContainer>(0);

        var title = new Label { Text = "VOXELCRAFT" };
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 46);
        title.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));
        title.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.65f));
        title.AddThemeConstantOverride("outline_size", 10);
        box.AddChild(title);

        var splash = new Label
        {
            Text = "теперь с greedy meshing!",
            HorizontalAlignment = HorizontalAlignment.Center,
            Rotation = Mathf.DegToRad(-7f),
        };
        splash.AddThemeFontSizeOverride("font_size", 15);
        splash.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.25f));
        splash.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.7f));
        splash.AddThemeConstantOverride("outline_size", 5);
        box.AddChild(splash);

        box.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 18f) });

        AddButton(box, "Одиночная игра", () => ShowOnly(_world));
        AddButton(box, "Настройки", () => ShowOnly(_settings));
        AddButton(box, "Выход из игры", () => GetTree().Quit());

        var hint = new Label
        {
            Text = "WASD — ходьба   •   E — инвентарь   •   Esc — пауза",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 1f, 1f, 0.55f),
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(hint);

        return panel;
    }

    private Control BuildWorldMenu()
    {
        var panel = CenteredPanel(460f, 470f);
        var box = panel.GetChild<VBoxContainer>(0);

        box.AddChild(Header("Одиночная игра"));

        var seedLabel = new Label { Text = "Сид мира (пусто — случайный)" };
        seedLabel.AddThemeFontSizeOverride("font_size", 14);
        box.AddChild(seedLabel);

        _seed = new LineEdit
        {
            PlaceholderText = "например: 1337",
            CustomMinimumSize = new Vector2(0f, 38f),
            Alignment = HorizontalAlignment.Center,
        };
        box.AddChild(_seed);

        var modeLabel = new Label { Text = "Режим игры" };
        modeLabel.AddThemeFontSizeOverride("font_size", 14);
        box.AddChild(modeLabel);

        _mode = new OptionButton { CustomMinimumSize = new Vector2(0f, 38f) };
        _mode.AddItem("Выживание", (int)GameMode.Survival);
        _mode.AddItem("Творческий", (int)GameMode.Creative);
        _mode.Selected = (int)(GameSettings.Instance?.Mode ?? GameMode.Creative);
        _mode.ItemSelected += _ => UpdateModeInfo();
        box.AddChild(_mode);

        _modeInfo = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0f, 42f),
            Modulate = new Color(0.75f, 0.85f, 0.95f),
        };
        _modeInfo.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(_modeInfo);

        box.AddChild(new Control { CustomMinimumSize = new Vector2(0f, 10f) });

        AddButton(box, "Создать новый мир", () => StartWorld(true));
        _continue = AddButton(box, "Продолжить мир", () => StartWorld(false));
        UpdateContinueText();

        AddButton(box, "Удалить сохранение", DeleteSave);
        AddButton(box, "Назад", () => ShowOnly(_main));

        UpdateModeInfo();
        return panel;
    }

    private Control BuildSettingsMenu()
    {
        var panel = CenteredPanel(520f, 560f);
        var box = panel.GetChild<VBoxContainer>(0);

        box.AddChild(Header("Настройки"));

        var settings = new SettingsPanel { SizeFlagsVertical = SizeFlags.ExpandFill };
        box.AddChild(settings);

        AddButton(box, "Назад", () => ShowOnly(_main));
        return panel;
    }

    // ----------------------------------------------------------------- actions

    private void UpdateModeInfo()
    {
        string info = _mode.GetItemId(_mode.Selected) == (int)GameMode.Survival
            ? "Выживание: здоровье, урон от падения, ломка по времени, блоки тратятся и добываются."
            : "Творческий: полёт (F), мгновенная ломка, бесконечные блоки.";
        _modeInfo.Text = info;
    }

    private void UpdateContinueText()
    {
        if (_continue == null) return;

        var save = SaveSystem.Instance;
        if (save != null && save.HasSave)
        {
            _continue.Text = $"Продолжить мир (сид {save.Seed})";
            _continue.Disabled = false;
        }
        else
        {
            _continue.Text = "Продолжить мир (нет сохранения)";
            _continue.Disabled = true;
        }
    }

    private void DeleteSave()
    {
        SaveSystem.Instance?.DeleteSave();
        UpdateContinueText();
    }

    private void StartWorld(bool fresh)
    {
        var settings = GameSettings.Instance;
        if (settings != null)
        {
            settings.Mode = (GameMode)_mode.GetItemId(_mode.Selected);
            settings.Save();
        }

        if (fresh)
        {
            int seed = int.TryParse(_seed.Text.Trim(), out int parsed)
                ? parsed
                : (int)(GD.Randi() % 1_000_000);
            SaveSystem.Instance?.ResetWorld(seed);
        }

        UiState.MenuOpen = false;
        GetTree().ChangeSceneToFile("res://Scenes/World/Main.tscn");
    }

    // -------------------------------------------------------------- UI helpers

    private static Label Header(string text)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 28);
        label.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.6f));
        label.AddThemeConstantOverride("outline_size", 6);
        return label;
    }

    private static PanelContainer CenteredPanel(float width, float height)
    {
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, height) };
        panel.SetAnchorsPreset(LayoutPreset.Center);
        panel.OffsetLeft = -width / 2f;
        panel.OffsetRight = width / 2f;
        panel.OffsetTop = -height / 2f;
        panel.OffsetBottom = height / 2f;

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.08f, 0.12f, 0.82f),
            BorderColor = new Color(0.45f, 0.6f, 0.8f, 0.5f),
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 26f,
            ContentMarginRight = 26f,
            ContentMarginTop = 20f,
            ContentMarginBottom = 20f,
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 9);
        panel.AddChild(box);
        return panel;
    }

    private static Button AddButton(Control parent, string text, System.Action action)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0f, 44f),
        };

        button.AddThemeFontSizeOverride("font_size", 17);
        button.AddThemeStyleboxOverride("normal", ButtonStyle(new Color(0.16f, 0.19f, 0.24f), new Color(0.4f, 0.5f, 0.62f)));
        button.AddThemeStyleboxOverride("hover", ButtonStyle(new Color(0.23f, 0.3f, 0.4f), new Color(0.65f, 0.8f, 1f)));
        button.AddThemeStyleboxOverride("pressed", ButtonStyle(new Color(0.11f, 0.14f, 0.18f), new Color(0.55f, 0.7f, 0.9f)));
        button.AddThemeStyleboxOverride("disabled", ButtonStyle(new Color(0.1f, 0.11f, 0.13f), new Color(0.25f, 0.27f, 0.3f)));
        button.AddThemeColorOverride("font_disabled_color", new Color(1f, 1f, 1f, 0.3f));

        button.Pressed += action;
        parent.AddChild(button);
        return button;
    }

    private static StyleBoxFlat ButtonStyle(Color background, Color border)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            CornerRadiusTopLeft = 5,
            CornerRadiusTopRight = 5,
            CornerRadiusBottomLeft = 5,
            CornerRadiusBottomRight = 5,
        };
    }

    private void ShowOnly(Control visible)
    {
        _main.Visible = visible == _main;
        _world.Visible = visible == _world;
        _settings.Visible = visible == _settings;

        if (visible == _world) UpdateContinueText();
    }
}
