using System;
using ILGPU;
using ILGPU.Runtime;
using ILGPU.Algorithms;
using NumericsVector4 = System.Numerics.Vector4;
using NumericsVector3 = System.Numerics.Vector3;

namespace QFrac.Compute;

/// <summary>
/// Fractal formula types for morphing between different fractals
/// </summary>
public enum FractalFormula
{
    Mandelbulb = 0,
    BurningShip = 1,
    Mandelbox = 2,
    SphereInversion = 3,
    QuaternionJulia = 4
}

/// <summary>
/// Manages ILGPU context and 3D quaternion fractal kernel using raymarching.
/// Renders multiple fractal types with morphing capabilities.
/// </summary>
public sealed class IlgpuFractal : IDisposable
{
    private readonly Context _context;
    private readonly Accelerator _accelerator;
    private readonly Action<Index1D, ArrayView<NumericsVector4>, int, int, float, NumericsVector3, NumericsVector3, float, int, int, float> _kernel;

    public float Power { get; set; } = 8.0f;
    public int MaxIterations { get; set; } = 15;
    public float BailoutRadius { get; set; } = 2.0f;
    public NumericsVector3 CameraPosition { get; set; } = new NumericsVector3(0, 0, -3);
    public NumericsVector3 CameraTarget { get; set; } = new NumericsVector3(0, 0, 0);
    public float FieldOfView { get; set; } = 60.0f;

    // Formula morphing
    public FractalFormula Formula1 { get; set; } = FractalFormula.Mandelbulb;
    public FractalFormula Formula2 { get; set; } = FractalFormula.BurningShip;
    public float MorphAmount { get; set; } = 0.0f; // 0 = Formula1, 1 = Formula2

    public IlgpuFractal()
    {
        _context = Context.CreateDefault();
        _accelerator = CreatePreferredAccelerator(_context);
        _kernel = _accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<NumericsVector4>, int, int, float, NumericsVector3, NumericsVector3, float, int, int, float>(FractalKernel);
    }

    public int Width { get; private set; }
    public int Height { get; private set; }

