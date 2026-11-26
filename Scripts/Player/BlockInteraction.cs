using Godot;
using VoxelCraft.Core;
using VoxelCraft.World;

namespace VoxelCraft.Player;

/// <summary>
/// Луч из камеры: подсветка блока под прицелом, ломка и установка.
/// Творческий — мгновенно и бесконечно. Выживание — ломка по времени,
/// блоки добываются в инвентарь и тратятся при установке.
/// </summary>
public partial class BlockInteraction : Node
{
    [Export] public float ReachCreative = 8f;
    [Export] public float ReachSurvival = 6f;

    public float BreakProgress { get; private set; }
    public bool IsBreaking { get; private set; }
    public Vector3I Target { get; private set; }
    public bool HasTarget { get; private set; }

    private Camera3D _camera;
    private CharacterBody3D _player;
    private Inventory _inventory;
    private BlockHighlight _highlight;
    private bool _breaking;

    public void Initialize(Inventory inventory) => _inventory = inventory;

    public override void _Ready()
    {
        _camera = GetViewport().GetCamera3D();
        _player = GetParent() as CharacterBody3D;

        _highlight = new BlockHighlight();
        (_player ?? (Node)this).AddChild(_highlight);
    }

    public override void _Process(double delta)
    {
        bool playing = Input.MouseMode == Input.MouseModeEnum.Captured &&
                       !UiState.AnyPanelOpen && !GetTree().Paused;

        if (!playing)
        {
            _highlight?.HideBox();
            ResetBreak();
            HasTarget = false;
            return;
        }

        bool creative = GameSettings.IsCreative;
        float reach = creative ? ReachCreative : ReachSurvival;

        if (!TryRaycast(out var target, out _, reach))
        {
            _highlight?.HideBox();
            HasTarget = false;
            ResetBreak();
            return;
        }

        Target = target;
        HasTarget = true;
        _highlight?.ShowAt(target);

        if (creative)
        {
            ResetBreak();
            return;
        }

        if (!Input.IsActionPressed("break_block"))
        {
            ResetBreak();
            return;
        }

        var id = ChunkManager.Instance?.GetBlock(target) ?? BlockId.Air;
        if (id == BlockId.Air || target.Y <= 0)
        {
            ResetBreak();
            return;
        }

        if (!_breaking)
        {
            _breaking = true;
            BreakProgress = 0f;
        }

        float hardness = BlockDatabase.Get(id).Hardness;
        if (hardness <= 0f)
        {
            BreakBlock(target);
            return;
        }

        BreakProgress += (float)delta / (hardness * 0.35f);
        IsBreaking = true;

        if (BreakProgress >= 1f)
        {
            BreakBlock(target);
        }
    }

    private void ResetBreak()
    {
        BreakProgress = 0f;
        IsBreaking = false;
        _breaking = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (Input.MouseMode != Input.MouseModeEnum.Captured || UiState.AnyPanelOpen) return;

        if (GameSettings.IsCreative && @event.IsActionPressed("break_block"))
        {
            if (TryRaycast(out var block, out _, ReachCreative) && block.Y > 0)
            {
                ChunkManager.Instance?.SetBlock(block, BlockId.Air);
            }
            return;
        }

        if (@event.IsActionPressed("place_block"))
        {
            PlaceBlock();
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true } mouseButton)
        {
            if (mouseButton.ButtonIndex == MouseButton.WheelUp) CycleSelection(-1);
            else if (mouseButton.ButtonIndex == MouseButton.WheelDown) CycleSelection(1);
            return;
        }

        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            int slot = (int)key.PhysicalKeycode - 49; // ASCII '1'
            if (slot >= 0 && slot < Inventory.HotbarSize)
            {
                _inventory?.Select(slot);
            }
        }
    }

    private void CycleSelection(int step)
    {
        if (_inventory == null) return;
        _inventory.Select(_inventory.Selected + step);
    }

    private void BreakBlock(Vector3I block)
    {
        var id = ChunkManager.Instance?.GetBlock(block) ?? BlockId.Air;
        if (id == BlockId.Air) return;

        if (ChunkManager.Instance.SetBlock(block, BlockId.Air))
        {
            _inventory?.Add(id);
        }

        ResetBreak();
    }

    private void PlaceBlock()
    {
        if (!TryRaycast(out var block, out var normal, ReachCreative)) return;

        var manager = ChunkManager.Instance;
        if (manager == null) return;

        var target = block + normal;
        var existing = manager.GetBlock(target);
        if (existing != BlockId.Air && existing != BlockId.Water) return;

        if (_inventory == null) return;

        var chosen = _inventory.SelectedBlock;
        if (chosen == BlockId.Air) return;

        if (!_inventory.ConsumeSelected()) return;

        if (IntersectsPlayer(target))
        {
            _inventory.Add(chosen);
            return;
        }

        manager.SetBlock(target, chosen);
    }

    private bool TryRaycast(out Vector3I block, out Vector3I normal, float reach)
    {
        block = default;
        normal = default;

        if (_camera == null || !IsInstanceValid(_camera))
        {
            _camera = GetViewport().GetCamera3D();
        }

        if (_camera == null) return false;

        Vector3 from = _camera.GlobalPosition;
        Vector3 to = from + -_camera.GlobalBasis.Z * reach;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = false;

        var hit = _camera.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (hit.Count == 0) return false;

        var position = (Vector3)hit["position"];
        var hitNormal = (Vector3)hit["normal"];

        Vector3 inside = position - hitNormal * 0.5f;

        block = new Vector3I(
            Mathf.FloorToInt(inside.X),
            Mathf.FloorToInt(inside.Y),
            Mathf.FloorToInt(inside.Z));

        normal = new Vector3I(
            Mathf.RoundToInt(hitNormal.X),
            Mathf.RoundToInt(hitNormal.Y),
            Mathf.RoundToInt(hitNormal.Z));

        return true;
    }

    private bool IntersectsPlayer(Vector3I cell)
    {
        if (_player == null) return false;

        var playerBox = new Aabb(
            _player.GlobalPosition + new Vector3(-0.35f, 0f, -0.35f),
            new Vector3(0.7f, 1.8f, 0.7f));

        var blockBox = new Aabb(new Vector3(cell.X, cell.Y, cell.Z), Vector3.One);
        return playerBox.Intersects(blockBox);
    }
}
