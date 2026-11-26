using Godot;
using VoxelCraft.Core;
using VoxelCraft.Data;
using VoxelCraft.Player;
using VoxelCraft.UI;
using VoxelCraft.World;

namespace VoxelCraft.World;

/// <summary>
/// Точка входа игрового мира. Собирает сцену кодом: окружение, свет, генератор,
/// чанки, игрока, инвентарь и UI. Главное меню — отдельная сцена.
/// </summary>
public partial class Main : Node3D
{
    private ChunkManager _chunkManager;
    private WorldGenerator _generator;
    private PlayerController _player;
    private BlockInteraction _interaction;
    private Hud _hud;
    private PauseMenu _pause;
    private Inventory _inventory;

    private bool _perfEnabled;
    private float _perfTimer;

    /// <summary>Профилирование: запуск с аргументом <c>-- --perf</c> печатает метрики раз в секунду.</summary>
    public override void _Process(double delta)
    {
        if (!_perfEnabled || _chunkManager == null) return;

        _perfTimer += (float)delta;
        if (_perfTimer < 1f) return;
        _perfTimer = 0f;

        float cpu = (float)Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000f;
        float physics = (float)Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000f;
        int drawCalls = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
        int primitives = (int)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);

        GD.Print($"[PERF] fps={Engine.GetFramesPerSecond(),3}  " +
                 $"чанков={_chunkManager.LoadedChunks,3}  " +
                 $"треуг={_chunkManager.Triangles,7}  " +
                 $"draw={drawCalls,4}  prim={primitives,7}  " +
                 $"cpu={cpu:0.0}мс  phys={physics:0.0}мс");
    }

    public override void _Ready()
    {
        _perfEnabled = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--perf") >= 0;

        UiState.MenuOpen = false;
        UiState.Paused = false;
        UiState.InventoryOpen = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;

        var settings = GameSettings.Instance;
        settings?.ApplyDisplay();

        SetupEnvironment();

        int seed = SaveSystem.Instance?.Seed ?? 1337;
        GD.Print($"[Main] Мир, seed = {seed}, режим = {(GameSettings.IsCreative ? "творческий" : "выживание")}");

        _generator = new WorldGenerator { Name = "WorldGenerator" };
        AddChild(_generator);
        _generator.Configure(seed);

        if (_generator.PrintHeightMapOnStart)
        {
            _generator.PrintHeightMap();
        }

        _chunkManager = new ChunkManager { Name = "ChunkManager" };
        AddChild(_chunkManager);
        _chunkManager.Initialize(_generator);

        SpawnPlayer();

        _inventory = new Inventory();
        if (GameSettings.IsCreative)
        {
            _inventory.FillCreativeDefaults();
        }
        else
        {
            _inventory.SetSlot(0, BlockId.Dirt, 32);
            _inventory.SetSlot(1, BlockId.Stone, 16);
            _inventory.SetSlot(2, BlockId.Sand, 16);
        }

        _inventory.Selected = 0;
        _player.Initialize(_inventory);
        _interaction.Initialize(_inventory);

        _hud = new Hud { Name = "Hud" };
        AddChild(_hud);
        _hud.Initialize(_player, _interaction, _inventory);

        _pause = new PauseMenu { Name = "PauseMenu" };
        AddChild(_pause);

        GetTree().CreateTimer(3.0).Timeout += () =>
            GD.Print($"[Main] Через 3 с: чанков {_chunkManager.LoadedChunks}, " +
                     $"треугольников {_chunkManager.Triangles}, в очереди {_chunkManager.PendingChunks}");
    }

    private void SpawnPlayer()
    {
        var playerScene = GD.Load<PackedScene>("res://Scenes/Player/Player.tscn");
        if (playerScene != null)
        {
            _player = playerScene.Instantiate<PlayerController>();
        }
        else
        {
            GD.PushWarning("[Main] Player.tscn не найден, создаю игрока кодом.");
            _player = new PlayerController();
            _player.AddChild(new CollisionShape3D
            {
                Name = "Collision",
                Shape = new CapsuleShape3D { Radius = 0.35f, Height = 1.8f },
                Position = new Vector3(0f, 0.9f, 0f),
            });
            _player.AddChild(new Camera3D
            {
                Name = "Camera3D",
                Position = new Vector3(0f, 1.62f, 0f),
                Current = true,
                Far = 500f,
            });
        }

        _player.Name = "Player";
        AddChild(_player);

        float surface = _generator.GetHeightAt(8, 8);
        float spawnY = surface + (GameSettings.IsCreative ? 4f : 2.5f);

        _player.SpawnPoint = new Vector3(8.5f, surface + 1.2f, 8.5f);
        _player.GlobalPosition = new Vector3(8.5f, spawnY, 8.5f);

        _chunkManager.SetTarget(_player);

        _interaction = new BlockInteraction { Name = "BlockInteraction" };
        _player.AddChild(_interaction);
    }

    private void SetupEnvironment()
    {
        var skyMaterial = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color(0.22f, 0.42f, 0.76f),
            SkyHorizonColor = new Color(0.62f, 0.76f, 0.92f),
            GroundHorizonColor = new Color(0.5f, 0.58f, 0.5f),
            GroundBottomColor = new Color(0.22f, 0.24f, 0.22f),
            SunAngleMax = 25f,
        };

        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = skyMaterial },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 1.0f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            TonemapExposure = 0.92f,
            FogEnabled = true,
            FogLightColor = new Color(0.66f, 0.78f, 0.9f),
            FogDensity = 0.0012f,
            // Без этого туман «съедает» небо и оно становится серым.
            FogSkyAffect = 0f,
        };

        AddChild(new WorldEnvironment { Name = "WorldEnvironment", Environment = environment });

        bool shadows = GameSettings.Instance?.Shadows ?? true;

        var sun = new DirectionalLight3D
        {
            Name = "Sun",
            RotationDegrees = new Vector3(-52f, -35f, 0f),
            LightEnergy = 0.85f,
            ShadowEnabled = shadows,
            DirectionalShadowMaxDistance = 90f,
        };
        AddChild(sun);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("save_world"))
        {
            SaveSystem.Instance?.Save();
        }
    }
}
