using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

// Read-only, public-shareable numeric telemetry. No debugger, packet capture or dumps.
class MultiplayerCapture {
  [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,int pid);
  [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool ReadProcessMemory(IntPtr handle,IntPtr address,byte[] buffer,UIntPtr size,out UIntPtr read);
  static uint Read(IntPtr handle,int address) {
    byte[] data=new byte[4];UIntPtr count;
    if(!ReadProcessMemory(handle,new IntPtr(address),data,(UIntPtr)4,out count)||count.ToUInt64()!=4)throw new IOException("Game read unavailable");
    return BitConverter.ToUInt32(data,0);
  }
  static int Main(string[] args) {
    IntPtr handle=IntPtr.Zero;
    try {
      Console.WriteLine("Multiplayer timing capture: local CSV only; no automatic upload.");
      string role=args.Length>0?args[0]:Prompt("Role (host / client): ");
      string session=args.Length>1?args[1]:Prompt("Shared test code (example mp-test-01): ");
      if(role!="host"&&role!="client")throw new ArgumentException("Role must be host or client");
      if(!Regex.IsMatch(session,@"\A[a-z0-9-]{1,32}\z"))throw new ArgumentException("Use only lowercase letters, numbers and hyphens for test code; no personal information");
      int seconds=args.Length>2?int.Parse(args[2]):60;
      if(seconds<1||seconds>120)throw new ArgumentException("Duration must be 1 to 120 seconds");
      var games=Process.GetProcessesByName("Changpogo");
      if(games.Length!=1)throw new InvalidOperationException("Run exactly one Changpogo game first");
      using(var game=games[0]) {
        using(var file=File.OpenRead(game.MainModule.FileName))using(var sha=SHA256.Create()) {
          if(BitConverter.ToString(sha.ComputeHash(file)).Replace("-","")!="16E4C3D3D17438928839A09901A4159DA154EB0EC822C784856707B77F9F4B1F")throw new InvalidOperationException("Unsupported game build");
        }
        handle=OpenProcess(0x1010,false,game.Id);
        if(handle==IntPtr.Zero)throw new IOException("Cannot read game process");
        uint mode=Read(handle,0x70e8c4);
        if((mode!=1&&mode!=2)||Read(handle,0x71ccfc)!=1)throw new InvalidOperationException("Enter an active multiplayer match first");
        string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"reports");Directory.CreateDirectory(folder);
        string output=Path.Combine(folder,session+"-"+role+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".csv");
        using(var writer=new StreamWriter(new FileStream(output,FileMode.CreateNew,FileAccess.Write),new UTF8Encoding(false))) {
          writer.WriteLine("schema,session,role,elapsed_ms,mode,active,net_state,counter_0c,counter_14,readiness_block,scheduler_clock,drift_accumulator");
          var watch=Stopwatch.StartNew();long next=0;
          while(watch.ElapsedMilliseconds<seconds*1000L) {
            writer.WriteLine("1,"+session+","+role+","+watch.ElapsedMilliseconds+","+Read(handle,0x70e8c4)+","+Read(handle,0x71ccfc)+","+Read(handle,0x71c7ec)+","+Read(handle,0x71c7f8)+","+Read(handle,0x71c800)+","+Read(handle,0x71ca84)+","+Read(handle,0x71d284)+","+unchecked((int)Read(handle,0x71d298)));
            next+=50;long delay=next-watch.ElapsedMilliseconds;if(delay>0)Thread.Sleep((int)delay);
          }
        }
        Console.WriteLine("Saved "+output);
        Console.WriteLine("Counters are NOT ping or measured input latency. Share only this CSV, not existing crash logs.");
      }
      if(args.Length==0){Console.WriteLine("Press Enter to close.");Console.ReadLine();}return 0;
    } catch(Exception ex) {
      // Do not serialize arbitrary exception messages or local paths into reports.
      Console.WriteLine("Capture failed: "+ex.GetType().Name+". Check role/code, active game and permissions.");
      if(args.Length==0)Console.ReadLine();return 1;
    } finally {if(handle!=IntPtr.Zero)CloseHandle(handle);}
  }
  static string Prompt(string message){Console.Write(message);return (Console.ReadLine()??"").Trim();}
}
