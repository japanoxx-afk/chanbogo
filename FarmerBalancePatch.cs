using System;
using System.IO;
using System.Security.Cryptography;

namespace ChangpogoLauncher {
  // Three playable factions only: JC (3), JD (25), JJ (38).
  // Do not modify campaign JS farmers or neutral units.
  static class FarmerBalancePatch {
    internal const string OriginalHash="d5f453293096141cb7d9c0038dcf447ca7950246d26af58114e51a8e964c6600";
    static readonly int[] Costs={0x1a6c35,0x1a874e,0x1aa769};
    // Data_Buildings.BuildTime, all 44 rows; offsets + original float values.
    static readonly int[] BuildOffsets={0x1043f4,0x104672,0x1048fc,0x104b7a,0x104e00,0x10507c,0x1052d0,0x105526,0x10578a,0x105a0e,0x105c76,0x105f06,0x10618e,0x106416,0x106696,0x1068f8,0x106b4a,0x106d9e,0x107000,0x107284,0x1074ec,0x107770,0x1079f8,0x107c6c,0x107eea,0x10814a,0x10839a,0x1085ec,0x108844,0x108ab0,0x108d18,0x108f8a,0x10920c,0x109498,0x10971c,0x109982,0x109bd8,0x109e30,0x10a090,0x10a312,0x10a5a2,0x10a826,0x10aaa2,0x10ad2a};
    static readonly float[] BuildTimes={70,60,80,80,80,150,100,120,40,70,60,70,80,80,80,150,100,120,40,80,60,60,60,70,70,100,100,120,65,70,70,60,60,80,80,150,100,120,70,40,80,70,60,60};
    internal static string Hash(byte[] bytes) {
      using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
    }
    internal static byte[] Original(byte[] bytes) {
      var original=(byte[])bytes.Clone();
      if(original.Length!=1954113)throw new InvalidDataException("농부 패치: 지원하지 않는 Changpogo.dat입니다.");
      foreach(int offset in Costs) {
        int cost=BitConverter.ToInt32(original,offset);
        if(cost!=75&&cost!=50)throw new InvalidDataException("농부 비용 데이터가 예상과 다릅니다.");
        Buffer.BlockCopy(BitConverter.GetBytes(75),0,original,offset,4);
      }
      for(int i=0;i<BuildOffsets.Length;i++) {
        float value=BitConverter.ToSingle(original,BuildOffsets[i]);
        if(value!=BuildTimes[i]&&value!=BuildTimes[i]/2)throw new InvalidDataException("건물 건설시간 데이터가 예상과 다릅니다.");
        Buffer.BlockCopy(BitConverter.GetBytes(BuildTimes[i]),0,original,BuildOffsets[i],4);
      }
      if(Hash(original)!=OriginalHash)throw new InvalidDataException("농부 패치: 원본과 다른 게임 데이터입니다. 파일을 변경하지 않았습니다.");
      return original;
    }
    internal static void Apply(string executable) {
      if(Hash(File.ReadAllBytes(executable))!="16e4c3d3d17438928839a09901a4159da154eb0ec822c784856707b77f9f4b1f")throw new InvalidDataException("농부 패치: 지원하지 않는 실행 파일입니다.");
      string path=Path.Combine(Path.GetDirectoryName(executable),"Changpogo.dat");
      string backup=path+".farmer-original.bak";
      // Keep an exclusive handle: no partially patched database can be opened.
      using(var stream=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {
        var bytes=new byte[checked((int)stream.Length)];
        int read=0,n;while(read<bytes.Length&&(n=stream.Read(bytes,read,bytes.Length-read))>0)read+=n;
        if(read!=bytes.Length)throw new EndOfStreamException();
        var original=Original(bytes);
        if(File.Exists(backup)) {
          if(Hash(File.ReadAllBytes(backup))!=OriginalHash)throw new InvalidDataException("농부 원본 백업 검증 실패: "+backup);
        } else {
          using(var saved=new FileStream(backup,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
            saved.Write(original,0,original.Length);saved.Flush(true);
          }
        }
        foreach(int offset in Costs) {
          if(BitConverter.ToInt32(bytes,offset)==50)continue;
          stream.Position=offset;var value=BitConverter.GetBytes(50);stream.Write(value,0,4);
        }
        for(int i=0;i<BuildOffsets.Length;i++) {
          stream.Position=BuildOffsets[i];var value=BitConverter.GetBytes(BuildTimes[i]/2);stream.Write(value,0,4);
        }
        stream.Flush(true);
        for(int i=0;i<BuildOffsets.Length;i++) {
          stream.Position=BuildOffsets[i];var value=new byte[4];
          if(stream.Read(value,0,4)!=4||BitConverter.ToSingle(value,0)!=BuildTimes[i]/2)throw new IOException("건설시간 패치 검증 실패");
        }
        foreach(int offset in Costs) {
          stream.Position=offset;var value=new byte[4];
          if(stream.Read(value,0,4)!=4||BitConverter.ToInt32(value,0)!=50)throw new IOException("농부 비용 패치 검증 실패");
        }
      }
    }
  }
}
