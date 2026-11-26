using Godot;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using VoxelCraft.Core;
using VoxelCraft.Data;

namespace VoxelCraft.World;

/// <summary>
/// Загружает/выгружает чанки вокруг игрока.
///
/// Схема потоков (этап 6):
///   • фоновый поток — только данные: BlockId[] + «сырой» меш (массивы);
///   • главный поток — создание узлов, ArrayMesh, коллизии, AddChild.
/// Так мы никогда не трогаем SceneTree из фонового потока.
/// </summary>
public partial class ChunkManager : Node3D
{
    public static ChunkManager Instance { get; private set; }

    [Export] public int ViewDistanceInChunks = 5;
    [Export] public int MaxChunksPerFrame = 2;
    [Export] public int MaxConcurrentJobs = 3;
    [Export] public bool GenerateCollision = true;

    private sealed class BuildResult
    {
        public Vector2I Coord;
        public BlockId[] Blocks;
        public ChunkMeshData Mesh;
        public Exception Error;
    }

    private readonly Dictionary<Vector2I, Chunk> _chunks = new();
    private readonly HashSet<Vector2I> _pending = new();
    private readonly Queue<Vector2I> _queue = new();
    private readonly ConcurrentQueue<BuildResult> _results = new();

    private WorldGenerator _generator;
    private Node3D _target;
    private int _jobsInFlight;
    private int _triangles;
    private float _refreshTimer;
    private Vector2I _lastCenter = new(int.MinValue / 2, int.MinValue / 2);
    private int _lastRadius = -1;

    public int LoadedChunks => _chunks.Count;
    public int PendingChunks => _queue.Count + _jobsInFlight;
    public int Triangles => _triangles;

    public override void _Ready()
    {
        Instance = this;
        ProcessPriority = 100;

        var settings = GameSettings.Instance;
        if (settings != null)
        {
            ViewDistanceInChunks = settings.RenderDistance;
            ChunkMeshBuilder.UseGreedy = settings.GreedyMeshing;
        }
    }

    public void Initialize(WorldGenerator generator) => _generator = generator;

    public void SetTarget(Node3D target)
    {
        _target = target;
        _refreshTimer = 0f;
        _lastCenter = new Vector2I(int.MinValue / 2, int.MinValue / 2);
    }

    public static Vector2I WorldToChunk(Vector3 worldPosition) => new(
        Mathf.FloorToInt(worldPosition.X / Chunk.SizeX),
        Mathf.FloorToInt(worldPosition.Z / Chunk.SizeZ));

    // ------------------------------------------------------------------ кадр

    public override void _Process(double delta)
    {
        DrainResults(MaxChunksPerFrame);

        if (_target == null || _generator == null) return;

        _refreshTimer -= (float)delta;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = 0.2f;
            RefreshQueue();
        }

