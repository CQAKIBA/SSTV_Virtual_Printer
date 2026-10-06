# Security Policy

SSTV Virtual Printer accepts print data and produces audio, so unexpected input handling and unsafe network exposure deserve careful reporting.

## Supported version

Only the latest published beta receives security fixes. This is experimental software and is not hardened as a network print server.

## Reporting a vulnerability

Use GitHub's **Private vulnerability reporting** feature for this repository when it is available. If it is not enabled, open a public issue containing only a minimal, non-sensitive request for a maintainer to establish a private reporting channel.

Do **not** publish exploit details, credentials, private keys, station details, personal documents, captured print jobs, or other sensitive evidence in a public issue.

Include the affected version, Windows version, reproduction preconditions, expected impact, and whether the issue is reachable through the default loopback-only configuration.

## Deployment boundary

The supported IPP listener is bound to the local loopback interface. Exposing it to a LAN or untrusted network is outside the supported threat model. PTT is not implemented, but VOX or external radio control can turn any unexpected audio into an on-air transmission; operators must maintain independent transmission control.
