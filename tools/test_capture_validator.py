import csv
import pathlib
import tempfile
from validate_multiplayer_csv import HEADER, HEADER2, HEADER3, validate

with tempfile.TemporaryDirectory() as folder:
    path = pathlib.Path(folder) / 'capture.csv'
    for header, schema in ((HEADER, '1'), (HEADER2, '2'), (HEADER3, '3')):
        row = [schema, 'test', 'host', '0', '1', '1', '3', '10', '12', '0', '100', '-1']
        if schema in ('2', '3'):
            row += ['2', '2', '16']
        if schema == '3':
            row += ['1', '1']
        def save(current):
            with path.open('w', newline='', encoding='utf-8') as file:
                writer = csv.writer(file)
                writer.writerow(header)
                writer.writerow(current)
        save(row)
        assert validate(path, 'test', 'host') == 1
        for index, value in ((2, 'client'), (4, '-1'), (11, '2147483648'), (len(row)-1, 'private text')):
            bad = row.copy()
            bad[index] = value
            save(bad)
            try:
                validate(path, 'test', 'host')
            except ValueError:
                pass
            else:
                raise AssertionError((schema, index, value))
print('PASS schema 1/2/3, signed drift, role mismatch, bounds and private-text rejection')
