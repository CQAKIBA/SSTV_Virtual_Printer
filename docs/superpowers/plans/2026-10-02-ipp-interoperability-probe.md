# IPP Interoperability Probe Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prove whether the Windows Microsoft IPP Class Driver can send a usable PWG Raster print job to a loopback-only SSTV software printer on supported Windows 10 and Windows 11 versions.

**Architecture:** A small .NET console host exposes one IPP endpoint over HTTP on loopback, answers the minimum discovery/capability operations required by Windows, and captures accepted print-job payloads without decoding them. A separate inspector identifies the received format and basic PWG Raster structure; a reproducible runbook records Windows/application results and makes the go/no-go decision for the product architecture.

**Tech Stack:** C# 14, .NET 10 LTS, ASP.NET Core Kestrel (HTTP/1.1), `System.Text.Json`, xUnit, PowerShell for Windows inspection only; no third-party runtime packages.

**Spec:** `docs/superpowers/specs/2026-10-02-sstv-virtual-printer-design.md`

## Global Constraints

- Target Windows 10 version 21H2 or later and Windows 11; the probe must record exact edition and build for every result.
- Bind only to `127.0.0.1` and `[::1]`; never bind to wildcard or LAN interfaces.
- Advertise `image/pwg-raster` as the sole supported document format for the first experiment.
- Accept only explicit operator-initiated test prints; the probe never emits audio.
- Treat IPP attributes and document bytes as untrusted and cap each request at 64 MiB.
- Persist captured payloads only under the user-supplied capture directory and never commit them.
- Preserve only the first document supplied for a job; record but do not parse later documents during this probe.
- Use TDD and commit after each task.

## Review Focus

- A request whose IPP header is split across many HTTP reads must parse identically to a contiguous request; Task 1 pins this with a one-byte-at-a-time stream test.
- A declared attribute length larger than the remaining body must fail as malformed rather than allocate or hang; Task 1 pins this with a truncated-value test.
- A body larger than 64 MiB must return HTTP 413 and leave no partial capture; Task 3 pins this with an over-limit integration test.
- A client disconnect during capture must remove the temporary file and must not publish metadata; Task 3 pins this with a cancellation integration test.
- Two simultaneous print jobs must receive distinct job identifiers and intact payloads; Task 3 pins this with a concurrent-request integration test.

---

## Planned File Structure

```text
SstvVPrinter.sln
Directory.Build.props
.gitignore
src/
  SstvVPrinter.IppProbe/
    SstvVPrinter.IppProbe.csproj
    Program.cs                         # CLI, configuration, and host lifetime only
    Ipp/
      IppConstants.cs                  # Operation, group, value-tag, and status constants
      IppValue.cs                      # Typed value representation
      IppAttribute.cs                  # Attribute name and one-or-more values
      IppMessage.cs                    # Version, code, request ID, and attribute groups
      IppMessageReader.cs              # Exact, streaming binary IPP parser
      IppMessageWriter.cs              # Binary IPP response serializer
      IppProtocolException.cs          # Bounded protocol error with IPP status mapping
      ProbePrinter.cs                  # Capability and operation responses
    Capture/
      CaptureOptions.cs                # Directory and 64 MiB limit
      PrintJobCapture.cs               # Atomic payload and metadata persistence
      CapturedJob.cs                   # Persisted metadata contract
      PwgRasterInspector.cs            # Signature/header inspection only
      PwgRasterReport.cs               # Inspection result contract
    Hosting/
      IppEndpoint.cs                   # HTTP-to-IPP adapter for POST /ipp/print
      LoopbackBinding.cs               # Rejects non-loopback configuration
tests/
  SstvVPrinter.IppProbe.Tests/
    SstvVPrinter.IppProbe.Tests.csproj
    IppMessageReaderTests.cs
    IppMessageWriterTests.cs
    ProbePrinterTests.cs
    PrintJobCaptureTests.cs
    PwgRasterInspectorTests.cs
    IppEndpointTests.cs
docs/
  validation/
    ipp-probe-runbook.md
    ipp-compatibility-results.md
```

## Task 1: Streaming IPP Binary Codec

