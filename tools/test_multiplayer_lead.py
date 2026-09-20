"""Run actual initializer and refill instructions; not a network/gameplay test."""
import hashlib
import pathlib
import struct
import sys
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE
from unicorn.x86_const import *

data = pathlib.Path(sys.argv[1]).read_bytes()
assert hashlib.sha256(data).hexdigest() == '16e4c3d3d17438928839a09901a4159da154eb0ec822c784856707b77f9f4b1f'
image = pefile.PE(data=data).get_memory_mapped_image()
assert image[0x88ae1:0x88ae7] == bytes.fromhex('6a03eb026a01')

for mode in (0, 1, 2):
    for patched in (False, True):
        uc = Uc(UC_ARCH_X86, UC_MODE_32)
        uc.mem_map(0x400000, 0x500000)
        uc.mem_write(0x400000, image)
        uc.mem_map(0x20000000, 0x10000)
        if patched:
            uc.mem_write(0x488ae2, b'\x02')
        def put(a, v): uc.mem_write(a, struct.pack('<I', v))
        def get(a): return struct.unpack('<I', uc.mem_read(a, 4))[0]
        state = {'sent': 0, 'consumed': 0}
        def hook(uc, pc, size, user):
            if pc not in (0x450bc1, 0x450daa, 0x450db5):
                return
            if pc == 0x450bc1:
                state['sent'] += 1
                result = 1
            else:
                result = state['consumed' if pc == 0x450daa else 'sent']
            esp = uc.reg_read(UC_X86_REG_ESP)
            uc.reg_write(UC_X86_REG_EAX, result)
            uc.reg_write(UC_X86_REG_EIP, get(esp))
            uc.reg_write(UC_X86_REG_ESP, esp + 4)
        uc.hook_add(UC_HOOK_CODE, hook)
        obj = 0x71d1f0
        put(0x70e8c4, mode)
        uc.reg_write(UC_X86_REG_ESI, obj)
        uc.reg_write(UC_X86_REG_EDI, 0)
        uc.reg_write(UC_X86_REG_EBX, 0x71c768)
        uc.reg_write(UC_X86_REG_EBP, 0x20009000)
        uc.reg_write(UC_X86_REG_ESP, 0x20008000)
        uc.emu_start(0x488ad9, 0x488b16, count=1000)
        lead = 1 if mode == 0 else (2 if patched else 3)
        assert get(obj + 0x98) == get(obj + 0x9c) == state['sent'] == lead
        assert uc.reg_read(UC_X86_REG_ESP) == 0x20008000
        for consumed in range(1, 100):
            state['consumed'] = consumed
            uc.emu_start(0x488e5b, 0x488ec7, count=1000)
            assert uc.reg_read(UC_X86_REG_EIP) == 0x488ec7
            assert state['sent'] - consumed == lead
            assert uc.reg_read(UC_X86_REG_ESP) == 0x20008000
        print(f'PASS mode={mode} patched={patched}: initializer and 99 refill cycles lead={lead}')
print('PASS: actual x86 initializer/refill; network timing and peer synchronization NOT tested')
