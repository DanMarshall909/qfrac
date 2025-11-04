using System;
using System.IO;
using QFrac.Compute;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace qfrac.Tests;

public class FractalImageTests
{
    private static readonly string ArtifactsPath = Path.Combine(AppContext.BaseDirectory, "artifacts");

    [Fact]
    public void GeneratesGradientPreviewImage()
    {
        Directory.CreateDirectory(ArtifactsPath);

        using var fractal = new IlgpuFractal();
        var rgba = fractal.GenerateRgbaImage(64, 64, 0f);

        using var image = new Image<Rgba32>(fractal.Width, fractal.Height);
        for (int y = 0; y < fractal.Height; y++)
        {
            for (int x = 0; x < fractal.Width; x++)
            {
                int idx = ((y * fractal.Width) + x) * 4;
                image[x, y] = new Rgba32(rgba[idx + 0], rgba[idx + 1], rgba[idx + 2], rgba[idx + 3]);
            }
        }

        string outputPath = Path.Combine(ArtifactsPath, "fractal_preview.png");
        image.Save(outputPath);

        Assert.True(File.Exists(outputPath), "Preview image should be written to disk.");
        Assert.True(new FileInfo(outputPath).Length > 0, "Preview image should have non-zero length.");
    }
}
