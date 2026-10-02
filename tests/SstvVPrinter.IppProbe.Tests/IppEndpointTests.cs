using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using SstvVPrinter.IppProbe.Capture;
using SstvVPrinter.IppProbe.Hosting;
using SstvVPrinter.IppProbe.Ipp;

namespace SstvVPrinter.IppProbe.Tests;

public sealed class IppEndpointTests
{
    [Fact]
    public async Task Post_get_printer_attributes_returns_application_ipp()
    {
        await using var server = await ProbeServer.StartAsync();
        using HttpResponseMessage response = await server.PostAsync(Request(IppOperations.GetPrinterAttributes));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/ipp", response.Content.Headers.ContentType!.MediaType);
        IppMessage message = await IppMessageReader.ReadAsync(await response.Content.ReadAsStreamAsync(), CancellationToken.None);
        Assert.Equal(IppStatusCodes.SuccessfulOk, message.Code);
        Assert.NotNull(message.FindAttribute("printer-name"));
    }

    [Fact]
    public async Task Post_print_job_captures_document_and_returns_job_id()
    {
        await using var server = await ProbeServer.StartAsync();
        using HttpResponseMessage response = await server.PostAsync(Request(IppOperations.PrintJob, [1, 2, 3, 4]));
        IppMessage message = await IppMessageReader.ReadAsync(await response.Content.ReadAsStreamAsync(), CancellationToken.None);
        Assert.Equal(IppStatusCodes.SuccessfulOk, message.Code);
        Assert.True(message.FindAttribute("job-id")!.Values.Single().AsInt32() > 0);
        Assert.Single(Directory.GetFiles(server.CaptureDirectory, "*.json"));
    }

    [Fact]
    public async Task Post_rejects_non_ipp_content_type()
    {
        await using var server = await ProbeServer.StartAsync();
        using var content = new ByteArrayContent([]); content.Headers.ContentType = new("text/plain");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await server.Client.PostAsync("ipp/print", content)).StatusCode);
    }

    [Fact]
    public async Task Post_rejects_content_type_with_ipp_prefix_only()
    {
        await using var server = await ProbeServer.StartAsync();
        using var content = new ByteArrayContent([]); content.Headers.ContentType = new("application/ipp-malformed");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await server.Client.PostAsync("ipp/print", content)).StatusCode);
    }

    [Fact]
    public async Task Post_malformed_ipp_after_readable_header_returns_ipp_bad_request()
    {
        await using var server = await ProbeServer.StartAsync();
        byte[] bytes = [2, 0, 0, 2, 0, 0, 0, 42, 0x7f];
        using var content = new ByteArrayContent(bytes); content.Headers.ContentType = new("application/ipp");

        using HttpResponseMessage response = await server.PostAsync(content);
        IppMessage message = await IppMessageReader.ReadAsync(await response.Content.ReadAsStreamAsync(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/ipp", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(IppStatusCodes.ClientErrorBadRequest, message.Code);
        Assert.Equal(42, message.RequestId);
    }

    [Fact]
    public async Task Post_over_64MiB_returns_413_without_capture()
    {
        await using var server = await ProbeServer.StartAsync();
        using var content = new DeclaredLengthContent(CaptureOptions.MaximumDocumentBytes + 1024 * 1024 + 1);
        content.Headers.ContentType = new("application/ipp");
        using var request = new HttpRequestMessage(HttpMethod.Post, "ipp/print") { Content = content };
        request.Headers.ExpectContinue = true;
        using HttpResponseMessage response = await server.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Empty(Directory.GetFiles(server.CaptureDirectory));
    }

    [Fact]
    public async Task Post_client_cancellation_leaves_no_capture()
    {
        await using var server = await ProbeServer.StartAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => server.Client.PostAsync("ipp/print", new SlowIppContent(Request(IppOperations.PrintJob)), cancellation.Token));
        await Task.Delay(100);
        Assert.Empty(Directory.GetFiles(server.CaptureDirectory, "*.json"));
    }

    [Fact]
    public async Task Post_two_concurrent_jobs_preserve_both_payloads()
    {
        await using var server = await ProbeServer.StartAsync();
        HttpResponseMessage[] responses = await Task.WhenAll(server.PostAsync(Request(IppOperations.PrintJob, [1])), server.PostAsync(Request(IppOperations.PrintJob, [2])));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(2, Directory.GetFiles(server.CaptureDirectory, "*.json").Length);
        foreach (HttpResponseMessage response in responses) response.Dispose();
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("192.0.2.1")]
    public void LoopbackBinding_rejects_wildcard_and_non_loopback_addresses(string address) =>
        Assert.Throws<ArgumentException>(() => LoopbackBinding.Validate(IPAddress.Parse(address)));

    private static ByteArrayContent Request(ushort operation, byte[]? document = null)
    {
        byte[] bytes = IppMessageReaderTests.BuildPrintJobRequest(document ?? []);
        BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(2, 2), operation);
        var content = new ByteArrayContent(bytes); content.Headers.ContentType = new("application/ipp"); return content;
    }

    private sealed class DeclaredLengthContent(long length) : HttpContent
    {
        protected override bool TryComputeLength(out long computed) { computed = length; return true; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;
    }

    private sealed class SlowIppContent(ByteArrayContent prefix) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = -1; return false; }
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await prefix.CopyToAsync(stream); byte[] block = new byte[8192];
            while (true) { await stream.WriteAsync(block); await stream.FlushAsync(); await Task.Delay(20); }
        }
    }
}

internal sealed class ProbeServer : IAsyncDisposable
{
    private readonly WebApplication app;
    private ProbeServer(WebApplication app, HttpClient client, string captureDirectory) { this.app = app; Client = client; CaptureDirectory = captureDirectory; }
    public HttpClient Client { get; }
    public string CaptureDirectory { get; }
    public static async Task<ProbeServer> StartAsync() { string directory = Path.Combine(Path.GetTempPath(), $"sstv-http-{Guid.NewGuid():N}"); Directory.CreateDirectory(directory); WebApplication app = ProbeApplication.Build(directory, 0); await app.StartAsync(); string address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First(v => v.Contains("127.0.0.1", StringComparison.Ordinal)); return new(app, new HttpClient { BaseAddress = new Uri(address) }, directory); }
    public Task<HttpResponseMessage> PostAsync(HttpContent content) => Client.PostAsync("ipp/print", content);
    public async ValueTask DisposeAsync() { Client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); if (Directory.Exists(CaptureDirectory)) Directory.Delete(CaptureDirectory, true); }
}
