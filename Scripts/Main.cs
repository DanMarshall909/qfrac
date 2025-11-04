namespace QFrac;

#nullable enable

using Godot;
using QFrac.Compute;

public partial class Main : Node3D
{
    private IlgpuFractal? _fractal;

    public override void _Ready()
    {
        GD.Print("qfrac starting up");
        _fractal = new IlgpuFractal();
        var sample = _fractal.Generate(8, 8, 0f);
        GD.Print($"Sample fractal color: {sample[0]}");
    }

    public override void _ExitTree()
    {
        _fractal?.Dispose();
    }
}
