# SSTV encoder validation results

Automated timing, waveform, container, and deterministic-output tests are recorded by the test suite. On 2026-10-03, the user decoded all eight fixtures with the smartphone app reported as **SSTV Decorder**: VIS mode recognition succeeded and every color-bar image displayed normally. The app version and displayed decoded dimensions were not recorded.

## Generated fixture evidence — 2026-10-02

All eight Release fixtures were generated successfully at 48 kHz mono/16-bit PCM. The 1× and 2× variants of every mode have identical sample and byte counts.

| Fixture | Samples | Bytes | SHA-256 |
|---|---:|---:|---|
| martin1-1x.wav | 5,529,608 | 11,059,260 | `302FD62D2CB7C2EF455ACDE756A11A7025328107016711CDBD51BC2749509646` |
| martin1-2x.wav | 5,529,608 | 11,059,260 | `A20949654ADA3EED586160F7A23FE9F73A3B5ADC42A39BC47F7E9149EBBE5B2B` |
| scottie1-1x.wav | 5,306,079 | 10,612,202 | `9DBECD6362B8EF0F1FF2DB0C0E4B9963EDD308AC01995E59E9255A4B37A7201E` |
| scottie1-2x.wav | 5,306,079 | 10,612,202 | `B5D7FD73CBAB55C46C402D0377F954AB766C7FFB310F45226122C82095B5BF60` |
| scottie2-1x.wav | 3,456,391 | 6,912,826 | `96D4D4EEA2A1643FE96006DED3559DD232639657C7C2900B6DCE7859387E171A` |
| scottie2-2x.wav | 3,456,391 | 6,912,826 | `40B45E3473743BC5F492DEAED81B1BFEF495593D48BB760B12F97776AA19E3BB` |
| robot36-1x.wav | 1,771,680 | 3,543,404 | `1E9D6B169062150A8D8B9442733E2EA8C93C13F36C282789FEC94F066CA2D690` |
| robot36-2x.wav | 1,771,680 | 3,543,404 | `89C1736D3558911C9563A1BD5BAB0F70471C38A8C5186557B9814D87C5DE2216` |

| Mode | Horizontal | Expected duration | Receiver/version | Detected mode | Decoded size | Colors/channels | Sync/slant | Result/notes |
|---|---:|---:|---|---|---|---|---|---|
| Martin 1 | 1× | 115.200176 s | SSTV Decorder / version not recorded | Martin 1 | Not recorded | Normal | Normal | Pass |
| Scottie 1 | 1× | 110.543320 s | SSTV Decorder / version not recorded | Scottie 1 | Not recorded | Normal | Normal | Pass |
| Scottie 2 | 1× | 72.008152 s | SSTV Decorder / version not recorded | Scottie 2 | Not recorded | Normal | Normal | Pass |
| Robot 36 | 1× | 36.910000 s | SSTV Decorder / version not recorded | Robot 36 | Not recorded | Normal | Normal | Pass |
| Martin 1 | 2× | 115.200176 s | SSTV Decorder / version not recorded | Martin 1 | Not recorded | Normal | Normal | Pass with one receiver; still experimental |
| Scottie 1 | 2× | 110.543320 s | SSTV Decorder / version not recorded | Scottie 1 | Not recorded | Normal | Normal | Pass with one receiver; still experimental |
| Scottie 2 | 2× | 72.008152 s | SSTV Decorder / version not recorded | Scottie 2 | Not recorded | Normal | Normal | Pass with one receiver; still experimental |
| Robot 36 | 2× | 36.910000 s | SSTV Decorder / version not recorded | Robot 36 | Not recorded | Normal | Normal | Pass with one receiver; still experimental |
