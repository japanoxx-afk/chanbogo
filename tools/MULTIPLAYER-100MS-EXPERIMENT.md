# 1.5.0 multiplayer 100ms experiment

This is a gameplay timing prototype, NOT a proven fix or routine update.
Extract to a NEW folder. Keep your 1.4.6 launcher. Every participant must close
the game and launch with THIS 1.5.0 experiment; never mix with normal builds.
The launcher cannot verify the remote participant's build automatically.

The exact-hash in-memory scheduler clone changes multiplayer batch cadence from
200 to 100ms, scales simulation credit by 0.5, and scales the drift-controller
time constants/clamps to 100ms. It preserves lead=3, readiness checks, packet
format and the controller's original branches. Single-player uses the existing
50ms patch. Original game EXE and display/minimap logic remain unchanged.

Offline x86 tests cover both modes, multiple speeds, relocation, stack/register
preservation, peer-not-ready gating, combined single/multi routing and 20-second
simulation-credit accumulation. Fractional speeds retain bounded initial credit
differences (not growing in that test). Transport, checksum synchronization,
nonzero peer clock drift and real multiplayer determinism are NOT validated.

First test a short disposable match, without saving over anything important.
Stop if units disagree, gameplay speed changes, the game stalls or disconnects.
Then all participants exit and return to 1.4.6. Closing the game removes the
memory-only patch. If stable, compare movement/attack delay and use the built-in
multiplayer capture to check whether observed batch rate increases toward 10/sec.
That rate is NOT an end-to-end latency measurement.

Normal update.json remains 1.4.6; this experiment requires an explicit download.
Build: build.ps1 -OutputDirectory bin/experiment-1.5.0 -MultiplayerExperiment
