# Stable follow-up after original lead restoration

Inputs: host-d8c80b60 and host-77a2e2c3 (second file supplied as guest).
The second CSV declares role=host but mode=2 throughout; treat it as client.
Do not rewrite the original evidence. The recorder source now derives output role
from the verified game's current mode rather than relying solely on the dropdown.

Both files have 3,000 rows, ~60s duration, lead_target=3, active=1, net_state=3,
readiness_block=0, and consumed counter advance=300. No interruption is observed
during these recordings. This supports stability in this trial, not proof that
lead mismatch caused the previous interruption or that crashes are eliminated.

| Sampled measure | Host | Guest |
|---|---:|---:|
| Mean counter-change interval | 200.027ms | 200.027ms |
| Min/max interval | 155/305ms | 154/295ms |
| Observed right-button rising edges | 12 | 12 |
| First nonzero queue observed within 500ms of edge | 11 | 11 |
| Time from edge to that observation | 15–168ms | 108–232ms |
| Time from edge to subsequent observed queue clear | 136–380ms | 171–360ms |

Host 15ms observation follows the second of closely spaced clicks, and can belong
to the earlier click. All associations are nearest temporal observations, NOT
proof of individual command identity. Unobserved queue changes may occur between
samples. The collector does not measure unit motion, receipt by a peer, packet
RTT, or actual command execution. One missing queue association on each machine
does not prove a dropped command.

Three lead batches at nominal 200ms represent ~600ms of scheduler lookahead;
adding sampled input/batching waits is consistent with the reported near-second
response, but is only a structural hypothesis. Previous lead=2 did not produce
user-confirmed improvement. Do not describe the hypothesis as a proven fix.

Read-only inspection of the executable's PeekMessage/GetMessage and Sleep call
sites did not establish a safe input-delay patch point. The obvious Sleep(500)
site has not been shown to belong to the input path; changing it would be blind.
No new network timing, readiness, simulation or game binary modifications are
made from this evidence. Response-delay fix remains unfinished. Existing paired
records can support further code tracing without requesting the same capture again.
