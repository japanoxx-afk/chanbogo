"""Execute original and patched x86 scheduling code with mocked engine services.

Tests cadence, command-queue traversal, simulation credit, relocation, register
preservation and byte-identical multiplayer routing. Not a gameplay benchmark.
"""
import pathlib
import struct
import sys
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_32, UC_HOOK_CODE
from unicorn.x86_const import *
from make_latency_patch import generate, START, END, BASE

path = sys.argv[1]
pe = pefile.PE(path)
code, relocations, mapping = generate(path)
manifest = pathlib.Path(__file__).resolve().parents[1].joinpath('latency.manifest').read_text().splitlines()
assert bytes.fromhex(manifest[1]) == code
assert list(map(int,manifest[2].split(','))) == relocations

def run(patched, mode, address=BASE, speed=100):
    uc = Uc(UC_ARCH_X86, UC_MODE_32)
    uc.mem_map(0x400000, 0x500000)
    uc.mem_write(0x400000,pe.get_memory_mapped_image())
    uc.mem_map(address,0x10000)
    installed = bytearray(code)
    for offset in relocations:
        old = struct.unpack_from('<i',installed,offset)[0]
        struct.pack_into('<I',installed,offset,(old+BASE-address)&0xffffffff)
    uc.mem_write(address,bytes(installed))
    uc.mem_map(0x20000000,0x10000)
    obj, stack, stop = 0x71d1f0,0x20008000,0x20000000
    def put(a,v): uc.mem_write(a,struct.pack('<I',v & 0xffffffff))
    def get(a): return struct.unpack('<I',uc.mem_read(a,4))[0]
    def putf(a,v): uc.mem_write(a,struct.pack('<f',v))
    def getf(a): return struct.unpack('<f',uc.mem_read(a,4))[0]
    put(0x71ccfc,1);put(0x70e8c4,mode)
    put(obj+0x94,1);put(obj+0x98,1);put(obj+0x9c,1)
    putf(obj+0x2c,200.0/speed)
    clock=1; received=0; sent=0; deliveries=[]; queue=[]; batches=[]; credit=0.0
    def handler(uc,pc,size,user):
        nonlocal received,sent,credit
        if START<=pc<END or address<=pc<address+len(code) or 0x689518<=pc<0x68953f:
            return
        esp=uc.reg_read(UC_X86_REG_ESP)
        count,value=0,0
        if pc==0x43fe4d: value=3
        elif pc==0x450956: count=4
        elif pc==0x64d207: count=8;value=(get(esp+8)-get(esp+4))&0xffffffff
        elif pc==0x45099a:
            value=1;received=sent;batches.append(clock)
            if queue: deliveries.extend(clock-q for q in queue);queue.clear()
        elif pc==0x451847: value=0
        elif pc==0x450daa: value=received
        elif pc==0x450db5: value=sent
        elif pc==0x450bc1:
            sent+=1;value=1
            if not deliveries and not queue: queue.append(2)
        elif pc==0x446692: count=8
        elif pc==0x580b0b: value=0
        elif pc==0x4e7ae5: value=0
        elif pc in (0x489a8a,0x489e25): pass
        else: raise AssertionError(f'Unexpected engine call {pc:#x}')
        uc.reg_write(UC_X86_REG_EAX,value)
        uc.reg_write(UC_X86_REG_EIP,get(esp))
        uc.reg_write(UC_X86_REG_ESP,esp+4+count)
    uc.hook_add(UC_HOOK_CODE,handler)
    for clock in range(2,2002):
        # The simulation consumes accumulated credit separately; count all credit
        # without modifying the game-speed table or simulation-tick function.
        if (clock-1) % speed == 0:
            putf(obj+0x80,max(0.0,getf(obj+0x80)-1.0))
        before = getf(obj+0x80)
        put(stack,stop);put(stack+4,clock)
        uc.reg_write(UC_X86_REG_ESP,stack);uc.reg_write(UC_X86_REG_ECX,obj)
        for reg in (UC_X86_REG_EBX,UC_X86_REG_ESI,UC_X86_REG_EDI,UC_X86_REG_EBP): uc.reg_write(reg,0x12345678)
        uc.emu_start(address if patched else START,stop,count=30000)
        assert uc.reg_read(UC_X86_REG_EIP)==stop
        assert uc.reg_read(UC_X86_REG_ESP)==stack+8
        for reg in (UC_X86_REG_EBX,UC_X86_REG_ESI,UC_X86_REG_EDI,UC_X86_REG_EBP): assert uc.reg_read(reg)==0x12345678
        credit+=getf(obj+0x80)-before
    return batches,credit,deliveries

for speed in (50,100,150,200):
    original=run(False,0,speed=speed)
    patched=run(True,0,speed=speed)
    assert all(b-a>=50 for a,b in zip(patched[0],patched[0][1:])),patched
    assert abs(original[1]-patched[1])<=200.0/speed,(original,patched)
    assert patched[2][0]<original[2][0],(original,patched)
    print(f'PASS speed={speed}: minimum batch interval 50ms; credit original={original[1]:.4f}, patched={patched[1]:.4f} (within one original batch); mocked queue {original[2][0]}->{patched[2][0]}ms')
for mode in (1,2):
    assert run(False,mode)==run(True,mode)
    print(f'PASS multiplayer mode={mode}: identical original behavior')
assert run(True,0,0x11000000)==run(True,0,BASE)
print('PASS relocation, stack and callee-saved registers; committed manifest reproducible')
