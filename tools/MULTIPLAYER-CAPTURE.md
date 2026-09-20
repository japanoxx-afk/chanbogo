# Multiplayer timing capture v1

This is a diagnostic tool, not a latency fix. It reads a verified game build
without attaching a debugger or changing game memory, simulation or networking.
No installation or Python is required to run MultiplayerCapture.exe on Windows.

1. Both players enter the same LAN match using the same launcher/game settings.
2. Run MultiplayerCapture.exe. Enter `host` on the host PC, `client` on the peer PC.
3. Both enter the agreed non-personal test code, for example `mp-test-01`.
4. For 60 seconds, play normally and issue movement/attack orders. Start at roughly
   the same time where possible. There is no input capture or command timestamping.
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
50ms, individual reads are not atomic, and host/client clocks are not synchronized.
Compare common counter ranges, not just the elapsed timestamps on different PCs.
If the game exits or reading fails, a partial report may remain; it is not a
completed capture. Normal completed captures print `Saved`.

Developer build: .NET Framework csc.exe /platform:x86 /out:MultiplayerCapture.exe MultiplayerCapture.cs
Validator: python tools/validate_multiplayer_csv.py REPORT.csv TEST-CODE host-or-client
