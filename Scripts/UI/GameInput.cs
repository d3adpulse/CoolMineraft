using Godot;

namespace VoxelCraft.UI;

/// <summary>
/// Регистрирует действия ввода в рантайме, чтобы не писать длинные блоки [input]
/// в project.godot и не зависеть от раскладки клавиатуры (используем physical keycode).
/// </summary>
public partial class GameInput : Node
{
    public override void _Ready()
    {
        // Движение
        AddKey("move_forward", Key.W);
        AddKey("move_backward", Key.S);
        AddKey("move_left", Key.A);
        AddKey("move_right", Key.D);

        // Полёт/прыжок
        AddKey("jump", Key.Space);
        AddKey("descend", Key.Shift);
        AddKey("sprint", Key.Ctrl);
        AddKey("toggle_fly", Key.F);

        // Мир
        AddKey("save_world", Key.F5);
        AddKey("toggle_inventory", Key.E);
        AddKey("toggle_debug", Key.F3);

        // Блоки
        AddMouse("break_block", MouseButton.Left);
        AddMouse("place_block", MouseButton.Right);
    }

    private static void AddKey(StringName action, Key key)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);

        var inputEvent = new InputEventKey
        {
            PhysicalKeycode = key,
            Keycode = key,
        };

        InputMap.ActionAddEvent(action, inputEvent);
    }

    private static void AddMouse(StringName action, MouseButton button)
    {
        if (!InputMap.HasAction(action)) InputMap.AddAction(action);

        var inputEvent = new InputEventMouseButton
        {
            ButtonIndex = button,
        };

        InputMap.ActionAddEvent(action, inputEvent);
    }
}
