"""Execute the real wheel handler through its final camera setter call."""
import pathlib, struct, sys
from unicorn import *
from unicorn.x86_const import *
b=pathlib.Path(sys.argv[1]).read_bytes()
assert b[0x3970a:0x39710].hex()=='d80d28d96900'
assert b[0x39785:0x3978b].hex()=='d90520d96900'
for percent in (100,125,150):
    u=Uc(UC_ARCH_X86,UC_MODE_32);u.mem_map(0x400000,0x500000);u.mem_write(0x400000,b)
    max_span=26*percent/100
    if percent!=100:
        u.mem_write(0x880000,struct.pack('<ff',max_span-14,max_span))
        u.mem_write(0x43970c,struct.pack('<I',0x880000))
        u.mem_write(0x439787,struct.pack('<I',0x880004))
    def stop(uc,addr,size,data):
        if addr==0x42fcee:uc.emu_stop()
    u.hook_add(UC_HOOK_CODE,stop)
    # Repeated wheel-in/out endpoints and intermediate positions, including
    # overshoot to exercise both clamps. Setter input is the displayed span.
    for q in [50,40,30,24,30,40,50,60,10,50]*3:
        u.mem_write(0x71e154,struct.pack('<f',90-q))
        u.mem_write(0x810164,struct.pack('<ff',q,0))
        u.reg_write(UC_X86_REG_ECX,0x810000);u.reg_write(UC_X86_REG_ESP,0x820000)
        u.emu_start(0x4396d1,0x4397c7,count=300)
        assert u.reg_read(UC_X86_REG_EIP)==0x42fcee
        span=struct.unpack('<f',u.mem_read(u.reg_read(UC_X86_REG_ESP)+4,4))[0]
        expected=max(14,min(max_span,14+(q-24)/26*(max_span-14)))
        assert abs(span-expected)<0.0001,(percent,q,span,expected)
    assert u.mem_read(0x69d928,4)==b[0x29d928:0x29d92c]
    assert u.mem_read(0x69d920,4)==b[0x29d920:0x29d924]
print('PASS 90 real wheel-handler cases: near/far/repeat/clamps, shared constants unchanged')
