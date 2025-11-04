using System;
using ILGPU;
using ILGPU.Runtime;
using NumericsVector4 = System.Numerics.Vector4;

namespace QFrac.Compute;

/// <summary>
/// Manages ILGPU context and simple quaternion fractal kernel.
/// Currently emits placeholder data; intended to be extended with CUDA-backed logic.
/// </summary>
public sealed class IlgpuFractal : IDisposable
{
    private readonly Context _context;
    private readonly Accelerator _accelerator;
    private readonly Action<Index1D, ArrayView<NumericsVector4>> _kernel;

    public IlgpuFractal()
    {
        _context = Context.CreateDefault();
        _accelerator = CreatePreferredAccelerator(_context);
        _kernel = _accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<NumericsVector4>>(SampleKernel);
    }

    public int Width { get; private set; }
    public int Height { get; private set; }

    public NumericsVector4[] Generate(int width, int height, float time)
    {
        Width = width;
        Height = height;

        int total = width * height;
        using var buffer = _accelerator.Allocate1D<NumericsVector4>(total);

        _kernel(total, buffer.View);
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

    private static void SampleKernel(Index1D index, ArrayView<NumericsVector4> output)
    {
        // Placeholder gradient until real fractal kernel lands.
        float t = index / (float)Math.Max(1, output.Length - 1);
        output[index] = new NumericsVector4(t, 1f - t, 0.5f, 1f);
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
