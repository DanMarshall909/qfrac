namespace QFrac;

#nullable enable

using Godot;
using QFrac.Compute;
using System;

public partial class Main : Node3D
{
    private IlgpuFractal? _fractal;
    private MeshInstance3D? _mesh;
    private Camera3D? _camera;
    private ImageTexture? _texture;
    private StandardMaterial3D? _material;

    private float _time = 0f;
    private int _renderWidth = 800;
    private int _renderHeight = 600;

    // Camera control
    private Vector3 _cameraPos = new Vector3(0, 0, 3);
    private float _yaw = 0f;
    private float _pitch = 0f;
    private float _moveSpeed = 2.0f;
    private float _lookSpeed = 0.002f;
    private bool _mouseCaptured = false;

    public override void _Ready()
    {
        GD.Print("qfrac starting up - Initializing 3D Mandelbulb viewer");

        // Initialize fractal computer
        _fractal = new IlgpuFractal();

        // Create camera
        _camera = new Camera3D();
        _camera.Position = _cameraPos;
        AddChild(_camera);

        // Create a fullscreen quad mesh
        var quadMesh = new QuadMesh();
        quadMesh.Size = new Vector2(4, 3); // 4:3 aspect ratio, larger for fullscreen effect

        _mesh = new MeshInstance3D();
        _mesh.Mesh = quadMesh;
        _mesh.Position = new Vector3(0, 0, -2); // Position in front of camera
        AddChild(_mesh);

        // Create material with texture
        _material = new StandardMaterial3D();
        _material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        _material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        _mesh.MaterialOverride = _material;

        // Create initial texture
        _texture = new ImageTexture();
        UpdateFractalTexture();

        GD.Print("Initialization complete!");
        GD.Print("=== CONTROLS ===");
        GD.Print("WASD/Space/Shift - Move camera");
        GD.Print("Mouse - Look around");
        GD.Print("Q/E - Change fractal power");
        GD.Print("1/2 - Cycle Formula 1 (left bracket)");
        GD.Print("3/4 - Cycle Formula 2 (right bracket)");
        GD.Print("Z/X - Adjust morph amount between formulas");
        GD.Print("R - Reset to defaults");
        GD.Print("ESC - Toggle mouse capture");
        GD.Print($"Starting with: {_fractal.Formula1} -> {_fractal.Formula2}, Morph: {_fractal.MorphAmount:F2}");

        Input.MouseMode = Input.MouseModeEnum.Captured;
        _mouseCaptured = true;
    }

    public override void _Process(double delta)
    {
        _time += (float)delta;

        HandleInput((float)delta);
        UpdateFractalTexture();
    }