**Files:**
- Create: `SstvVPrinter.sln`
- Create: `Directory.Build.props`
- Create: `.gitignore`
- Create: `src/SstvVPrinter.IppProbe/SstvVPrinter.IppProbe.csproj`
- Create: `src/SstvVPrinter.IppProbe/Ipp/IppConstants.cs`
- Create: `src/SstvVPrinter.IppProbe/Ipp/IppValue.cs`
- Create: `src/SstvVPrinter.IppProbe/Ipp/IppAttribute.cs`
- Create: `src/SstvVPrinter.IppProbe/Ipp/IppMessage.cs`
- Create: `src/SstvVPrinter.IppProbe/Ipp/IppMessageReader.cs`
- Create: `src/SstvVPrinter.IppProbe/Ipp/IppMessageWriter.cs`
- Create: `src/SstvVPrinter.IppProbe/Ipp/IppProtocolException.cs`
- Create: `tests/SstvVPrinter.IppProbe.Tests/SstvVPrinter.IppProbe.Tests.csproj`
- Create: `tests/SstvVPrinter.IppProbe.Tests/IppMessageReaderTests.cs`
- Create: `tests/SstvVPrinter.IppProbe.Tests/IppMessageWriterTests.cs`

**Interfaces:**
- Consumes: raw IPP bytes as defined by RFC 8010; this task has no project-internal dependency.
- Produces: `IppMessageReader.ReadAsync(Stream input, CancellationToken cancellationToken) -> ValueTask<IppMessage>`, leaving `input` positioned at the first document byte; `IppMessageWriter.WriteAsync(Stream output, IppMessage message, CancellationToken cancellationToken) -> ValueTask`; immutable `IppMessage(Version Version, ushort Code, int RequestId, IReadOnlyList<IppAttributeGroup> Groups)` and typed attribute/value records used by all later tasks.

- [ ] **Step 1: Scaffold the solution and test project**

Create a `net10.0` console project and xUnit test project, enable nullable reference types, implicit usings, warnings as errors, deterministic builds, and add both projects to `SstvVPrinter.sln`. Ignore `bin/`, `obj/`, `.vs/`, `captures/`, and `*.trx`.

- [ ] **Step 2: Write failing parser tests**

Add tests named `ReadAsync_parses_operation_attributes_and_stops_before_document`, `ReadAsync_accepts_one_byte_at_a_time_stream`, `ReadAsync_combines_zero_length_attribute_names_with_previous_name`, `ReadAsync_rejects_unknown_delimiter_tag`, and `ReadAsync_rejects_truncated_value_without_large_allocation`. Use an IPP 2.0 `Print-Job` request with request ID 42, operation attributes `attributes-charset=utf-8`, `attributes-natural-language=en`, `printer-uri=ipp://localhost:8631/ipp/print`, `requesting-user-name=tester`, and `document-format=image/pwg-raster`; append four marker bytes and assert that the marker remains unread by the parser.

- [ ] **Step 3: Run the parser tests and confirm failure**

Run: `dotnet test tests/SstvVPrinter.IppProbe.Tests/SstvVPrinter.IppProbe.Tests.csproj --filter FullyQualifiedName~IppMessageReaderTests`

Expected: FAIL because the IPP model and reader do not exist.

- [ ] **Step 4: Implement the IPP model and streaming reader**

Implement the exact interfaces above. Read network-order integers directly from the supplied stream without a buffering abstraction that can consume document bytes. Enforce a 1 MiB cumulative limit for IPP attributes and reject negative, truncated, or structurally invalid lengths with `IppProtocolException` mapped to `client-error-bad-request` (`0x0400`). Preserve unknown value tags as bounded raw byte values so Windows-specific attributes can be logged without breaking the probe.

- [ ] **Step 5: Run the parser tests and confirm success**

Run: `dotnet test tests/SstvVPrinter.IppProbe.Tests/SstvVPrinter.IppProbe.Tests.csproj --filter FullyQualifiedName~IppMessageReaderTests`

Expected: PASS for all parser tests.

- [ ] **Step 6: Write failing serializer tests**

Add tests named `WriteAsync_emits_network_byte_order_and_end_tag`, `WriteAsync_uses_empty_name_for_repeated_values`, and `WriteAsync_round_trips_supported_value_types`. Assert exact bytes for IPP version 2.0, successful status `0x0000`, request ID 42, operation attributes, and a two-value `ipp-versions-supported` attribute.

