namespace SstvVPrinter.Sstv.Signal;

public readonly record struct PcmBlock(long StartSample, ReadOnlyMemory<float> Samples);
