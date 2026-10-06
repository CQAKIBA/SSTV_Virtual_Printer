using System.Buffers.Binary;
using System.Text;

namespace SstvVPrinter.IppProbe.Ipp;

public static class IppMessageWriter
{
    public static async ValueTask WriteAsync(Stream output, IppMessage message, CancellationToken cancellationToken)
    {
        byte[] header = new byte[8];
        header[0] = message.Version.Major;
        header[1] = message.Version.Minor;
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(2, 2), message.Code);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), message.RequestId);
        await output.WriteAsync(header, cancellationToken);

        foreach (IppAttributeGroup group in message.Groups)
        {
            await output.WriteAsync(new byte[] { group.Tag }, cancellationToken);
            foreach (IppAttribute attribute in group.Attributes)
            {
                byte[] name = Encoding.UTF8.GetBytes(attribute.Name);
                for (int index = 0; index < attribute.Values.Count; index++)
                {
                    IppValue value = attribute.Values[index];
                    await output.WriteAsync(new byte[] { value.Tag }, cancellationToken);
                    await WriteLengthAsync(output, index == 0 ? name.Length : 0, cancellationToken);
                    if (index == 0) await output.WriteAsync(name, cancellationToken);
                    await WriteLengthAsync(output, value.Data.Length, cancellationToken);
                    await output.WriteAsync(value.Data, cancellationToken);
                }
            }
        }

        await output.WriteAsync(new byte[] { IppDelimiterTags.EndOfAttributes }, cancellationToken);
    }

    private static async ValueTask WriteLengthAsync(Stream output, int length, CancellationToken cancellationToken)
    {
        if ((uint)length > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(length));
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)length);
        await output.WriteAsync(bytes, cancellationToken);
    }
}