- [ ] **Step 7: Implement and verify the serializer**

Implement `IppMessageWriter.WriteAsync`, then run:

`dotnet test tests/SstvVPrinter.IppProbe.Tests/SstvVPrinter.IppProbe.Tests.csproj`

Expected: PASS with no warnings.

- [ ] **Step 8: Commit the codec**

```powershell
git add SstvVPrinter.sln Directory.Build.props .gitignore src/SstvVPrinter.IppProbe tests/SstvVPrinter.IppProbe.Tests
git commit -m "feat: add streaming IPP codec"
```

## Task 2: Minimal IPP Everywhere Capability Surface

**Files:**
- Create: `src/SstvVPrinter.IppProbe/Ipp/ProbePrinter.cs`
- Create: `tests/SstvVPrinter.IppProbe.Tests/ProbePrinterTests.cs`

**Interfaces:**
- Consumes: `IppMessage` and codec constants from Task 1.
- Produces: `ProbePrinter.CreateResponse(IppMessage request) -> IppMessage`; `ProbePrinter.AllocateJob(string userName, string? jobName) -> ProbeJob`; supported operations `Get-Printer-Attributes` (`0x000B`), `Validate-Job` (`0x0004`), and `Print-Job` (`0x0002`).

- [ ] **Step 1: Write failing capability-response tests**

Add `GetPrinterAttributes_returns_windows_required_capabilities`, asserting request ID preservation and these advertised values: IPP 2.0 and 2.1; `image/pwg-raster` only; `operations-supported` containing `Print-Job`, `Validate-Job`, and `Get-Printer-Attributes`; color enabled; 300 DPI; one-sided printing; A4 media; UTF-8; English natural language; no compression; idle printer state; and zero queued jobs. Add `GetPrinterAttributes_honors_requested_attributes` and assert that an omitted attribute is not returned when Windows supplies a requested-attributes list.

- [ ] **Step 2: Write failing validation and rejection tests**

Add tests named `ValidateJob_accepts_pwg_raster`, `ValidateJob_rejects_unsupported_document_format`, `CreateResponse_rejects_unknown_operation`, `CreateResponse_rejects_wrong_printer_uri`, and `CreateResponse_echoes_request_id_on_error`. Unsupported formats return `client-error-document-format-not-supported` (`0x040A`); unknown operations return `server-error-operation-not-supported` (`0x0501`).

- [ ] **Step 3: Run tests and confirm failure**

Run: `dotnet test tests/SstvVPrinter.IppProbe.Tests/SstvVPrinter.IppProbe.Tests.csproj --filter FullyQualifiedName~ProbePrinterTests`

Expected: FAIL because `ProbePrinter` does not exist.

- [ ] **Step 4: Implement the minimal printer state machine**

Implement the interfaces above with canonical printer URI `ipp://localhost:8631/ipp/print` and display name `SSTV Virtual Printer Probe`. Validate required operation attributes before dispatch. Job IDs are positive process-local integers allocated atomically; job URIs use `ipp://localhost:8631/jobs/{id}`.

- [ ] **Step 5: Verify the capability surface**

Run: `dotnet test tests/SstvVPrinter.IppProbe.Tests/SstvVPrinter.IppProbe.Tests.csproj`

Expected: PASS with no warnings.

- [ ] **Step 6: Commit the printer protocol behavior**

```powershell
git add src/SstvVPrinter.IppProbe/Ipp/ProbePrinter.cs tests/SstvVPrinter.IppProbe.Tests/ProbePrinterTests.cs
git commit -m "feat: advertise minimal IPP printer capabilities"
```

## Task 3: Bounded, Atomic Print-Job Capture

**Files:**
- Create: `src/SstvVPrinter.IppProbe/Capture/CaptureOptions.cs`
- Create: `src/SstvVPrinter.IppProbe/Capture/CapturedJob.cs`
- Create: `src/SstvVPrinter.IppProbe/Capture/PrintJobCapture.cs`
- Create: `src/SstvVPrinter.IppProbe/Capture/PwgRasterReport.cs`
- Create: `src/SstvVPrinter.IppProbe/Capture/PwgRasterInspector.cs`
- Create: `tests/SstvVPrinter.IppProbe.Tests/PrintJobCaptureTests.cs`
- Create: `tests/SstvVPrinter.IppProbe.Tests/PwgRasterInspectorTests.cs`

