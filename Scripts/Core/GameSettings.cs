using Godot;
using System;
using System.IO;
using System.Text.Json;

namespace VoxelCraft.Core;

/// <summary>
/// Автозагрузка: настройки игры (дальность прорисовки, чувствительность, режим).
/// Хранятся в user://settings.json и переживают перезапуск.
/// </summary>
public partial class GameSettings : Node
{
    public static GameSettings Instance { get; private set; }

    public const string SettingsPath = "user://settings.json";

    public int RenderDistance = 5;
    public float MouseSensitivity = 0.0022f;
    public float Fov = 75f;
    public bool Fullscreen;
    public bool VSync = true;
    public bool Shadows = true;
    public bool GreedyMeshing = true;
    public GameMode Mode = GameMode.Creative;

    public static bool IsCreative => Instance == null || Instance.Mode == GameMode.Creative;

    private sealed class Dto
    {
        public int RenderDistance { get; set; } = 5;
        public float MouseSensitivity { get; set; } = 0.0022f;
        public float Fov { get; set; } = 75f;
        public bool Fullscreen { get; set; }
        public bool VSync { get; set; } = true;
        public bool Shadows { get; set; } = true;
        public bool GreedyMeshing { get; set; } = true;
        public int Mode { get; set; }
    }

    public override void _Ready()
    {
        Instance = this;
        Load();
        ApplyDisplay();
    }

    public void Load()
    {
        string path = ProjectSettings.GlobalizePath(SettingsPath);
        if (!File.Exists(path)) return;

        try
        {
            var dto = JsonSerializer.Deserialize<Dto>(File.ReadAllText(path));
            if (dto == null) return;

            RenderDistance = Mathf.Clamp(dto.RenderDistance, 2, 10);
            MouseSensitivity = Mathf.Clamp(dto.MouseSensitivity, 0.0005f, 0.01f);
            Fov = Mathf.Clamp(dto.Fov, 55f, 110f);
            Fullscreen = dto.Fullscreen;
            VSync = dto.VSync;
            Shadows = dto.Shadows;
            GreedyMeshing = dto.GreedyMeshing;
            Mode = (GameMode)dto.Mode;
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[GameSettings] Не удалось прочитать настройки: {exception.Message}");
        }
    }

    public void Save()
    {
        try
        {
            var dto = new Dto
            {
                RenderDistance = RenderDistance,
                MouseSensitivity = MouseSensitivity,
                Fov = Fov,
                Fullscreen = Fullscreen,
                VSync = VSync,
                Shadows = Shadows,
                GreedyMeshing = GreedyMeshing,
                Mode = (int)Mode,
            };

            File.WriteAllText(ProjectSettings.GlobalizePath(SettingsPath), JsonSerializer.Serialize(dto));
        }
        catch (Exception exception)
        {
            GD.PushError($"[GameSettings] Ошибка сохранения: {exception.Message}");
        }
    }

    /// <summary>Применяет всё, что можно применить без перезапуска сцены.</summary>
    public void ApplyDisplay()
    {
        DisplayServer.WindowSetVsyncMode(VSync
            ? DisplayServer.VSyncMode.Enabled
            : DisplayServer.VSyncMode.Disabled);

        DisplayServer.WindowSetMode(Fullscreen
            ? DisplayServer.WindowMode.Fullscreen
            : DisplayServer.WindowMode.Windowed);
    }

    public void SetAndSave(Action<GameSettings> change)
    {
        change(this);
        Save();
    }
}
