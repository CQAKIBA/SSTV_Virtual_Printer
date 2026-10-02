using System.Buffers.Binary;
using System.Text;
namespace SstvVPrinter.IppProbe.Ipp;

public static class IppMessageReader
{
    const int Limit = 1024 * 1024;
    public static async ValueTask<IppMessage> ReadAsync(Stream s, CancellationToken ct) { byte[] h = new byte[8]; await Exact(s, h, ct); var groups = new List<IppAttributeGroup>(); int used = 8; string? previous = null; byte tag = await Byte(s, ct); while (tag != 3) { if (!IppDelimiterTags.IsGroup(tag)) throw Bad($"Unknown delimiter 0x{tag:X2}"); var attrs = new List<IppAttribute>(); var indexes = new Dictionary<string, int>(); byte next = await Byte(s, ct); while (!IppDelimiterTags.IsGroup(next) && next != 3) { ushort nl = await U16(s, ct); Check(used = checked(used + 3 + nl)); string name; if (nl == 0) name = previous ?? throw Bad("Repeated value without name"); else { byte[] nb = new byte[nl]; await Exact(s, nb, ct); name = Encoding.UTF8.GetString(nb); previous = name; } ushort vl = await U16(s, ct); Check(used = checked(used + 2 + vl)); byte[] vb = new byte[vl]; await Exact(s, vb, ct); var value = new IppValue(next, vb); if (nl == 0) { if (!indexes.TryGetValue(name, out int i)) throw Bad("Repeated name outside group"); attrs[i] = new(name, attrs[i].Values.Append(value).ToArray()); } else { indexes[name] = attrs.Count; attrs.Add(new(name, [value])); } next = await Byte(s, ct); } groups.Add(new(tag, attrs)); tag = next; } return new(new(h[0], h[1]), BinaryPrimitives.ReadUInt16BigEndian(h.AsSpan(2, 2)), BinaryPrimitives.ReadInt32BigEndian(h.AsSpan(4, 4)), groups); }
    static void Check(int n) { if (n > Limit) throw Bad("Attributes exceed 1 MiB"); }
    static async ValueTask<byte> Byte(Stream s, CancellationToken c) { byte[] b = new byte[1]; await Exact(s, b, c); return b[0]; }
    static async ValueTask<ushort> U16(Stream s, CancellationToken c) { byte[] b = new byte[2]; await Exact(s, b, c); return BinaryPrimitives.ReadUInt16BigEndian(b); }
    static async ValueTask Exact(Stream s, Memory<byte> b, CancellationToken c) { try { await s.ReadExactlyAsync(b, c); } catch (EndOfStreamException e) { throw Bad("Unexpected end", e); } }
    static IppProtocolException Bad(string m, Exception? e = null) => new(m, IppStatusCodes.ClientErrorBadRequest, e);
}
