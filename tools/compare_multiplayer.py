"""Compare validated numeric traces without treating unsynchronized clocks as ping."""
import collections
import csv
import statistics
import sys
from validate_multiplayer_csv import validate

def summarize(path, session, role):
    validate(path, session, role)
    with open(path, encoding='utf-8') as file:
        rows = [{k: int(v) for k, v in r.items() if k not in ('role', 'session')}
                for r in csv.DictReader(file)]
    transitions = [r['elapsed_ms'] for a, r in zip(rows, rows[1:]) if a['counter_0c'] != r['counter_0c']]
    intervals = [b-a for a, b in zip(transitions, transitions[1:])]
    occupancy, start = [], None
    for r in rows:
        if r['queued_command_bytes'] and start is None:
            start = r['elapsed_ms']
        if not r['queued_command_bytes'] and start is not None:
            occupancy.append(r['elapsed_ms']-start)
            start = None
    print(role, 'samples', len(rows), 'counter advance', rows[-1]['counter_0c']-rows[0]['counter_0c'])
    for field in ('mode', 'active', 'net_state', 'readiness_block', 'lead_target'):
        print(field, dict(collections.Counter(r[field] for r in rows)))
    print('observed transition interval min/mean/max ms:', min(intervals), statistics.mean(intervals), max(intervals))
    print('completed nonzero queue occupancy runs ms:', occupancy, '(NOT individual command latency)')
    first = {}
    for r in rows:
        first.setdefault(r['counter_0c'], r)
    return first

if __name__ == '__main__':
    host = summarize(sys.argv[1], sys.argv[3], 'host')
    client = summarize(sys.argv[2], sys.argv[3], 'client')
    overlap = sorted(host.keys() & client.keys())
    print('common counter range/count:', (overlap[0], overlap[-1], len(overlap)) if overlap else None)
    print('No absolute cross-PC latency: clocks/start times are not synchronized.')
