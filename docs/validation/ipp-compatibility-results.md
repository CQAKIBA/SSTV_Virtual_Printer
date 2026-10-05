# IPP Compatibility Results

## Decision gate

- **Go:** A supported Windows 10 build and Windows 11 24H2 both produce valid, inspectable PWG Raster from Word, Paint, and one browser, with portrait and landscape preserved.
- **Conditional go:** Printing is reliable but Windows selects another documented standard format. Revise the decoder scope before product implementation.
- **No-go:** The queue cannot be installed reliably, jobs require LAN exposure, or no stable decodable format is delivered. Return to architecture design before considering a V3/V4 driver.

Current decision: **Current Windows 11 25H2 path verified; formal gate still pending**. The specified Windows 11 24H2 matrix and a supported Windows 10 build have not yet been exercised.

The loopback architecture and Microsoft IPP Class Driver path are proven on the current Windows 11 machine. The full gate still requires application/orientation coverage on Windows 11 plus a supported Windows 10 machine.

| OS edition/build | Application/version | Orientation | Queue driver | Advertised format | Received MIME | Payload signature | First-page properties | Result | Notes |
|---|---|---|---|---|---|---|---|---|---|
| Not run | Word | Portrait | — | `image/pwg-raster` | — | — | — | Pending | |
| Not run | Paint | Portrait | — | `image/pwg-raster` | — | — | — | Pending | |
| Not run | Browser | Portrait | — | `image/pwg-raster` | — | — | — | Pending | |
| Not run | Word | Landscape | — | `image/pwg-raster` | — | — | — | Pending | |
| Not run | Paint | Landscape | — | `image/pwg-raster` | — | — | — | Pending | |
| Not run | Browser | Landscape | — | `image/pwg-raster` | — | — | — | Pending | |
| Windows 11 Home 25H2, build 26200.9457 | PowerShell 7.6.5 `Out-Printer` | Portrait | Microsoft IPP Class Driver | `image/pwg-raster` | `image/pwg-raster` | `52 61 53 32` (`RaS2`) | 2480×3507, 300×300 dpi; 10,211 bytes | Pass | Loopback queue installed with `Add-Printer -IppURL`; Validate-Job, Print-Job, and Get-Job-Attributes succeeded; queue drained to zero. SHA-256 `CEB215F08DE57B8C20CAE34C374BDD01E87FFF4FC9AE0ACEB333DDCFE1B84AFE`. |
| Windows 11 Home 25H2, build 26200.9457 | Windows Explorer 26200.9457, context menu **Print** (Print Pictures wizard) | Portrait | Microsoft IPP Class Driver | `image/pwg-raster` | `image/pwg-raster` | `52 61 53 32` (`RaS2`) | 2480×3507, 300×300 dpi; 18,321,676 bytes | Pass | Job name `フル ページ写真`; full image payload captured, Get-Job-Attributes succeeded, and queue drained to zero. SHA-256 `6580A9BB60980984C502D51F5F3E5E7E57868F8031CA262BFB5D0C388DC11470`. |
| Windows 11 Home 25H2, build 26200.9457 | Microsoft Paint 11.2605.81.0 | Portrait | Microsoft IPP Class Driver | `image/pwg-raster` | `image/pwg-raster` | `52 61 53 32` (`RaS2`) | 2480×3507, 300×300 dpi; 287,710 bytes | Pass | Job name `model(1).ofd.png`; queue drained to zero. SHA-256 `147B6BF08FA29EE4030BCD76727274EB1E0F3F292CFD547AF6B9FF843B84A564`. |
| Windows 11 Home 25H2, build 26200.9457 | Adobe Acrobat/Reader DC 26.2.21931.0 | Portrait | Microsoft IPP Class Driver | `image/pwg-raster` | `image/pwg-raster` | `52 61 53 32` (`RaS2`) | 2480×3507, 300×300 dpi; 12,919,265 bytes | Pass | PDF job captured and queue drained to zero. SHA-256 `27E143BCD2B18ED35DAE34A37A3292268779A3A0F934EA12E3B71F5A15455520`. |
| Windows 11 Home 25H2, build 26200.9457 | Microsoft Excel 16.0.19127.20800 | Portrait | Microsoft IPP Class Driver | `image/pwg-raster` | `image/pwg-raster` | `52 61 53 32` (`RaS2`) | 2480×3507, 300×300 dpi; 641,624 bytes | Pass | Job name `数式のチュートリアル1`; queue drained to zero. SHA-256 `9884ACE3549B41947D3DA57505D4D2A12612D76948A12BFD7105861E8CA1AAD2`. |
| Windows 11 Home 25H2, build 26200.9457 | Adobe Acrobat/Reader DC 26.2.21931.0 | Landscape | Microsoft IPP Class Driver | `image/pwg-raster` | `image/pwg-raster` | `52 61 53 32` (`RaS2`) | 2480×3507 physical raster, Orientation=1 (90° landscape), A4 595×841 pt, 300×300 dpi; 7,640,465 bytes | Pass | Acrobat and printer properties were both set to landscape. PWG Raster correctly retained the portrait-feed physical buffer and encoded landscape as Orientation=1; queue drained to zero. SHA-256 `F944C016FDE4BACDFEAE50CF282ABD18605B8C1EC96AB5E287A55A4F54AD8467`. |

Add rows for each OS build. Completed rows include exact Windows build, application version, exact `Get-Printer` driver name, received MIME type, first four payload bytes, dimensions, resolution, and pass/fail reason.
