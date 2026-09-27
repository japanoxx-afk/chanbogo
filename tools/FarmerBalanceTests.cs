using System;
using System.IO;
using ChangpogoLauncher;
class FarmerBalanceTests {
  static void Check(bool ok) {if(!ok)throw new Exception("Balance test failed");}
  static void Main(string[] args) {
    string dir=Path.Combine(Path.GetTempPath(),"ChangpogoBalanceTest-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
    string exe=Path.Combine(dir,"Changpogo.exe"),dat=Path.Combine(dir,"Changpogo.dat");
    File.Copy(Path.Combine(args[0],"Changpogo.exe"),exe);File.Copy(Path.Combine(args[0],"Changpogo.dat"),dat);
    var before=FarmerBalancePatch.Original(File.ReadAllBytes(dat));
    FarmerBalancePatch.Apply(exe);var after=File.ReadAllBytes(dat);
    var offsets=(int[])typeof(FarmerBalancePatch).GetField("BuildOffsets",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
    Check(offsets.Length==44);
    foreach(int offset in offsets)Check(BitConverter.ToSingle(after,offset)==BitConverter.ToSingle(before,offset)/2);
    foreach(int offset in new[]{0x1a6c35,0x1a874e,0x1aa769})Check(BitConverter.ToInt32(after,offset)==50);
    for(int i=0;i<before.Length;i++)if(before[i]!=after[i]) {
      bool allowed=i==0x1a6c35||i==0x1a874e||i==0x1aa769;
      foreach(int offset in offsets)if(i>=offset&&i<offset+4)allowed=true;
      Check(allowed);
    }
    Check(FarmerBalancePatch.Hash(File.ReadAllBytes(dat+".farmer-original.bak"))==FarmerBalancePatch.OriginalHash);
    FarmerBalancePatch.Apply(exe);Check(FarmerBalancePatch.Hash(after)==FarmerBalancePatch.Hash(File.ReadAllBytes(dat)));
    after[100]^=1;File.WriteAllBytes(dat,after);bool refused=false;
    try {FarmerBalancePatch.Apply(exe);}catch(InvalidDataException){refused=true;}
    Check(refused);Check(FarmerBalancePatch.Hash(after)==FarmerBalancePatch.Hash(File.ReadAllBytes(dat)));
    Console.WriteLine("PASS three costs + 44 build times only, original backup, idempotence, unknown data refused; fixtures: "+dir);
  }
}
