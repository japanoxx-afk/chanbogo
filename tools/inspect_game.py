"""Read-only disassembly of the locally installed game (not distributed)."""
import sys
import pefile
from capstone import Cs, CS_ARCH_X86, CS_MODE_32

pe = pefile.PE(sys.argv[1])
md = Cs(CS_ARCH_X86, CS_MODE_32)
md.skipdata = True
base = pe.OPTIONAL_HEADER.ImageBase
if sys.argv[2] == 'range':
    start, size = (int(x, 0) for x in sys.argv[3:5])
    for i in md.disasm(pe.get_data(start-base, size), start):
        print(hex(i.address), i.bytes.hex(), i.mnemonic, i.op_str)
else:
    needles = sys.argv[3:]
    for section in pe.sections:
        if not section.Characteristics & 0x20000000:
            continue
        history = []
        for i in md.disasm(section.get_data(), base+section.VirtualAddress):
            line = f'{i.address:#x} {i.mnemonic} {i.op_str}'
            if any(n in i.op_str for n in needles):
                print('\n'.join(history[-4:]+[line]), '\n')
            history.append(line)