        StartJobs();
    }

    /// <summary>Ставит в очередь недостающие чанки и выгружает далёкие.</summary>
    private void RefreshQueue()
    {
        var center = WorldToChunk(_target.GlobalPosition);
        // Дальность может поменяться в настройках прямо в игре.
        int currentRadius = GameSettings.Instance?.RenderDistance ?? ViewDistanceInChunks;

        if (center != _lastCenter || currentRadius != _lastRadius)
        {
            _lastCenter = center;
            _lastRadius = currentRadius;

            var candidates = new List<(Vector2I Coord, int Distance)>();
            int radius = currentRadius;
            float maxDistanceSq = (radius + 0.5f) * (radius + 0.5f);

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    if (dx * dx + dz * dz > maxDistanceSq) continue;

                    var coord = new Vector2I(center.X + dx, center.Y + dz);
                    if (_chunks.ContainsKey(coord) || _pending.Contains(coord)) continue;

                    candidates.Add((coord, dx * dx + dz * dz));
                }
            }

            // Ближние чанки — первыми.
            candidates.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            foreach (var candidate in candidates)
            {
                _queue.Enqueue(candidate.Coord);
                _pending.Add(candidate.Coord);
            }
        }

        // Выгружаем с запасом, чтобы чанки не «дёргались» на границе радиуса.
        int unloadDistance = currentRadius + 2;
        List<Vector2I> toRemove = null;

        foreach (var pair in _chunks)
        {
            int dx = Math.Abs(pair.Key.X - center.X);
            int dz = Math.Abs(pair.Key.Y - center.Y);
            if (dx > unloadDistance || dz > unloadDistance)
            {
                (toRemove ??= new List<Vector2I>()).Add(pair.Key);
            }
        }

        if (toRemove == null) return;

        foreach (var coord in toRemove)
        {
            if (!_chunks.TryGetValue(coord, out var chunk)) continue;

            _triangles -= chunk.TriangleCount;
            _chunks.Remove(coord);
            chunk.QueueFree();
        }
    }

    // ------------------------------------------------------- фоновые задания

    private void StartJobs()
    {
        while (_jobsInFlight < MaxConcurrentJobs && _queue.Count > 0)
        {
            var coord = _queue.Dequeue();
            _jobsInFlight++;

            // Копии соседей делаем в главном потоке — дальше фоновый поток
            // работает только со своими массивами.
            var input = CreateMeshInput(coord);
            var generator = _generator;
            var modifications = SaveSystem.Instance?.ModificationsFor(coord);
            var jobCoord = coord;

            Task.Run(() =>
            {
                var result = new BuildResult { Coord = jobCoord };
                try
                {
                    generator.FillChunk(input.Blocks, jobCoord);
                    ApplyModifications(input.Blocks, jobCoord, modifications);
                    result.Blocks = input.Blocks;
                    result.Mesh = ChunkMeshBuilder.Build(input);
                }
                catch (Exception exception)
                {
                    result.Error = exception;
                }

                _results.Enqueue(result);
            });
        }
    }

    private ChunkMeshInput CreateMeshInput(Vector2I coord)
    {
        return new ChunkMeshInput
        {
            Blocks = new BlockId[Chunk.Volume],
            NegX = CopyBlocks(coord + new Vector2I(-1, 0)),
            PosX = CopyBlocks(coord + new Vector2I(1, 0)),
            NegZ = CopyBlocks(coord + new Vector2I(0, -1)),
            PosZ = CopyBlocks(coord + new Vector2I(0, 1)),
        };
    }

    private BlockId[] CopyBlocks(Vector2I coord)
    {
        if (!_chunks.TryGetValue(coord, out var chunk) || chunk.Blocks == null) return null;
        return (BlockId[])chunk.Blocks.Clone();
    }

    private static void ApplyModifications(
        BlockId[] blocks, Vector2I coord, Dictionary<Vector3I, BlockId> modifications)
    {
        if (modifications == null) return;

        foreach (var pair in modifications)
        {
            int localX = pair.Key.X - coord.X * Chunk.SizeX;
            int localY = pair.Key.Y;
            int localZ = pair.Key.Z - coord.Y * Chunk.SizeZ;

            if (!Chunk.InBounds(localX, localY, localZ)) continue;
            blocks[Chunk.Index(localX, localY, localZ)] = pair.Value;
        }
    }

    /// <summary>Забирает готовые результаты. Только главный поток.</summary>
    private void DrainResults(int maxPerFrame)
    {
        int handled = 0;

        while (handled < maxPerFrame && _results.TryDequeue(out var result))
        {
            handled++;
            if (_jobsInFlight > 0) _jobsInFlight--;
            _pending.Remove(result.Coord);

            if (result.Error != null)
            {
                GD.PushError($"[ChunkManager] Ошибка генерации чанка {result.Coord}: {result.Error}");
                continue;
            }

            if (_chunks.ContainsKey(result.Coord)) continue;

            var chunk = new Chunk();
            chunk.Init(result.Coord, result.Blocks);
            AddChild(chunk);
            chunk.ApplyMesh(result.Mesh, GenerateCollision);

            _chunks[result.Coord] = chunk;
            _triangles += chunk.TriangleCount;
        }
    }

    // -------------------------------------------------------- доступ к миру

    public bool HasChunk(Vector2I coord) => _chunks.ContainsKey(coord);

    public BlockId GetBlock(Vector3I worldPosition)
    {
        if (worldPosition.Y < 0) return BlockId.Stone;
        if (worldPosition.Y >= Chunk.SizeY) return BlockId.Air;

        var coord = ChunkCoordOf(worldPosition);
        if (!_chunks.TryGetValue(coord, out var chunk)) return BlockId.Air;

        int localX = worldPosition.X - coord.X * Chunk.SizeX;
        int localZ = worldPosition.Z - coord.Y * Chunk.SizeZ;
        return chunk.GetBlockRaw(localX, worldPosition.Y, localZ);
    }

    public static Vector2I ChunkCoordOf(Vector3I worldPosition) => new(
        Mathf.FloorToInt(worldPosition.X / (float)Chunk.SizeX),
        Mathf.FloorToInt(worldPosition.Z / (float)Chunk.SizeZ));

    /// <summary>
    /// Ставит блок и перестраивает меш этого чанка.
    /// Если блок на границе — перестраивается и соседний чанк.
    /// </summary>
    public bool SetBlock(Vector3I worldPosition, BlockId id, bool record = true)
    {
        if (worldPosition.Y < 1 || worldPosition.Y >= Chunk.SizeY) return false;

        var coord = ChunkCoordOf(worldPosition);
        if (!_chunks.TryGetValue(coord, out var chunk)) return false;

        int localX = worldPosition.X - coord.X * Chunk.SizeX;
        int localZ = worldPosition.Z - coord.Y * Chunk.SizeZ;

        if (chunk.GetBlockRaw(localX, worldPosition.Y, localZ) == id) return false;

        chunk.SetBlockRaw(localX, worldPosition.Y, localZ, id);

        // Сохраняем только изменения, а не весь мир (этап 9).
        if (record) SaveSystem.Instance?.Record(worldPosition, id);

        RebuildChunk(chunk);

        if (localX == 0) RebuildNeighbor(coord + new Vector2I(-1, 0));
        if (localX == Chunk.SizeX - 1) RebuildNeighbor(coord + new Vector2I(1, 0));
        if (localZ == 0) RebuildNeighbor(coord + new Vector2I(0, -1));
        if (localZ == Chunk.SizeZ - 1) RebuildNeighbor(coord + new Vector2I(0, 1));

        return true;
    }

    private void RebuildNeighbor(Vector2I coord)
    {
        if (_chunks.TryGetValue(coord, out var chunk))
        {
            RebuildChunk(chunk);
        }
    }

    /// <summary>
    /// Синхронная перестройка одного чанка в главном потоке.
    /// Вызывается только при изменении блока игроком (редкое событие).
    /// </summary>
    private void RebuildChunk(Chunk chunk)
    {
        var input = new ChunkMeshInput
        {
            Blocks = chunk.Blocks,
            NegX = GetLiveBlocks(chunk.Coord + new Vector2I(-1, 0)),
            PosX = GetLiveBlocks(chunk.Coord + new Vector2I(1, 0)),
            NegZ = GetLiveBlocks(chunk.Coord + new Vector2I(0, -1)),
            PosZ = GetLiveBlocks(chunk.Coord + new Vector2I(0, 1)),
        };

        var mesh = ChunkMeshBuilder.Build(input);

        _triangles -= chunk.TriangleCount;
        chunk.ApplyMesh(mesh, GenerateCollision);
        _triangles += chunk.TriangleCount;
    }

    private BlockId[] GetLiveBlocks(Vector2I coord) =>
        _chunks.TryGetValue(coord, out var chunk) ? chunk.Blocks : null;
}
