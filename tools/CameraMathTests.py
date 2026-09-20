"""Verify camera-distance evidence against the supported executable, no writes."""
import hashlib, struct, sys
from unicorn import Uc, UC_ARCH_X86, UC_MODE_32
from unicorn.x86_const import UC_X86_REG_ECX, UC_X86_REG_ESP
d = open(sys.argv[1], 'rb').read()
assert hashlib.sha256(d).hexdigest() == '16e4c3d3d17438928839a09901a4159da154eb0ec822c784856707b77f9f4b1f'
assert struct.unpack_from('<f',d,0x29d7bc)[0] == 36.0
# Execute the actual level-load assignments. Validate only distance changes,
# keeping field-of-view (26 degrees) and pitch (40 degrees) identical.
for distance in [36.,45.,54.]:
    u=Uc(UC_ARCH_X86,UC_MODE_32)
    u.mem_map(0x400000,0x500000); u.mem_write(0x400000,d)
    u.mem_write(0x69d7bc,struct.pack('<f',distance))
    from unicorn.x86_const import UC_X86_REG_ESI
    u.reg_write(UC_X86_REG_ESI,0x800000)
    # Constructor's default distance / FOV / pitch assignments (no calls).
    u.emu_start(0x42e0e6,0x42e103)
    assert struct.unpack('<fff',u.mem_read(0x800014,12)) == (distance,26.,40.)
print('PASS actual x86 camera defaults: distance 36/45/54, FOV=26, pitch=40 unchanged')
