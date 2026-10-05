# PWG Page Composition Validation Results

Validation date: 2026-10-03 JST

| Private capture | SHA-256 | Mode | Physical → logical | Orientation | Content bounds | Header | Result |
|---|---|---|---|---:|---|---|---|
| `captures/4.pwg` | `147B6BF08FA29EE4030BCD76727274EB1E0F3F292CFD547AF6B9FF843B84A564` | Martin 1, 1× | 2480×3507 → 2480×3507 | 0 | `(75,16) 170×240` | Dedicated 16 rows | PASS: upright portrait, complete page and header, aspect preserved; thin magenta/gray diagram lines remain visible after reduction. |
| `captures/7.pwg` | `F944C016FDE4BACDFEAE50CF282ABD18605B8C1EC96AB5E287A55A4F54AD8467` | Robot 36, 2× | 2480×3507 → 3507×2480 | 1 | `(150,0) 339×240` | 50% white overlay | PASS: counter-clockwise normalization produces upright landscape content, no crop, complete right-aligned callsign, and independently rendered 640-pixel header/page detail. |

The PPM and temporary PNG inspection copies were stored only under the ignored `.superpowers/` workspace and are not committed. Both previews used the print-job acceptance timestamp from their capture metadata, converted to fixed JST.
