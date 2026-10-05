using System.Runtime.CompilerServices;
using SstvVPrinter.Sstv.Signal;
using SstvVPrinter.Sstv.Wave;

namespace SstvVPrinter.Sstv.Tests;

public sealed class WaveFileWriterTests
{
    [Fact]
    public async Task WriteAsync_writes_riff_pcm_mono_48000_16bit()
    {
        var bytes = await Write([new PcmBlock(0, new float[] { 0, 0.5f })]);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.Equal("WAVEfmt ", System.Text.Encoding.ASCII.GetString(bytes, 8, 8));
        Assert.Equal((short)1, BitConverter.ToInt16(bytes, 20));
        Assert.Equal((short)1, BitConverter.ToInt16(bytes, 22));
        Assert.Equal(48_000, BitConverter.ToInt32(bytes, 24));
        Assert.Equal((short)16, BitConverter.ToInt16(bytes, 34));
    }

    [Fact]
    public async Task WriteAsync_sets_riff_and_data_lengths()
    {
        var bytes = await Write([new PcmBlock(0, new float[] { 0, 0.5f, -0.5f })]);
        Assert.Equal(bytes.Length - 8, BitConverter.ToInt32(bytes, 4));
        Assert.Equal(6, BitConverter.ToInt32(bytes, 40));
    }

    [Fact]
    public async Task WriteAsync_clamps_float_roundoff_before_pcm16_conversion()
    {
        var bytes = await Write([new PcmBlock(0, new float[] { 1.001f, -1.001f })]);
        Assert.Equal(short.MaxValue, BitConverter.ToInt16(bytes, 44));
        Assert.Equal(short.MinValue, BitConverter.ToInt16(bytes, 46));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public async Task WriteAsync_rejects_nonfinite_pcm_without_publication(float value)
    {
        var path = TempPath();
        await Assert.ThrowsAsync<InvalidDataException>(() => WaveFileWriter.WriteAsync(path, Blocks(new PcmBlock(0, new[] { value }))));
        Assert.False(File.Exists(path));
        Assert.Empty(TemporarySiblings(path));
    }

    [Fact]
    public async Task WriteAsync_publishes_atomically()
    {
        var path = TempPath();
        await WaveFileWriter.WriteAsync(path, Blocks(new PcmBlock(0, new float[] { 0 })));
        Assert.True(File.Exists(path));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(path)!, "." + Path.GetFileName(path) + ".*.tmp"));
        File.Delete(path);
    }

    [Fact]
    public async Task WriteAsync_cancellation_and_failure_leave_destination_untouched()
    {
        var path = TempPath();
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WaveFileWriter.WriteAsync(path, CancelAfterFirst(cancellation), cancellation.Token));
        Assert.False(File.Exists(path));
        Assert.Empty(TemporarySiblings(path));

        await Assert.ThrowsAsync<InvalidOperationException>(() => WaveFileWriter.WriteAsync(path, FailAfterFirst()));
        Assert.False(File.Exists(path));
        Assert.Empty(TemporarySiblings(path));
    }

    [Fact]
    public async Task WriteAsync_publication_collision_preserves_competing_destination()
    {
        var path = TempPath();
        await Assert.ThrowsAsync<IOException>(() => WaveFileWriter.WriteAsync(path, CollideAfterFirst(path)));
        Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(path));
        Assert.Empty(TemporarySiblings(path));
        File.Delete(path);
    }

    [Fact]
    public async Task WriteAsync_rejects_noncontiguous_or_reordered_pcm_blocks()
    {
        var path = TempPath();
        await Assert.ThrowsAsync<InvalidDataException>(() => WaveFileWriter.WriteAsync(path, Blocks(
            new PcmBlock(0, new float[] { 0 }), new PcmBlock(2, new float[] { 0 }))));
        Assert.False(File.Exists(path));
    }

    private static async Task<byte[]> Write(PcmBlock[] blocks)
    {
        var path = TempPath();
        await WaveFileWriter.WriteAsync(path, Blocks(blocks));
        var bytes = await File.ReadAllBytesAsync(path);
        File.Delete(path);
        return bytes;
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"sstv-{Guid.NewGuid():N}.wav");

    private static string[] TemporarySiblings(string path) => Directory.GetFiles(Path.GetDirectoryName(path)!, "." + Path.GetFileName(path) + ".*.tmp");

    private static async IAsyncEnumerable<PcmBlock> CancelAfterFirst(CancellationTokenSource cancellation)
    {
        yield return new PcmBlock(0, new float[] { 0 });
        cancellation.Cancel();
        await Task.Yield();
        cancellation.Token.ThrowIfCancellationRequested();
    }

    private static async IAsyncEnumerable<PcmBlock> FailAfterFirst()
    {
        yield return new PcmBlock(0, new float[] { 0 });
        await Task.Yield();
        throw new InvalidOperationException("Injected stream failure.");
    }

    private static async IAsyncEnumerable<PcmBlock> CollideAfterFirst(string path)
    {
        yield return new PcmBlock(0, new float[] { 0 });
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        yield return new PcmBlock(1, new float[] { 0 });
    }

    private static async IAsyncEnumerable<PcmBlock> Blocks(PcmBlock first, params PcmBlock[] rest)
    {
        yield return first;
        foreach (var block in rest) yield return block;
        await Task.CompletedTask;
    }

    private static IAsyncEnumerable<PcmBlock> Blocks(PcmBlock[] blocks) => Blocks(blocks[0], blocks.Skip(1).ToArray());
}
