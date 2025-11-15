# QFrac - 3D Quaternion Fractal Viewer

A real-time 3D fractal viewer built with Godot 4.2.1 and C#, featuring GPU-accelerated raymarching of Mandelbulb fractals using ILGPU.

## Features

- **True 3D Rendering**: View Mandelbulb fractals as actual 3D objects, not 2D projections
- **GPU Acceleration**: ILGPU-powered compute kernels with CUDA support (CPU fallback available)
- **Raymarching**: Distance estimation-based rendering for smooth fractal surfaces
- **Interactive Navigation**: First-person camera controls for exploring fractal geometry
- **Real-time Parameters**: Adjust fractal power on-the-fly (Mandelbulb power from 2.0 to 16.0)
- **Dynamic Lighting**: Surface normal calculation with diffuse and ambient lighting

## Technical Architecture

### Components

1. **IlgpuFractal.cs** (`Scripts/Compute/`)
   - GPU compute kernel using ILGPU
   - Implements Mandelbulb distance estimation
   - Raymarching algorithm for 3D rendering
   - Camera-aware rendering (perspective projection)

2. **Main.cs** (`Scripts/`)
   - Godot integration layer
   - Camera and input management
   - Per-frame texture streaming from GPU
   - Material and mesh setup

3. **Main.tscn** (`scenes/`)
   - Main 3D scene
   - Configured for forward+ rendering

## Requirements

- **Godot**: 4.2.1 or later with C# support
- **.NET**: 6.0 or later
- **GPU**: CUDA-capable GPU recommended (CPU fallback supported)

## Building and Running

1. **Open Project**
   ```
   Open the project folder in Godot 4.2.1+
   ```

2. **Build C# Solution**
   ```
   In Godot: Project → Tools → C# → Build Solution
   ```

3. **Run**
   ```
   Press F5 or click the Run button
   ```

## Controls

| Input | Action |
|-------|--------|
| **W/A/S/D** | Move camera forward/left/back/right |
| **Space** | Move camera up |
| **Shift** | Move camera down |
| **Mouse** | Look around (first-person view) |
| **Q** | Decrease fractal power |
| **E** | Increase fractal power |
| **ESC** | Toggle mouse capture |

## How It Works

### Mandelbulb Algorithm

The Mandelbulb is a 3D fractal defined by iterating the formula:

```
z = z^n + c
```

Where `z` and `c` are 3D points in space, and the power `n` (typically 8) determines the fractal's shape.

### Distance Estimation

Each pixel traces a ray through 3D space using sphere tracing:

1. Start at camera position
2. Calculate distance to nearest fractal surface
3. Step forward by that distance
4. Repeat until hitting surface or max distance

This allows efficient rendering without explicit geometry.

### GPU Pipeline

```
Main.cs (_Process)
    ↓
IlgpuFractal.Generate()
    ↓
GPU Kernel (raymarching per pixel)
    ↓
RGBA byte array
    ↓
Godot ImageTexture
    ↓
Material on quad mesh
```

## Customization

### Fractal Parameters (in `IlgpuFractal.cs`)

```csharp
public float Power { get; set; } = 8.0f;           // Mandelbulb power (2-16)
public int MaxIterations { get; set; } = 15;       // Detail level
public float BailoutRadius { get; set; } = 2.0f;   // Iteration escape threshold
```

### Camera Settings (in `Main.cs`)

```csharp
private float _moveSpeed = 2.0f;   // Camera movement speed
private float _lookSpeed = 0.002f; // Mouse sensitivity
private int _renderWidth = 800;    // Fractal resolution
private int _renderHeight = 600;
```

## Future Enhancements

- [ ] Additional fractal types (Quaternion Julia sets, Mandelbox)
- [ ] UI panel for runtime parameter control
- [ ] Color palette customization
- [ ] Animation/keyframe recording
- [ ] Screenshot/video export
- [ ] Performance optimization (adaptive quality, LOD)
- [ ] Ambient occlusion and advanced lighting

## License

This project is provided as-is for educational and research purposes.

## Credits

- Fractal mathematics based on research by Daniel White and Paul Nylander
- Built with [Godot Engine](https://godotengine.org/)
- GPU compute powered by [ILGPU](https://github.com/m4rs-mt/ILGPU)
