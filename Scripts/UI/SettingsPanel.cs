using Godot;
using VoxelCraft.Core;

namespace VoxelCraft.UI;

/// <summary>Панель настроек, используется и в главном меню, и в паузе.</summary>
public partial class SettingsPanel : Control
{
    private HSlider _distance;
    private HSlider _sensitivity;
    private HSlider _fov;
    private CheckButton _vsync;
    private CheckButton _shadows;
    private CheckButton _fullscreen;
    private CheckButton _greedy;

    public override void _Ready()
    {
        var settings = GameSettings.Instance;
        if (settings == null) return;

        var box = new VBoxContainer();
        box.SetAnchorsPreset(LayoutPreset.FullRect);
        box.OffsetLeft = 12f;
        box.OffsetTop = 8f;
        box.OffsetRight = -12f;
        box.OffsetBottom = -8f;
        AddChild(box);

        var title = new Label { Text = "Настройки" };
        title.AddThemeFontSizeOverride("font_size", 24);
        box.AddChild(title);

        _distance = AddSlider(box, "Дальность прорисовки (чанков)", 2f, 10f, 1f, settings.RenderDistance,
            v => settings.SetAndSave(s => s.RenderDistance = (int)v));

        _sensitivity = AddSlider(box, "Чувствительность мыши", 0.0005f, 0.01f, 0.0001f,
            settings.MouseSensitivity, v => settings.SetAndSave(s => s.MouseSensitivity = v));

        _fov = AddSlider(box, "Поле зрения", 55f, 110f, 1f, settings.Fov,
            v => settings.SetAndSave(s => s.Fov = v));

        _vsync = AddCheck(box, "Вертикальная синхронизация", settings.VSync,
            on => settings.SetAndSave(s => s.VSync = on));

        _shadows = AddCheck(box, "Тени", settings.Shadows,
            on => settings.SetAndSave(s => s.Shadows = on));

        _fullscreen = AddCheck(box, "Полный экран", settings.Fullscreen,
            on => settings.SetAndSave(s => s.Fullscreen = on));

        _greedy = AddCheck(box, "Greedy meshing (оптимизация)", settings.GreedyMeshing,
            on => settings.SetAndSave(s => s.GreedyMeshing = on));

        var hint = new Label
        {
            Text = "Дальность и greedy применяются после пересоздания мира.",
            Modulate = new Color(0.8f, 0.85f, 0.9f),
        };
        hint.AddThemeFontSizeOverride("font_size", 13);
        box.AddChild(hint);
    }

    private static HSlider AddSlider(Control parent, string label, float min, float max,
        float step, float value, System.Action<float> onChanged)
    {
        var row = new VBoxContainer();
        parent.AddChild(row);

        var caption = new Label { Text = label };
        caption.AddThemeFontSizeOverride("font_size", 15);
        row.AddChild(caption);

        var slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = value,
            CustomMinimumSize = new Vector2(280f, 24f),
        };
        slider.ValueChanged += value => onChanged((float)value);
        row.AddChild(slider);
        return slider;
    }

    private static CheckButton AddCheck(Control parent, string label, bool value,
        BaseButton.ToggledEventHandler onChanged)
    {
        var check = new CheckButton { Text = label, ButtonPressed = value };
        check.Toggled += onChanged;
        parent.AddChild(check);
        return check;
    }
}
