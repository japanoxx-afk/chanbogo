"""Verify camera initialization using the original x86 instructions."""
import hashlib, pathlib, struct, sys, math
from unicorn import Uc, UC_ARCH_X86, UC_MODE_32
from unicorn.x86_const import UC_X86_REG_ESI
b=pathlib.Path(sys.argv[1]).read_bytes()
assert hashlib.sha256(b).hexdigest()=='16e4c3d3d17438928839a09901a4159da154eb0ec822c784856707b77f9f4b1f'
assert struct.unpack_from('<f',b,0x29d7bc)[0]==36.0
for percent in range(100,151):
    u=Uc(UC_ARCH_X86,UC_MODE_32)
    u.mem_map(0x400000,0x500000);u.mem_write(0x400000,b)
    distance=struct.unpack('<f',struct.pack('<f',36*percent/100))[0]
    u.mem_write(0x69d7bc,struct.pack('<f',distance))
    span=struct.unpack('<f',struct.pack('<f',26*percent/100))[0]
    u.mem_write(0x69d7c0,struct.pack('<f',span))
    u.reg_write(UC_X86_REG_ESI,0x800000)
    u.emu_start(0x42e0e6,0x42e103)
    assert struct.unpack('<fff',u.mem_read(0x800014,12))==(distance,span,40.)
    angle=2*math.atan2(span/2,distance)
    assert abs(angle-2*math.atan2(13,36))<1e-7
    assert abs(2*distance*math.tan(angle/2)-26*percent/100)<0.00001
print('PASS 51 distance/span pairs: larger view span, stable projection angle and pitch')
