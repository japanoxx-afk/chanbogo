"""Strict allowlist gate for public timing CSVs. Never accepts logs or dumps."""
import csv
import re
import sys
from pathlib import Path

HEADER = 'schema,session,role,elapsed_ms,mode,active,net_state,counter_0c,counter_14,readiness_block,scheduler_clock,drift_accumulator'.split(',')
HEADER2 = HEADER + ['lead_current', 'lead_target', 'queued_command_bytes']

def validate(path, session, role):
    if not re.fullmatch(r'[a-z0-9-]{1,32}', session) or role not in ('host', 'client'):
        raise ValueError('Invalid expected session or role')
    if Path(path).stat().st_size > 2*1024*1024:
        raise ValueError('Oversized report')
    with open(path, encoding='utf-8', newline='') as file:
        reader = csv.reader(file)
        header = next(reader)
        if header not in (HEADER, HEADER2):
            raise ValueError('Unexpected fields')
        schema = '1' if header == HEADER else '2'
        count, previous = 0, -1
        for row in reader:
            if len(row) != len(header) or row[:3] != [schema, session, role]:
                raise ValueError('Unexpected metadata')
            if any(not re.fullmatch(r'-?[0-9]{1,10}', value) for value in row[3:]):
                raise ValueError('Non-numeric telemetry')
            values = list(map(int, row[3:]))
            if not previous <= values[0] <= 121000:
                raise ValueError('Invalid elapsed time')
            drift_index = header.index('drift_accumulator') - 3
            if any(not (-2147483648 <= value <= 2147483647 if i == drift_index else 0 <= value <= 0xffffffff) for i, value in enumerate(values[1:], 1)):
                raise ValueError('Out-of-range telemetry')
            previous = values[0]
            count += 1
        if not 1 <= count <= 10000:
            raise ValueError('Invalid sample count')
    return count

if __name__ == '__main__':
    print('PASS allowlisted CSV:', validate(*sys.argv[1:]), 'rows')