**Interfaces:**
- Consumes: the document-positioned `Stream` returned after Task 1 parsing and `ProbeJob` allocated by Task 2.
- Produces: `PrintJobCapture.CaptureAsync(ProbeJob job, Stream document, string documentFormat, CancellationToken cancellationToken) -> Task<CapturedJob>`; `PwgRasterInspector.InspectAsync(Stream payload, CancellationToken cancellationToken) -> ValueTask<PwgRasterReport>`; atomically published `{jobId}.pwg` and `{jobId}.json` files.

- [ ] **Step 1: Write failing capture tests**

Add `CaptureAsync_writes_payload_and_metadata_atomically`, `CaptureAsync_rejects_payload_over_64MiB_without_partial_files`, `CaptureAsync_cancellation_removes_temporary_files`, and `CaptureAsync_concurrent_jobs_keep_distinct_payloads`. Assert SHA-256, byte length, document format, job ID, received UTC timestamp, and original user/job names in JSON. Use sparse/generated streams rather than allocating 64 MiB test arrays.

- [ ] **Step 2: Run capture tests and confirm failure**

Run: `dotnet test tests/SstvVPrinter.IppProbe.Tests/SstvVPrinter.IppProbe.Tests.csproj --filter FullyQualifiedName~PrintJobCaptureTests`

Expected: FAIL because capture types do not exist.

- [ ] **Step 3: Implement bounded atomic capture**

Implement the interface above. Write payload and JSON to unique `.tmp` files in the destination directory, flush and close them, then rename payload first and metadata last; metadata existence is the publication marker. On error or cancellation, remove only the temporary files created by that call. Stream through an incremental SHA-256 hash and stop as soon as the 64 MiB limit is exceeded.

- [ ] **Step 4: Write failing PWG Raster inspection tests**

Add `InspectAsync_recognizes_big_endian_pwg_sync_word`, `InspectAsync_reports_first_page_dimensions_and_resolution`, `InspectAsync_rejects_non_pwg_payload`, and `InspectAsync_rejects_truncated_page_header`. Use a minimal synthetic PWG Raster header fixture; inspection does not decode pixels.

- [ ] **Step 5: Implement the PWG Raster inspector**

Implement `InspectAsync` to report whether the stream has a valid PWG/CUPS raster sync word and to read the first page's width, height, bits per color, bits per pixel, color space, bytes per line, and X/Y resolution using bounded fixed-size reads. Preserve the input stream position when it is seekable.

- [ ] **Step 6: Run all capture and inspector tests**

Run: `dotnet test tests/SstvVPrinter.IppProbe.Tests/SstvVPrinter.IppProbe.Tests.csproj`

Expected: PASS with no temporary files left by tests.

- [ ] **Step 7: Commit job capture**

```powershell
git add src/SstvVPrinter.IppProbe/Capture tests/SstvVPrinter.IppProbe.Tests/PrintJobCaptureTests.cs tests/SstvVPrinter.IppProbe.Tests/PwgRasterInspectorTests.cs
git commit -m "feat: capture and inspect PWG Raster jobs"
```

## Task 4: Loopback HTTP Host and End-to-End Probe

**Files:**
- Create: `src/SstvVPrinter.IppProbe/Hosting/LoopbackBinding.cs`
- Create: `src/SstvVPrinter.IppProbe/Hosting/IppEndpoint.cs`
- Create: `src/SstvVPrinter.IppProbe/Program.cs`
- Create: `tests/SstvVPrinter.IppProbe.Tests/IppEndpointTests.cs`
- Create: `docs/validation/ipp-probe-runbook.md`
- Create: `docs/validation/ipp-compatibility-results.md`

**Interfaces:**
- Consumes: Task 1 codec, Task 2 `ProbePrinter`, Task 3 capture and inspection.
- Produces: CLI `dotnet run --project src/SstvVPrinter.IppProbe -- --capture-directory <absolute-path> [--port 8631]`; HTTP `POST /ipp/print` with `Content-Type: application/ipp`; console receipt summary containing job ID, byte count, SHA-256, detected format, and first-page raster properties.

