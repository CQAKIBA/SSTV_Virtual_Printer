using System.Buffers.Binary;
using System.Text;
namespace SstvVPrinter.IppProbe.Ipp;

public sealed class IppValue(byte tag, ReadOnlySpan<byte> data) : IEquatable<IppValue>
{
    public byte Tag { get; } = tag; public byte[] Data { get; } = data.ToArray();
    public static IppValue FromString(byte tag, string value) => new(tag, Encoding.UTF8.GetBytes(value));
    public static IppValue FromInt32(byte tag, int value) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(b, value); return new(tag, b); }
    public static IppValue FromBoolean(bool value) => new(IppValueTags.Boolean, [(byte)(value ? 1 : 0)]);
    public string AsString() => Encoding.UTF8.GetString(Data); public int AsInt32() => Data.Length == 4 ? BinaryPrimitives.ReadInt32BigEndian(Data) : throw new InvalidOperationException();
    public bool Equals(IppValue? other) => other is not null && Tag == other.Tag && Data.AsSpan().SequenceEqual(other.Data); public override bool Equals(object? obj) => Equals(obj as IppValue);
    public override int GetHashCode() { var h = new HashCode(); h.Add(Tag); foreach (byte b in Data) h.Add(b); return h.ToHashCode(); }
}
