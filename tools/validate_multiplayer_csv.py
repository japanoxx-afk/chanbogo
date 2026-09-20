"""Strict allowlist gate for public timing CSVs. Never accepts logs or dumps."""
import csv
import re
import sys
from pathlib import Path

HEADER = 'schema,session,role,elapsed_ms,mode,active,net_state,counter_0c,counter_14,readiness_block,scheduler_clock,drift_accumulator'.split(',')

def validate(path, session, role):
    if not re.fullmatch(r'[a-z0-9-]{1,32}', session) or role not in ('host', 'client'):
        raise ValueError('Invalid expected session or role')
    if Path(path).stat().st_size > 1024*1024:
        raise ValueError('Oversized report')
    with open(path, encoding='utf-8', newline='') as file:
        reader = csv.reader(file)
        if next(reader) != HEADER:
            raise ValueError('Unexpected fields')
        count, previous = 0, -1
        for row in reader:
            if len(row) != len(HEADER) or row[:3] != ['1', session, role]:
                raise ValueError('Unexpected metadata')
            if any(not re.fullmatch(r'-?[0-9]{1,10}', value) for value in row[3:]):
                raise ValueError('Non-numeric telemetry')
            values = list(map(int, row[3:]))
            if not previous <= values[0] <= 121000:
                raise ValueError('Invalid elapsed time')
            if any(not 0 <= value <= 0xffffffff for value in values[1:-1]) or not -2147483648 <= values[-1] <= 2147483647:
                raise ValueError('Out-of-range telemetry')
            previous = values[0]
            count += 1
        if not 1 <= count <= 5000:
            raise ValueError('Invalid sample count')
    return count

if __name__ == '__main__':
    print('PASS allowlisted CSV:', validate(*sys.argv[1:]), 'rows')