- [ ] **Step 1: Write failing endpoint integration tests**

Add `Post_get_printer_attributes_returns_application_ipp`, `Post_print_job_captures_document_and_returns_job_id`, `Post_rejects_non_ipp_content_type`, `Post_over_64MiB_returns_413_without_capture`, `Post_client_cancellation_leaves_no_capture`, `Post_two_concurrent_jobs_preserve_both_payloads`, and `LoopbackBinding_rejects_wildcard_and_non_loopback_addresses`. Exercise the real ASP.NET Core pipeline with an ephemeral loopback port and temporary capture directory.

- [ ] **Step 2: Run endpoint tests and confirm failure**

Run: `dotnet test tests/SstvVPrinter.IppProbe.Tests/SstvVPrinter.IppProbe.Tests.csproj --filter "FullyQualifiedName~IppEndpointTests"`

Expected: FAIL because the endpoint and binding guard do not exist.

- [ ] **Step 3: Implement loopback-only hosting**

Implement `IppEndpoint.Map(IEndpointRouteBuilder endpoints, ProbePrinter printer, PrintJobCapture capture)` and the CLI. Configure Kestrel for HTTP/1.1 on both `127.0.0.1:{port}` and `[::1]:{port}`, reject non-loopback addresses before starting, disable request-body buffering, set the request body limit to 64 MiB plus 1 MiB for IPP attributes, and map malformed IPP to an IPP error response when the request ID is readable. Map an HTTP framing/body-size failure to HTTP 400/413 because no valid IPP response can be constructed.

- [ ] **Step 4: Run automated verification**

Run:

```powershell
dotnet test SstvVPrinter.sln --configuration Release
dotnet build SstvVPrinter.sln --configuration Release
```

Expected: all tests PASS; build succeeds with zero warnings.

- [ ] **Step 5: Write the reproducible Windows runbook**

In `docs/validation/ipp-probe-runbook.md`, document exact commands to start the probe, the endpoint `ipp://localhost:8631/ipp/print`, the Windows Settings path for adding an **IPP Device** by full URL, how to verify the queue uses `Microsoft IPP Class Driver` with `Get-Printer`, how to remove only the probe queue, and how to print one-page color/portrait and landscape fixtures from each source application. Do not claim a PowerShell installation command unless it is demonstrated on every target Windows version.

- [ ] **Step 6: Create the compatibility result template**

In `docs/validation/ipp-compatibility-results.md`, add one row per OS/application combination with fields for exact OS build, application version, queue-driver name, advertised format, received MIME type, payload signature, first-page properties, success/failure, and notes. Define the gate:

- **Go:** both a supported Windows 10 build and Windows 11 24H2 produce valid, inspectable PWG Raster from Word, Paint, and one browser, with landscape and portrait preserved.
- **Conditional go:** printing is reliable but Windows selects another documented standard format; revise the decoder scope before product implementation.
- **No-go:** the queue cannot be installed reliably, jobs require LAN exposure, or no stable decodable format is delivered; return to architecture design before adding a V3/V4 driver.

- [ ] **Step 7: Perform the current-machine manual probe**

Start the host with an absolute temporary capture directory, add the printer through Windows Settings, verify the driver name, print from every locally available target application, inspect the generated JSON reports, and fill the matching rows in `ipp-compatibility-results.md`. Remove the probe printer afterward but retain captures outside Git until the result is reviewed.

- [ ] **Step 8: Commit the completed probe and evidence**

```powershell
git add src/SstvVPrinter.IppProbe/Hosting src/SstvVPrinter.IppProbe/Program.cs tests/SstvVPrinter.IppProbe.Tests/IppEndpointTests.cs docs/validation
git commit -m "feat: complete Windows IPP interoperability probe"
```

## Completion Gate

Do not plan or implement the production printer host until the compatibility matrix reaches **Go** or the design is explicitly revised after a **Conditional go**. A Windows 11-only success does not prove the approved Windows 10 requirement. After the gate passes, write separate implementation plans for:

1. SSTV encoder core and reference-signal verification
2. Page decoding and header composition
3. Windows audio output and transmission control boundary
4. Desktop UI, job store, printer lifecycle, packaging, and end-to-end validation
