"""Read-only sampled-input analysis. Never equates queue observations with orders."""
import csv
import statistics
import sys
from validate_multiplayer_csv import validate

def analyze(path):
    with open(path, encoding='utf-8') as file:
        raw = list(csv.DictReader(file))
    validate(path, raw[0]['session'], raw[0]['role'])
    modes = {int(r['mode']) for r in raw}
    actual = {1: 'host', 2: 'client'}.get(next(iter(modes)), 'unknown') if len(modes) == 1 else 'mixed'
    print('declared role:', raw[0]['role'], 'observed mode role:', actual)
    rows = [{k: int(v) for k, v in r.items() if k not in ('session', 'role')} for r in raw]
    if any(r['active'] != 1 or r['net_state'] != 3 for r in rows):
        raise ValueError('Interrupted/mixed match: do not aggregate input timings')
    print('samples:', len(rows), 'lead targets:', sorted({r['lead_target'] for r in rows}))
    print('counter advance:', rows[-1]['counter_0c'] - rows[0]['counter_0c'])
    previous = rows[0]['counter_0c']
    times = []
    for r in rows[1:]:
        if r['counter_0c'] < previous:
            raise ValueError('Counter reset: multiple matches')
        if r['counter_0c'] != previous:
            times.append(r['elapsed_ms'])
        previous = r['counter_0c']
    intervals = [b-a for a,b in zip(times,times[1:])]
    if intervals:
        print('counter interval min/mean/max ms:', min(intervals), round(statistics.mean(intervals), 3), max(intervals))
    clicks = [i for i,r in enumerate(rows) if r['right_button_down'] and (i == 0 or not rows[i-1]['right_button_down'])]
    print('click_ms, next_observed_nonzero_queue_ms, next_observed_clear_ms')
    for i in clicks:
        start = rows[i]['elapsed_ms']
        q = next((j for j in range(i,len(rows)) if rows[j]['elapsed_ms']-start <= 500 and rows[j]['queued_command_bytes']), None)
        end = next((j for j in range(q+1,len(rows)) if not rows[j]['queued_command_bytes']), None) if q is not None else None
        print(start, None if q is None else rows[q]['elapsed_ms']-start, None if end is None else rows[end]['elapsed_ms']-start)
    print('CAUTION: nearest observations only; NOT identified command, send, or unit-motion latency.')

if __name__ == '__main__':
    for path in sys.argv[1:]:
        analyze(path)
