# mp-test-04 findings and limitations

Supplied client and host CSVs each contain 3000 samples spanning ~60 seconds.
Both supplied error files say `require_active_multiplayer`; these are pre-capture
validation failures, not game crash reports. Error filenames do not link to CSVs.

- Host lead_current/target = 3 in all rows; client = 2 in all rows.
- First transition from network state 3 to 4: host 7572ms, client 8045ms.
  Both consumed counters stop at 159. Recording clocks have independent origins.
- Host inactive/network state 0 at 17524ms; client at 18055ms.
- Later samples show new game initialization and counter resets, then another
  state 4 at counter 163. Old capture incorrectly mixed matches into one CSV.
- Initial uninterrupted client segment has no observed right-button-down samples
  or queued command bytes. Host has three observed right-button rising edges.
  These do not establish one-second end-to-end latency. No unit-motion timestamp
  is recorded, and sampled input/queue changes do not identify individual orders.

Local host process inspection found the same game PID still alive, with its
original startup time. Its diagnostics contain no fatal exception, exit record
or crash dump. Thus the evidence supports a match interruption, not a proven host
process crash caused by capture. Client process survival was not inspected.

Lead mismatch is a concrete configuration defect and plausible contributor to
multiplayer synchronization failure, but causality is not yet established. The
collector only requests process read/query access; it has no game terminate/write
operation. This does not prove that observation overhead or focus changes are
irrelevant. Do not bypass readiness or alter cadence based on this interrupted,
mismatched test.

v1.4.6 retires the unproven lead reduction, forces original lead on newly launched
games, rejects capture with a stale patched game, and stops recording at the first
session-state change. Both peers must update/restart. The multiplayer response
delay and definitive cause of match termination remain unverified.