    private void HandleInput(float delta)
    {
        if (_fractal == null || _camera == null) return;

        // Toggle mouse capture
        if (Input.IsActionJustPressed("ui_cancel"))
        {
            _mouseCaptured = !_mouseCaptured;
            Input.MouseMode = _mouseCaptured ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
        }

        // Movement
        var velocity = Vector3.Zero;
        if (Input.IsKeyPressed(Key.W)) velocity.Z -= 1;
        if (Input.IsKeyPressed(Key.S)) velocity.Z += 1;
        if (Input.IsKeyPressed(Key.A)) velocity.X -= 1;
        if (Input.IsKeyPressed(Key.D)) velocity.X += 1;
        if (Input.IsKeyPressed(Key.Space)) velocity.Y += 1;
        if (Input.IsKeyPressed(Key.Shift)) velocity.Y -= 1;

        // Transform velocity to camera space
        var forward = -_camera.GlobalTransform.Basis.Z;
        var right = _camera.GlobalTransform.Basis.X;
        var up = Vector3.Up;

        _cameraPos += (forward * velocity.Z + right * velocity.X + up * velocity.Y) * _moveSpeed * delta;

        // Update camera
        _camera.Position = _cameraPos;
        _camera.Rotation = new Vector3(_pitch, _yaw, 0);

        // Update fractal camera
        var camTarget = _cameraPos + forward;
        _fractal.CameraPosition = new System.Numerics.Vector3(_cameraPos.X, _cameraPos.Y, _cameraPos.Z);
        _fractal.CameraTarget = new System.Numerics.Vector3(camTarget.X, camTarget.Y, camTarget.Z);

        // Change fractal power
        if (Input.IsKeyPressed(Key.Q))
        {
            _fractal.Power = Mathf.Max(2.0f, _fractal.Power - delta * 2.0f);
            GD.Print($"Power: {_fractal.Power:F2}");
        }
        if (Input.IsKeyPressed(Key.E))
        {
            _fractal.Power = Mathf.Min(16.0f, _fractal.Power + delta * 2.0f);
            GD.Print($"Power: {_fractal.Power:F2}");
        }

        // Cycle Formula 1
        if (Input.IsActionJustPressed("ui_text_backspace") || Input.IsKeyPressed(Key.Key1))
        {
            _fractal.Formula1 = (QFrac.Compute.FractalFormula)(((int)_fractal.Formula1 + 4) % 5);
            GD.Print($"Formula 1: {_fractal.Formula1} -> {_fractal.Formula2}, Morph: {_fractal.MorphAmount:F2}");
        }
        if (Input.IsKeyPressed(Key.Key2))
        {
            _fractal.Formula1 = (QFrac.Compute.FractalFormula)(((int)_fractal.Formula1 + 1) % 5);
            GD.Print($"Formula 1: {_fractal.Formula1} -> {_fractal.Formula2}, Morph: {_fractal.MorphAmount:F2}");
        }

        // Cycle Formula 2
        if (Input.IsKeyPressed(Key.Key3))
        {
            _fractal.Formula2 = (QFrac.Compute.FractalFormula)(((int)_fractal.Formula2 + 4) % 5);
            GD.Print($"Formula 2: {_fractal.Formula1} -> {_fractal.Formula2}, Morph: {_fractal.MorphAmount:F2}");
        }
        if (Input.IsKeyPressed(Key.Key4))
        {
            _fractal.Formula2 = (QFrac.Compute.FractalFormula)(((int)_fractal.Formula2 + 1) % 5);
            GD.Print($"Formula 2: {_fractal.Formula1} -> {_fractal.Formula2}, Morph: {_fractal.MorphAmount:F2}");
        }

        // Adjust morph amount
        if (Input.IsKeyPressed(Key.Z))
        {
            _fractal.MorphAmount = Mathf.Max(0.0f, _fractal.MorphAmount - delta * 0.5f);
            GD.Print($"Morph: {_fractal.MorphAmount:F2} ({_fractal.Formula1} -> {_fractal.Formula2})");
        }
        if (Input.IsKeyPressed(Key.X))
        {
            _fractal.MorphAmount = Mathf.Min(1.0f, _fractal.MorphAmount + delta * 0.5f);
            GD.Print($"Morph: {_fractal.MorphAmount:F2} ({_fractal.Formula1} -> {_fractal.Formula2})");
        }

        // Reset to defaults
        if (Input.IsActionJustPressed("ui_home") || Input.IsKeyPressed(Key.R))
        {
            _fractal.Formula1 = QFrac.Compute.FractalFormula.Mandelbulb;
            _fractal.Formula2 = QFrac.Compute.FractalFormula.BurningShip;
            _fractal.MorphAmount = 0.0f;
            _fractal.Power = 8.0f;
            _cameraPos = new Vector3(0, 0, 3);
            _yaw = 0f;
            _pitch = 0f;
            GD.Print("Reset to defaults");
            GD.Print($"{_fractal.Formula1} -> {_fractal.Formula2}, Morph: {_fractal.MorphAmount:F2}, Power: {_fractal.Power:F2}");
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!_mouseCaptured) return;

        if (@event is InputEventMouseMotion mouseMotion)
        {
            _yaw -= mouseMotion.Relative.X * _lookSpeed;
            _pitch -= mouseMotion.Relative.Y * _lookSpeed;
            _pitch = Mathf.Clamp(_pitch, -Mathf.Pi / 2, Mathf.Pi / 2);
        }
    }

    private void UpdateFractalTexture()
    {
        if (_fractal == null || _material == null || _texture == null) return;

        try
        {
            // Generate fractal image
            var rgbaData = _fractal.GenerateRgbaImage(_renderWidth, _renderHeight, _time);

            // Create Godot image from RGBA data
            var image = Image.CreateFromData(_renderWidth, _renderHeight, false, Image.Format.Rgba8, rgbaData);

            // Update texture
            _texture = ImageTexture.CreateFromImage(image);
            _material.AlbedoTexture = _texture;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"Error updating fractal texture: {ex.Message}");
        }
    }

    public override void _ExitTree()
    {
        _fractal?.Dispose();
    }
}
