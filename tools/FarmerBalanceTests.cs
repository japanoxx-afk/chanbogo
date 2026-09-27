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
    int changed=0;for(int i=0;i<before.Length;i++)if(before[i]!=after[i]){Check(i==0x1a6c35||i==0x1a874e||i==0x1aa769);Check(after[i]==50);changed++;}
    Check(changed==3);Check(FarmerBalancePatch.Hash(File.ReadAllBytes(dat+".farmer-original.bak"))==FarmerBalancePatch.OriginalHash);
    FarmerBalancePatch.Apply(exe);Check(FarmerBalancePatch.Hash(after)==FarmerBalancePatch.Hash(File.ReadAllBytes(dat)));
    after[100]^=1;File.WriteAllBytes(dat,after);bool refused=false;
    try {FarmerBalancePatch.Apply(exe);}catch(InvalidDataException){refused=true;}
    Check(refused);Check(FarmerBalancePatch.Hash(after)==FarmerBalancePatch.Hash(File.ReadAllBytes(dat)));
    Console.WriteLine("PASS three costs only, original backup, idempotence, unknown data refused; fixtures: "+dir);
  }
}
