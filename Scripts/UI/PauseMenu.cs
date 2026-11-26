using Godot;
using VoxelCraft.Core;
using VoxelCraft.Data;

namespace VoxelCraft.UI;

/// <summary>Меню паузы по Esc: продолжить, настройки, главное меню, выход.</summary>
public partial class PauseMenu : CanvasLayer
{
    private Control _root;
    private VBoxContainer _buttons;
    private Control _settings;
    private bool _settingsShown;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 10;

        _root = new Control();
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);

        // Затемнение живёт ВНУТРИ _root, иначе оно висело бы поверх игры всегда.
        var shade = new ColorRect { Color = new Color(0f, 0f, 0f, 0.62f) };
        shade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(shade);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(420f, 420f) };
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.OffsetLeft = -210f;
        panel.OffsetRight = 210f;
        panel.OffsetTop = -210f;
        panel.OffsetBottom = 210f;
        _root.AddChild(panel);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        panel.AddChild(box);

        var title = new Label { Text = "Пауза" };
        title.HorizontalAlignment = HorizontalAlignment.Center;
        title.AddThemeFontSizeOverride("font_size", 32);
        box.AddChild(title);

        _buttons = new VBoxContainer();
        box.AddChild(_buttons);

        AddButton("Продолжить", HideMenu);
        AddButton("Настройки", ShowSettings);
        AddButton("Сохранить мир", () => SaveSystem.Instance?.Save());
        AddButton("В главное меню", ToMainMenu);
        AddButton("Выйти из игры", () => GetTree().Quit());

        _settings = new SettingsPanel();
        _settings.Visible = false;
        _settings.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        box.AddChild(_settings);

        _root.Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel")) return;

        if (!GetTree().Paused)
        {
            ShowMenu();
        }
        else if (_settingsShown)
        {
            _settingsShown = false;
            _settings.Visible = false;
            _buttons.Visible = true;
        }
        else
        {
            HideMenu();
        }
    }

    private void ShowMenu()
    {
        GetTree().Paused = true;
        UiState.Paused = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _root.Visible = true;
    }

    private void HideMenu()
    {
        _settingsShown = false;
        _settings.Visible = false;
        _buttons.Visible = true;
        _root.Visible = false;
        GetTree().Paused = false;
        UiState.Paused = false;

        // После паузы тоже не захватываем молча — игрок кликнет сам.
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void ShowSettings()
    {
        _settingsShown = true;
        _buttons.Visible = false;
        _settings.Visible = true;
    }

    private void ToMainMenu()
    {
        SaveSystem.Instance?.Save();
        GetTree().Paused = false;
        UiState.Paused = false;
        UiState.InventoryOpen = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
    }

    private void AddButton(string text, System.Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0f, 40f) };
        button.Pressed += action;
        _buttons.AddChild(button);
    }
}
