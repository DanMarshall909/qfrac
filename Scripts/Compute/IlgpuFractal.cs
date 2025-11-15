using System;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Algorithms;
using NumericsVector4 = System.Numerics.Vector4;
using NumericsVector3 = System.Numerics.Vector3;

namespace QFrac.Compute;

/// <summary>
/// Manages ILGPU context and 3D quaternion fractal kernel using raymarching.
/// Renders Mandelbulb and quaternion Julia sets in 3D space.
/// </summary>
public sealed class IlgpuFractal : IDisposable
{
    private readonly Context _context;
    private readonly Accelerator _accelerator;
    private readonly Action<Index1D, ArrayView<NumericsVector4>, int, int, float, NumericsVector3, NumericsVector3, float> _kernel;

    public float Power { get; set; } = 8.0f;
    public int MaxIterations { get; set; } = 15;
    public float BailoutRadius { get; set; } = 2.0f;
    public NumericsVector3 CameraPosition { get; set; } = new NumericsVector3(0, 0, -3);
    public NumericsVector3 CameraTarget { get; set; } = new NumericsVector3(0, 0, 0);
    public float FieldOfView { get; set; } = 60.0f;

    public IlgpuFractal()
    {
        _context = Context.CreateDefault();
        _accelerator = CreatePreferredAccelerator(_context);
        _kernel = _accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<NumericsVector4>, int, int, float, NumericsVector3, NumericsVector3, float>(MandelbulbKernel);
    }

    public int Width { get; private set; }
    public int Height { get; private set; }

    public NumericsVector4[] Generate(int width, int height, float time)
    {
        Width = width;
        Height = height;

        int total = width * height;
        using var buffer = _accelerator.Allocate1D<NumericsVector4>(total);

        _kernel(total, buffer.View, width, height, time, CameraPosition, CameraTarget, Power);
        _accelerator.Synchronize();

        return buffer.GetAsArray1D();
    }

    public byte[] GenerateRgbaImage(int width, int height, float time)
    {
        var pixels = Generate(width, height, time);
        var data = new byte[pixels.Length * 4];

        for (int i = 0; i < pixels.Length; i++)
        {
            var color = pixels[i];
            int baseIndex = i * 4;
            data[baseIndex + 0] = ClampToByte(color.X);
            data[baseIndex + 1] = ClampToByte(color.Y);
            data[baseIndex + 2] = ClampToByte(color.Z);
            data[baseIndex + 3] = ClampToByte(color.W);
        }

        return data;
    }

    // Helper: Vector3 operations for ILGPU
    private static float Length(NumericsVector3 v) =>
        XMath.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    private static NumericsVector3 Normalize(NumericsVector3 v)
    {
        float len = Length(v);
        return len > 0 ? new NumericsVector3(v.X / len, v.Y / len, v.Z / len) : v;
    }

