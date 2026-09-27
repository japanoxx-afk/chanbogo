"""Emulate the all-unit training epilogue; farmers must not be halved twice."""
import pathlib,re,struct,sys
from unicorn import Uc, UC_ARCH_X86, UC_MODE_32
from unicorn.x86_const import *
source=pathlib.Path('SinglePlayerPatch.cs').read_text(encoding='utf-8-sig')
code=bytes.fromhex(re.search(r'var code=Decode\("(85c0[0-9a-f]+)"\)',source)[1])
b=pathlib.Path(sys.argv[1]).read_bytes()
assert b[0x16237:0x1623e].hex()=='5f5e5bc9c20400'
assert b[0x161b2:0x161b7].hex()=='db4074eb4b' # Time1 -> common fixed-point conversion
assert struct.unpack_from('<f',b,0x29d594)[0]==65536.0
for unit in range(-1,123):
 for time in (0,1,65536,15*65536,56*65536):
  u=Uc(UC_ARCH_X86,UC_MODE_32);u.mem_map(0x100000,0x1000);u.mem_map(0x200000,0x2000)
  u.mem_write(0x100000,code);bp=0x201000
  u.mem_write(bp-32,struct.pack('<III',31,32,33))
  u.mem_write(bp,struct.pack('<III',0x201800,0x100800,unit&0xffffffff))
  for reg,val in [(UC_X86_REG_EBP,bp),(UC_X86_REG_ESP,bp-32),(UC_X86_REG_EAX,time),(UC_X86_REG_EBX,999)]:u.reg_write(reg,val)
  u.emu_start(0x100000,0x100800,count=30)
  assert u.reg_read(UC_X86_REG_EAX)==time//2
  assert u.reg_read(UC_X86_REG_ESP)==bp+12
  assert [u.reg_read(r) for r in (UC_X86_REG_EDI,UC_X86_REG_ESI,UC_X86_REG_EBX,UC_X86_REG_EBP)]==[31,32,33,0x201800]
print('PASS 620 cases: all unit training halved once, including farmers; stack/register return preserved')
