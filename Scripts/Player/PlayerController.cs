using Godot;
using VoxelCraft.Core;

namespace VoxelCraft.Player;

/// <summary>
/// Контроллер игрока: ходьба, прыжок, полёт (творческий режим), урон от падения.
/// Мышь захватывается при старте; Esc/паузу обрабатывает PauseMenu.
/// </summary>
public partial class PlayerController : CharacterBody3D
{
    [Export] public float WalkSpeed = 5.5f;
    [Export] public float SprintSpeed = 9.0f;
    [Export] public float FlySpeed = 14.0f;
    [Export] public float FlyBoostMultiplier = 3.0f;
    [Export] public float JumpVelocity = 8.5f;
    [Export] public float Gravity = 26.0f;

    public const float MaxHealth = 20f;

    private Camera3D _camera;
    private Inventory _inventory;
    private float _pitch;
    private bool _flying;
    private float _fallStartY;
    private bool _wasOnFloor;
    private bool _falling;

    public float Health { get; private set; } = MaxHealth;
    public bool IsFlying => _flying;
    public Camera3D Camera => _camera;
    public Vector3 SpawnPoint { get; set; }

    public override void _Ready()
    {
        _camera = GetNodeOrNull<Camera3D>("Camera3D");
        var settings = GameSettings.Instance;

        if (settings != null && _camera != null)
        {
            _camera.Fov = settings.Fov;
        }

        // Выживание начинается на земле, творческий — в полёте (чанки ещё грузятся).
        _flying = GameSettings.IsCreative;

        // Курсор НЕ захватываем автоматически: игрок кликает сам.
        // Иначе при потере фокуса окна курсор «залипает» и кажется, что мышь умерла.
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public override void _Process(double delta)
    {
        var settings = GameSettings.Instance;
        if (settings != null && _camera != null && !Mathf.IsEqualApprox(_camera.Fov, settings.Fov))
        {
            _camera.Fov = settings.Fov;
        }
    }

    public void Initialize(Inventory inventory)
    {
        _inventory = inventory;
    }

    public void Respawn()
    {
        Health = MaxHealth;
        Velocity = Vector3.Zero;
        GlobalPosition = SpawnPoint;
        _falling = false;
        _wasOnFloor = false;
    }

    public void Damage(float amount)
    {
        if (Health <= 0f) return;
        Health = Mathf.Max(0f, Health - amount);
        if (Health <= 0f) Respawn();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
        {
            float sensitivity = GameSettings.Instance?.MouseSensitivity ?? 0.0022f;
            RotateY(-motion.Relative.X * sensitivity);

            _pitch = Mathf.Clamp(
                _pitch - motion.Relative.Y * sensitivity,
                -Mathf.Pi / 2f + 0.01f,
                Mathf.Pi / 2f - 0.01f);

            if (_camera != null)
            {
                _camera.Rotation = new Vector3(_pitch, 0f, 0f);
            }

            return;
        }

        // Клик по окну захватывает мышь (если не открыт инвентарь/пауза).
        if (@event is InputEventMouseButton { Pressed: true } &&
            Input.MouseMode == Input.MouseModeEnum.Visible &&
            !UiState.AnyPanelOpen &&
            !GetTree().Paused)
        {
            Input.MouseMode = Input.MouseModeEnum.Captured;
            return;
        }

        // Переключатель полёта есть только в творческом режиме.
        if (GameSettings.IsCreative && @event.IsActionPressed("toggle_fly"))
        {
            _flying = !_flying;
            Velocity = Vector3.Zero;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        Vector2 input = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
        Vector3 direction = GlobalBasis * new Vector3(input.X, 0f, input.Y);
        if (direction.LengthSquared() > 1f) direction = direction.Normalized();

        if (_flying)
        {
            float speed = FlySpeed * (Input.IsActionPressed("sprint") ? FlyBoostMultiplier : 1f);
            Vector3 velocity = direction * speed;
            if (Input.IsActionPressed("jump")) velocity.Y += speed;
            if (Input.IsActionPressed("descend")) velocity.Y -= speed;

            Velocity = velocity;
            MoveAndSlide();
            return;
        }

        float walkSpeed = Input.IsActionPressed("sprint") ? SprintSpeed : WalkSpeed;
        Vector3 horizontal = direction * walkSpeed;

        Velocity = new Vector3(horizontal.X, Velocity.Y, horizontal.Z);

        bool onFloor = IsOnFloor();

        if (!onFloor)
        {
            if (!_wasOnFloor && !_falling)
            {
                _falling = true;
                _fallStartY = GlobalPosition.Y;
            }

            Velocity = new Vector3(Velocity.X, Velocity.Y - Gravity * dt, Velocity.Z);
        }
        else
        {
            if (_falling)
            {
                float distance = _fallStartY - GlobalPosition.Y;
                if (distance > 3.5f)
                {
                    Damage((distance - 3.5f) * 2.4f);
                }

                _falling = false;
            }

            if (Velocity.Y < 0f)
            {
                Velocity = new Vector3(Velocity.X, 0f, Velocity.Z);
            }

            if (Input.IsActionPressed("jump"))
            {
                Velocity = new Vector3(Velocity.X, JumpVelocity, Velocity.Z);
                _falling = false;
            }
        }

        _wasOnFloor = onFloor;
        MoveAndSlide();
    }
}
