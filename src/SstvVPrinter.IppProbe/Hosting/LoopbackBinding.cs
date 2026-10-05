using System.Net;
namespace SstvVPrinter.IppProbe.Hosting;

public static class LoopbackBinding { public static void Validate(IPAddress address) { if (!IPAddress.IsLoopback(address)) throw new ArgumentException("The IPP probe may bind only to loopback addresses.", nameof(address)); } }
