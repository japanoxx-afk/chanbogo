"""Generate a relocatable single-player scheduler clone from one verified build.

Prints the manifest; never writes or distributes the game executable.
Requires pefile and capstone, only for developers regenerating the resource.
"""
import hashlib
import struct
import sys
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_32

HASH = '16e4c3d3d17438928839a09901a4159da154eb0ec822c784856707b77f9f4b1f'
START, END, BASE = 0x488bb3, 0x48904d, 0x10000000

def generate(path):
    raw = open(path, 'rb').read()
    assert hashlib.sha256(raw).hexdigest() == HASH, 'Unsupported executable'
    pe = pefile.PE(data=raw)
    md = Cs(CS_ARCH_X86, CS_MODE_32)
    instructions = list(md.disasm(pe.get_data(START-0x400000, END-START), START))
    assert instructions[-1].address + instructions[-1].size == END
    # The existing float constant is read-only and covered by the whole-file hash.
    section = pe.sections[1]
    const_offset = section.get_data().find(struct.pack('<f', 50.0))
    assert const_offset >= 0
    fifty = 0x400000 + section.VirtualAddress + const_offset
    changes = {
        0x488c71: ('b8c8000000', b'\xb8'+struct.pack('<i',50)),
        0x488c88: ('81c738ffffff', b'\x81\xc7'+struct.pack('<i',-50)),
        0x488cae: ('8d8f38ffffff', b'\x8d\x8f'+struct.pack('<i',-50)),
        0x488cb4: ('81c738ffffff', b'\x81\xc7'+struct.pack('<i',-50)),
        0x488db6: ('d80d88d76900', b'\xd8\x0d'+struct.pack('<I',fifty)),
        0x488dc3: ('b839ffffff', b'\xb8'+struct.pack('<i',-49)),
        0x488dcb: ('b9c7000000', b'\xb9'+struct.pack('<i',49)),
        0x488e3d: ('05c8000000', b'\x05'+struct.pack('<i',50)),
        # Original credit is 200 / speed_ms. Scale to 50 / speed_ms;
        # leave simulation ticks, AI time, animation and game-speed tables alone.
        0x488f14: ('d88680000000', bytes.fromhex('d80de4d36900d88680000000')),
    }
    # Single-player has no remote clock to chase. Anchor each successful batch
    # to now instead of the multiplayer drift controller. It otherwise couples
    # short batches to a controller tuned for 200ms.
    branch_override = {0x488ccc: ('jmp', 0x488cf2)}
    mapping, cursor = {}, 64
    def branch(i):
        return i.mnemonic in ('call','jmp') or i.mnemonic.startswith('j')
    def relative(i):
        return branch(i) and i.op_str.startswith('0x')
    for i in instructions:
        mapping[i.address] = cursor
        mnemonic = branch_override.get(i.address,(i.mnemonic,0))[0]
        cursor += (5 if mnemonic in ('call','jmp') else 6) if relative(i) else len(changes.get(i.address, ('',i.bytes))[1])
    result = bytearray(b'\x90'*64)
    relocs = []
    # Preserve flags in both modes. Multiplayer bypasses the clone completely.
    stub = bytes.fromhex('9c833dc4e87000007506') + b'\x9d\xe9' + struct.pack('<i',64-16)
    stub += b'\x9d' + bytes.fromhex('558bec83ec40') + b'\xe9'
    relocs.append(len(stub))
    stub += struct.pack('<i',START+6-(BASE+len(stub)+4))
    result[:len(stub)] = stub
    for i in instructions:
        offset = len(result)
        assert offset == mapping[i.address]
        if relative(i):
            mnemonic, target = branch_override.get(i.address,(i.mnemonic,int(i.op_str,16)))
            if mnemonic == 'call': op = b'\xe8'
            elif mnemonic == 'jmp': op = b'\xe9'
            else:
                condition = (i.bytes[0] & 15) if len(i.bytes)==2 else (i.bytes[1] & 15)
                op = bytes((0x0f,0x80+condition))
            if START <= target < END:
                destination = BASE + mapping[target]
            else:
                destination = target
                relocs.append(offset+len(op))
            result.extend(op+struct.pack('<i',destination-(BASE+offset+len(op)+4)))
        elif i.address in changes:
            expected,replacement = changes[i.address]
            assert i.bytes.hex() == expected, (hex(i.address),i.bytes.hex())
            result.extend(replacement)
        else:
            result.extend(i.bytes)
    return bytes(result), relocs, mapping

if __name__ == '__main__':
    code, relocs, _ = generate(sys.argv[1])
    print(HASH)
    print(code.hex())
    print(','.join(str(r) for r in relocs))
