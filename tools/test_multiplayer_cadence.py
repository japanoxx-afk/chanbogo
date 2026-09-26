"""Offline-only 100ms candidate. No launcher/game files are changed."""
from test_latency_patch import run, path, BASE
from make_latency_patch import generate, combined_candidate
import sys
period = 75 if '--75ms' in sys.argv else 100

candidate = generate(path, multiplayer=True, multiplayer_period=period)
for mode in (1,2):
    for speed in (50,100,150,200):
        original = run(False,mode,speed=speed)
        patched = run(True,mode,speed=speed,candidate=candidate)
        print('candidate',mode,speed,'batches',len(original[0]),len(patched[0]),'credit',original[1],patched[1])
        assert len(patched[0]) > len(original[0])
        assert abs(original[1]-patched[1]) <= 200.0/speed
        # Original also has an immediate initial catch-up batch (201,202,...).
        # Assert the entire expected zero-drift schedule, including that transient.
        if period==100 and speed in (50,100):
            assert patched[0] == [101,102] + list(range(201,2002,100))
            assert original[0] == [201,202] + list(range(401,2002,200))
        else:
            # Simulation-credit gating at fractional game speeds changes spacing.
            assert abs(len(patched[0])-200/period*len(original[0])) <= 3
        assert run(True,mode,0x11000000,speed,candidate) == patched
    blocked = run(True,mode,candidate=candidate,blocked_until=750)
    assert blocked[0] and min(blocked[0]) >= 750
assert run(False,0) == run(True,0,candidate=candidate)
for speed in (50,100,150,200):
    original=run(False,1,speed=speed,duration=20000)
    patched=run(True,1,speed=speed,candidate=candidate,duration=20000)
    print('20-second credit',speed,original[1],patched[1])
    assert abs(original[1]-patched[1]) <= 200.0/speed
print('PASS candidate: both multiplayer modes, speed credit, relocation, peer-not-ready gate and original single routing')
print('NOT VERIFIED: real transport, peer clock drift, checksum synchronization, multiplayer simulation determinism')
combined = combined_candidate(path,period)
import pathlib
manifest = pathlib.Path(__file__).resolve().parents[1].joinpath('multiplayer-75ms.manifest' if period==75 else 'multiplayer-experiment.manifest').read_text().splitlines()
assert bytes.fromhex(manifest[1]) == combined[0]
assert list(map(int,manifest[2].split(','))) == combined[1]
for mode in (0,1,2):
    expected = run(True,0) if mode==0 else run(True,mode,candidate=candidate)
    for address in (BASE,0x11000000):
        assert run(True,mode,address,candidate=combined) == expected
print('PASS combined dispatcher: existing single patch and new multiplayer candidate, both load addresses')