    public NumericsVector4[] Generate(int width, int height, float time)
    {
        Width = width;
        Height = height;

        int total = width * height;
        using var buffer = _accelerator.Allocate1D<NumericsVector4>(total);

        _kernel(total, buffer.View, width, height, time, CameraPosition, CameraTarget, Power,
                (int)Formula1, (int)Formula2, MorphAmount);
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

    // Burning Ship 3D distance estimation
    private static float BurningShipDE(NumericsVector3 pos, float power, int maxIter)
    {
        NumericsVector3 z = pos;
        float dr = 1.0f;
        float r = 0.0f;

        for (int i = 0; i < maxIter; i++)
        {
            r = Length(z);
            if (r > 2.0f) break;

            // Apply absolute value (burning ship characteristic)
            z = new NumericsVector3(XMath.Abs(z.X), XMath.Abs(z.Y), XMath.Abs(z.Z));

            // Convert to polar coordinates
            float theta = XMath.Acos(z.Z / XMath.Max(r, 0.0001f));
            float phi = XMath.Atan2(z.Y, z.X);
            dr = XMath.Pow(r, power - 1.0f) * power * dr + 1.0f;

            // Scale and rotate
            float zr = XMath.Pow(r, power);
            theta = theta * power;
            phi = phi * power;

            // Convert back to cartesian
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

    // Mandelbox distance estimation
    private static float MandelboxDE(NumericsVector3 pos, float power, int maxIter)
    {
        const float fixedRadius = 1.0f;
        const float minRadius = 0.5f;
        float scale = power * 0.25f; // Scale based on power parameter

        NumericsVector3 z = pos;
        float dr = 1.0f;

        for (int i = 0; i < maxIter; i++)
        {
            // Box fold
            z = new NumericsVector3(
                XMath.Clamp(z.X, -1.0f, 1.0f) * 2.0f - z.X,
                XMath.Clamp(z.Y, -1.0f, 1.0f) * 2.0f - z.Y,
                XMath.Clamp(z.Z, -1.0f, 1.0f) * 2.0f - z.Z
            );

            // Sphere fold
            float r2 = z.X * z.X + z.Y * z.Y + z.Z * z.Z;
            if (r2 < minRadius * minRadius)
            {
                float temp = (fixedRadius * fixedRadius) / (minRadius * minRadius);
                z = new NumericsVector3(z.X * temp, z.Y * temp, z.Z * temp);
                dr *= temp;
            }
            else if (r2 < fixedRadius * fixedRadius)
            {
                float temp = (fixedRadius * fixedRadius) / r2;
                z = new NumericsVector3(z.X * temp, z.Y * temp, z.Z * temp);
                dr *= temp;
            }

            z = new NumericsVector3(z.X * scale + pos.X, z.Y * scale + pos.Y, z.Z * scale + pos.Z);
            dr = dr * XMath.Abs(scale) + 1.0f;

            if (r2 > 4.0f) break;
        }

        return Length(z) / XMath.Abs(dr);
    }

    // Sphere Inversion fractal
    private static float SphereInversionDE(NumericsVector3 pos, float power, int maxIter)
    {
        NumericsVector3 z = pos;
        float dr = 1.0f;

        for (int i = 0; i < maxIter; i++)
        {
            float r2 = z.X * z.X + z.Y * z.Y + z.Z * z.Z;
            if (r2 > 4.0f) break;

            // Sphere inversion
            float k = power / r2;
            z = new NumericsVector3(z.X * k, z.Y * k, z.Z * k);
            dr = dr * k + 1.0f;

            z = new NumericsVector3(z.X + pos.X, z.Y + pos.Y, z.Z + pos.Z);
        }

        return Length(z) / XMath.Abs(dr);
    }

    // Quaternion Julia set
    private static float QuaternionJuliaDE(NumericsVector3 pos, float power, int maxIter)
    {
        // Using a predefined Julia constant
        NumericsVector3 c = new NumericsVector3(0.18f, 0.88f, 0.24f);
        NumericsVector3 z = pos;
        float dr = 1.0f;

        for (int i = 0; i < maxIter; i++)
        {
            float r = Length(z);
            if (r > 2.0f) break;

            // Quaternion-like multiplication (simplified)
            float x2 = z.X * z.X;
            float y2 = z.Y * z.Y;
            float z2 = z.Z * z.Z;

            float newX = x2 - y2 - z2;
            float newY = 2.0f * z.X * z.Y;
            float newZ = 2.0f * z.X * z.Z;

            z = new NumericsVector3(newX + c.X, newY + c.Y, newZ + c.Z);
            dr = 2.0f * r * dr + 1.0f;
        }

        return Length(z) / dr;
    }

    // Get distance based on formula type
    private static float GetFormulaDE(NumericsVector3 pos, float power, int maxIter, int formulaType)
    {
        switch (formulaType)
        {
            case 0: return MandelbulbDE(pos, power, maxIter);
            case 1: return BurningShipDE(pos, power, maxIter);
            case 2: return MandelboxDE(pos, power, maxIter);
            case 3: return SphereInversionDE(pos, power, maxIter);
            case 4: return QuaternionJuliaDE(pos, power, maxIter);
            default: return MandelbulbDE(pos, power, maxIter);
        }
    }

    // Morph between two formulas
    private static float MorphedDE(NumericsVector3 pos, float power, int maxIter, int formula1, int formula2, float morphAmount)
    {
        if (morphAmount <= 0.0f)
            return GetFormulaDE(pos, power, maxIter, formula1);
        if (morphAmount >= 1.0f)
            return GetFormulaDE(pos, power, maxIter, formula2);

        // Blend between two distance estimates
        float d1 = GetFormulaDE(pos, power, maxIter, formula1);
        float d2 = GetFormulaDE(pos, power, maxIter, formula2);

        // Linear interpolation
        return d1 * (1.0f - morphAmount) + d2 * morphAmount;
    }

    // Raymarching with morphing support
    private static float Raymarch(NumericsVector3 ro, NumericsVector3 rd, float power, int maxIter,
                                  int formula1, int formula2, float morphAmount)
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
            float d = MorphedDE(pos, power, maxIter, formula1, formula2, morphAmount);

            if (d < minDist)
                return t;

            t += d;

            if (t > maxDist)
                break;
        }

        return -1.0f;
    }

    // Calculate normal using gradient with morphing support
    private static NumericsVector3 CalculateNormal(NumericsVector3 p, float power, int maxIter,
                                                    int formula1, int formula2, float morphAmount)
    {
        const float eps = 0.001f;
        float d = MorphedDE(p, power, maxIter, formula1, formula2, morphAmount);

        return Normalize(new NumericsVector3(
            MorphedDE(new NumericsVector3(p.X + eps, p.Y, p.Z), power, maxIter, formula1, formula2, morphAmount) - d,
            MorphedDE(new NumericsVector3(p.X, p.Y + eps, p.Z), power, maxIter, formula1, formula2, morphAmount) - d,
            MorphedDE(new NumericsVector3(p.X, p.Y, p.Z + eps), power, maxIter, formula1, formula2, morphAmount) - d
        ));
    }

    private static void FractalKernel(
        Index1D index,
        ArrayView<NumericsVector4> output,
        int width,
        int height,
        float time,
        NumericsVector3 camPos,
        NumericsVector3 camTarget,
        float power,
        int formula1,
        int formula2,
        float morphAmount)
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

        // Raymarch with morphing
        float t = Raymarch(camPos, rd, power, 15, formula1, formula2, morphAmount);

        NumericsVector4 color;
        if (t > 0.0f)
        {
            // Hit the fractal
            NumericsVector3 hitPos = new NumericsVector3(
                camPos.X + rd.X * t,
                camPos.Y + rd.Y * t,
                camPos.Z + rd.Z * t
            );

            NumericsVector3 normal = CalculateNormal(hitPos, power, 15, formula1, formula2, morphAmount);

            // Simple lighting
            NumericsVector3 lightDir = Normalize(new NumericsVector3(1, 1, -1));
            float diff = XMath.Max(0.0f, Dot(normal, lightDir));
            float amb = 0.3f;

            // Color based on position and normal with morph influence
            float colorFactor = (normal.X * 0.5f + 0.5f) * 0.7f +
                               (normal.Y * 0.5f + 0.5f) * 0.2f +
                               (normal.Z * 0.5f + 0.5f) * 0.1f;

            // Add color variation based on morph amount
            float morphHue = morphAmount * 0.5f;

            float brightness = amb + diff * 0.7f;
            color = new NumericsVector4(
                colorFactor * brightness + morphHue * 0.3f,
                (1.0f - colorFactor) * brightness,
                XMath.Sin(hitPos.Z * 2.0f + morphAmount * 3.14159f) * 0.5f + 0.5f * brightness,
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
