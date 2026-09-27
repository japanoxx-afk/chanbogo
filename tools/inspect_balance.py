"""Read-only schema/row inspection of the supported Changpogo database."""
import pathlib
import struct
import sys

def tables(b):
    def u(p): return struct.unpack_from('<I', b, p)[0]
    def s(p):
        assert b[p] == 0
        n = u(p + 1)
        return b[p+5:p+5+n*2].decode('utf-16le').rstrip('\0'), p+5+n*2
    section = struct.unpack_from('<Q', b, 12)[0]
    count = u(section)
    metas = struct.unpack_from('<'+'Q'*count, b, section+4)
    datas = struct.unpack_from('<'+'Q'*count, b, section+4+count*8)
    p = section+4+count*16
    for m, d in zip(metas, datas):
        name, p = s(p)
        n = u(m)
        types = struct.unpack_from('<'+'I'*n, b, m+4)
        q = m+4+n*4
        cols = []
        for t in types:
            col, q = s(q)
            cols.append(col)
        yield name, d, cols, types, s

def rows(b, d, cols, types, s):
    q = d+4
    for _ in range(struct.unpack_from('<I', b, d)[0]):
        row = {}
        for col, t in zip(cols, types):
            off = q
            if t == 7: v, q = s(q)
            else:
                assert t in (2, 4, 5), t
                fmt = '<B' if t == 2 else '<f' if t == 5 else '<i'
                v = struct.unpack_from(fmt, b, q)[0]
                q += struct.calcsize(fmt)
            row[col] = (v, off)
        yield row

if __name__ == '__main__':
    b = pathlib.Path(sys.argv[1]).read_bytes()
    for name, d, cols, types, s in tables(b):
        if name not in ('Data_Units', 'Data_Clans', 'Data_Buildings'): continue
        for row in rows(b, d, cols, types, s):
            if name == 'Data_Units' and 'Farmer' not in str(row.get('Name')): continue
            if name == 'Data_Buildings' and not any(row.get('UnitOut'+str(i), (None,))[0] in (3,25,38,13) for i in range(1,7)): continue
            print(name, {k: (v,hex(o)) for k,(v,o) in row.items() if k in ('Type','Name','Clan','RiceTrainCost') or 'Time' in k or 'UnitOut' in k or 'TrainingRate' in k})
