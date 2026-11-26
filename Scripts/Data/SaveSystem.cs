using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using VoxelCraft.World;

namespace VoxelCraft.Data;

/// <summary>
/// Этап 9: сохраняем только изменения блоков + seed мира.
/// Полный мир — гигабайты, модификации — килобайты.
/// </summary>
public partial class SaveSystem : Node
{
    public static SaveSystem Instance { get; private set; }

    [Export] public string SavePath = "user://world_save.json";
    [Export] public float AutosaveInterval = 3f;

    public int Seed { get; private set; }
    public bool HasSave { get; private set; }
    public int ModifiedCount => _modified.Count;

    private readonly Dictionary<Vector3I, BlockId> _modified = new();
    private bool _dirty;
    private float _timer;

    private sealed class SaveDto
    {
        public int Seed { get; set; }
        public List<int[]> Blocks { get; set; } = new();
    }

    public override void _Ready()
    {
        Instance = this;
        Load();
    }

    public override void _Process(double delta)
    {
        if (!_dirty) return;

        _timer += (float)delta;
        if (_timer >= AutosaveInterval) Save();
    }

    public override void _ExitTree()
    {
        if (_dirty) Save();
    }

    /// <summary>Запомнить изменение блока (вызывается ChunkManager.SetBlock).</summary>
    public void Record(Vector3I worldPosition, BlockId id)
    {
        _modified[worldPosition] = id;
        _dirty = true;
        _timer = 0f;
    }

    /// <summary>Копия модификаций, относящихся к чанку. Вызывается в главном потоке перед фоновым заданием.</summary>
    public Dictionary<Vector3I, BlockId> ModificationsFor(Vector2I chunkCoord)
    {
        var result = new Dictionary<Vector3I, BlockId>();
        if (_modified.Count == 0) return result;

        foreach (var pair in _modified)
        {
            if (ChunkManager.ChunkCoordOf(pair.Key) == chunkCoord)
            {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    public void Load()
    {
        string path = ProjectSettings.GlobalizePath(SavePath);

        if (!File.Exists(path))
        {
            HasSave = false;
            Seed = (int)(GD.Randi() % 1_000_000);
            GD.Print($"[SaveSystem] Сохранения нет. Новый seed: {Seed}");

            // Сразу фиксируем seed на диске, иначе следующий запуск создаст другой мир.
            HasSave = true;
            _dirty = true;
            Save();
            return;
        }

        try
        {
            string json = File.ReadAllText(path);
            var dto = JsonSerializer.Deserialize<SaveDto>(json);

            if (dto == null) return;

            Seed = dto.Seed;
            HasSave = true;
            _modified.Clear();

            foreach (int[] entry in dto.Blocks)
            {
                if (entry == null || entry.Length < 4) continue;
                _modified[new Vector3I(entry[0], entry[1], entry[2])] = (BlockId)entry[3];
            }

            GD.Print($"[SaveSystem] Загружено: seed {Seed}, модификаций {_modified.Count}");
        }
        catch (Exception exception)
        {
            GD.PushWarning($"[SaveSystem] Не удалось прочитать сохранение: {exception.Message}");
            HasSave = false;
            Seed = (int)(GD.Randi() % 1_000_000);
        }
    }

    public void Save()
    {
        try
        {
            var dto = new SaveDto { Seed = Seed };

            foreach (var pair in _modified)
            {
                dto.Blocks.Add(new[] { pair.Key.X, pair.Key.Y, pair.Key.Z, (int)pair.Value });
            }

            string path = ProjectSettings.GlobalizePath(SavePath);
            File.WriteAllText(path, JsonSerializer.Serialize(dto));

            _dirty = false;
            _timer = 0f;
            GD.Print($"[SaveSystem] Сохранено модификаций: {_modified.Count} -> {path}");
        }
        catch (Exception exception)
        {
            GD.PushError($"[SaveSystem] Ошибка сохранения: {exception.Message}");
        }
    }

    /// <summary>Полностью удалить сохранение (из главного меню).</summary>
    public void DeleteSave()
    {
        try
        {
            string path = ProjectSettings.GlobalizePath(SavePath);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception exception)
        {
            GD.PushError($"[SaveSystem] Не удалось удалить сохранение: {exception.Message}");
        }

        _modified.Clear();
        HasSave = false;
        Seed = (int)(GD.Randi() % 1_000_000);
        _dirty = false;
        _timer = 0f;
        GD.Print($"[SaveSystem] Сохранение удалено. Новый seed: {Seed}");
    }

    /// <summary>Сброс мира: новый seed и пустой список изменений.</summary>
    public void ResetWorld(int seed)
    {
        _modified.Clear();
        Seed = seed;
        HasSave = true;
        _dirty = true;
        Save();
    }
}