    private static float Dot(NumericsVector3 a, NumericsVector3 b) =>
        a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    private static NumericsVector3 Cross(NumericsVector3 a, NumericsVector3 b) =>
        new NumericsVector3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );

    // Mandelbulb distance estimation
    private static float MandelbulbDE(NumericsVector3 pos, float power, int maxIter)
    {
        NumericsVector3 z = pos;
        float dr = 1.0f;
        float r = 0.0f;

        for (int i = 0; i < maxIter; i++)
        {
            r = Length(z);
            if (r > 2.0f) break;

            // Convert to polar coordinates
            float theta = XMath.Acos(z.Z / r);
            float phi = XMath.Atan2(z.Y, z.X);
            dr = XMath.Pow(r, power - 1.0f) * power * dr + 1.0f;

            // Scale and rotate the point
            float zr = XMath.Pow(r, power);
            theta = theta * power;
            phi = phi * power;

            // Convert back to cartesian coordinates
            float sinTheta = XMath.Sin(theta);
            z = new NumericsVector3(
                sinTheta * XMath.Cos(phi),
                sinTheta * XMath.Sin(phi),
                XMath.Cos(theta)
            );
            z = new NumericsVector3(z.X * zr + pos.X, z.Y * zr + pos.Y, z.Z * zr + pos.Z);
        }

        return 0.5f * XMath.Log(r) * r / dr;
    }

    // Raymarching
    private static float Raymarch(NumericsVector3 ro, NumericsVector3 rd, float power, int maxIter)
    {
        float t = 0.0f;
        const int maxSteps = 100;
        const float minDist = 0.001f;
        const float maxDist = 10.0f;

        for (int i = 0; i < maxSteps; i++)
        {
            NumericsVector3 pos = new NumericsVector3(
                ro.X + rd.X * t,
                ro.Y + rd.Y * t,
                ro.Z + rd.Z * t
            );
            float d = MandelbulbDE(pos, power, maxIter);

            if (d < minDist)
                return t;

            t += d;

            if (t > maxDist)
                break;
        }

        return -1.0f;
    }

    // Calculate normal using gradient
    private static NumericsVector3 CalculateNormal(NumericsVector3 p, float power, int maxIter)
    {
        const float eps = 0.001f;
        float d = MandelbulbDE(p, power, maxIter);

        return Normalize(new NumericsVector3(
            MandelbulbDE(new NumericsVector3(p.X + eps, p.Y, p.Z), power, maxIter) - d,
            MandelbulbDE(new NumericsVector3(p.X, p.Y + eps, p.Z), power, maxIter) - d,
            MandelbulbDE(new NumericsVector3(p.X, p.Y, p.Z + eps), power, maxIter) - d
        ));
    }

    private static void MandelbulbKernel(
        Index1D index,
        ArrayView<NumericsVector4> output,
        int width,
        int height,
        float time,
        NumericsVector3 camPos,
        NumericsVector3 camTarget,
        float power)
    {
        int x = index % width;
        int y = index / width;

        // Normalized pixel coordinates (from -1 to 1)
        float u = (x / (float)width) * 2.0f - 1.0f;
        float v = (y / (float)height) * 2.0f - 1.0f;
        v *= (float)height / (float)width; // Aspect ratio correction

        // Camera setup
        NumericsVector3 forward = Normalize(new NumericsVector3(
            camTarget.X - camPos.X,
            camTarget.Y - camPos.Y,
            camTarget.Z - camPos.Z
        ));
        NumericsVector3 right = Normalize(Cross(forward, new NumericsVector3(0, 1, 0)));
        NumericsVector3 up = Cross(right, forward);

        // Ray direction
        NumericsVector3 rd = Normalize(new NumericsVector3(
            forward.X + right.X * u + up.X * v,
            forward.Y + right.Y * u + up.Y * v,
            forward.Z + right.Z * u + up.Z * v
        ));

        // Raymarch
        float t = Raymarch(camPos, rd, power, 15);

        NumericsVector4 color;
        if (t > 0.0f)
        {
            // Hit the fractal
            NumericsVector3 hitPos = new NumericsVector3(
                camPos.X + rd.X * t,
                camPos.Y + rd.Y * t,
                camPos.Z + rd.Z * t
            );

            NumericsVector3 normal = CalculateNormal(hitPos, power, 15);

            // Simple lighting
            NumericsVector3 lightDir = Normalize(new NumericsVector3(1, 1, -1));
            float diff = XMath.Max(0.0f, Dot(normal, lightDir));
            float amb = 0.3f;

            // Color based on position and normal
            float colorFactor = (normal.X * 0.5f + 0.5f) * 0.7f +
                               (normal.Y * 0.5f + 0.5f) * 0.2f +
                               (normal.Z * 0.5f + 0.5f) * 0.1f;

            float brightness = amb + diff * 0.7f;
            color = new NumericsVector4(
                colorFactor * brightness,
                (1.0f - colorFactor) * brightness,
                XMath.Sin(hitPos.Z * 2.0f) * 0.5f + 0.5f * brightness,
                1.0f
            );
        }
        else
        {
            // Background
            float bg = v * 0.3f + 0.2f;
            color = new NumericsVector4(bg, bg * 0.9f, bg * 1.1f, 1.0f);
        }

        output[index] = color;
    }

    private static Accelerator CreatePreferredAccelerator(Context context)
    {
        var device = context.GetPreferredDevice(preferCPU: false);
        return device.CreateAccelerator(context);
    }

    public void Dispose()
    {
        _accelerator.Dispose();
        _context.Dispose();
    }

    private static byte ClampToByte(float value) =>
        (byte)Math.Clamp(value * 255f, 0f, 255f);
}
