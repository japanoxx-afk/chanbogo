# Matched host/client analysis

Both validated schema-2 traces contain 3,000 samples over about 60 seconds.
Both advance the consumed counter by 300 and retain lead_current/lead_target=2.
The submitted-minus-consumed difference is 2 in every sample on both PCs.
The overlapping consumed counter range is 1352–1579 (228 values).

| Observed measure | Host | Client |
| --- | ---: | ---: |
| Mean interval between observed counter changes | 200.134 ms | 200.114 ms |
| Minimum / maximum interval | 91 / 305 ms | 153 / 310 ms |
| Completed nonzero outgoing-queue runs | 11 | 14 |
| Longest observed nonzero queue run | 261 ms | 360 ms |
| readiness_block nonzero samples | 0 | 0 |

These are sampled, non-atomic engine fields, not packet RTT or individual command
latency. A nonzero queue run can contain several commands. Zero readiness_block
does not rule out other readiness checks or transport delay. The lower counter
intervals can reflect catch-up and sampling, not a changed target cadence.

The evidence confirms the 3->2 patch is active on both machines, not just the
host. It does not locate the entire user-reported one-second response delay.
There are no click timestamps, individual packet IDs, or unit-start timestamps.
The traces have different recording start times and engine-clock offsets; their
absolute timestamps cannot be subtracted to measure host-client latency.

Do not reduce buffers again or bypass peer readiness based on these traces alone.
The launcher/gameplay patch remains unchanged. Capture v3 adds foreground-only
right-button state to separate approximate input-to-queue timing from later
waiting, and adds role-specific launch scripts and input retry/default handling.
It still cannot prove individual execution latency or replace an in-game test.

Reproduce numeric analysis:

`py -3 tools/compare_multiplayer.py diagnostics-public/mp-test-03/host/capture-01.csv diagnostics-public/mp-test-03/client/capture-01.csv mp-test-03`
