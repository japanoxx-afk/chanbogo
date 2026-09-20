# Multiplayer timing capture v3

Version 3 samples only right mouse button down/up state while the game is the
foreground application, alongside the counters. No keyboard keys, coordinates,
window titles or other applications' input are recorded. A short click can be
missed between 20ms samples. This identifies approximate input timing, NOT when
a unit begins moving. Match individual, spaced orders to queue transitions with
caution: multiple orders and transient zero-length queues are ambiguous.

Blank interactive test code defaults to mp-test-04; invalid roles are re-prompted.
An explicit Recording message shows when sampling starts. Host.cmd / Client.cmd
preselect the role/code and pause after completion or failure. Enter the multiplayer
match BEFORE running these files. Return to the game after Recording appears.

Version 2 adds current/target command lead and outgoing queued byte count at
about 20ms intervals. Only the byte count is read, NEVER command contents.
The queue is at 0x71BF08 + 4, read by 0x448772 when sending at 0x450C80.
Samples can miss brief queue changes; this is not click-to-movement measurement.
The reports folder is now created before game validation. Failures write a
sanitized capture-error file with a fixed stage name (no exception messages,
paths or network data). A folder creation failure can only be shown on screen.

This is a diagnostic tool, not a latency fix. It reads a verified game build
without attaching a debugger or changing game memory, simulation or networking.
No installation or Python is required to run MultiplayerCapture.exe on Windows.

1. Both players enter the same LAN match using the same launcher/game settings.
2. Run MultiplayerCapture.exe. Enter `host` on the host PC, `client` on the peer PC.
3. Both enter the agreed non-personal test code, for example `mp-test-01`.
4. For 60 seconds, issue a right-click movement order about every three seconds.
   Start at roughly the same time. Only right-button state is sampled; it is not
   a record of actual engine command execution.
5. The tool creates one CSV under `reports` beside the tool. Send only that CSV.
   Do not send the launcher's original Diagnostics folders or crash.dmp publicly.

The CSV contains a schema number, test code, role, elapsed milliseconds and
numeric engine counters. It does NOT contain names, IP/MAC addresses, machine
names, file paths, wall-clock timestamps, packets, memory dumps or chat contents.
Do not enter a name, address or other personal information as the test code.
Filename suffixes distinguish captures; roles are declared by the user, not inferred.

There is NO automatic Git upload and no need to give this tool GitHub credentials.
The assistant validates the allowlisted fields before committing selected CSVs
to `diagnostics-public/<test-code>/host` or `client` with user authorization.
Never use `git add .` on diagnostic folders.

Counter differences are not measured command latency or ping. Sampling is about
20ms, individual reads are not atomic, and host/client clocks are not synchronized.
Compare common counter ranges, not just the elapsed timestamps on different PCs.
If the game exits or reading fails, a partial report may remain; it is not a
completed capture. Normal completed captures print `Saved`.

Developer build: .NET Framework csc.exe /platform:x86 /out:MultiplayerCapture.exe MultiplayerCapture.cs
Validator: python tools/validate_multiplayer_csv.py REPORT.csv TEST-CODE host-or-client
