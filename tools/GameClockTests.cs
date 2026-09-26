using System;
using System.Reflection;
class GameClockTests {
  static void Main(string[] args) {
    var a=Assembly.LoadFrom(args[0]);var t=a.GetType("ChangpogoLauncher.GameClockOverlay");
    var m=t.GetMethod("FormatTicks",BindingFlags.NonPublic|BindingFlags.Static);
    uint[] values={0,9,10,599,600,35999,36000,360000};
    string[] expected={"게임 00:00:00","게임 00:00:00","게임 00:00:01","게임 00:00:59","게임 00:01:00","게임 00:59:59","게임 01:00:00","게임 10:00:00"};
    for(int i=0;i<values.Length;i++)if((string)m.Invoke(null,new object[]{values[i]})!=expected[i])throw new Exception("Tick conversion failed");
    Console.WriteLine("PASS game tick formatting, rollover and reset-to-zero");
  }
}
