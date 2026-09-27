using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
class LauncherLayoutTests {
  static void Fonts(Control c,float scale,System.Collections.Generic.List<Action> changes) {
    var font=new Font(c.Font.FontFamily,c.Font.Size*scale,c.Font.Style);
    changes.Add(()=>c.Font=font);foreach(Control child in c.Controls)Fonts(child,scale,changes);
  }
  static void Check(Control parent) {
    for(int i=0;i<parent.Controls.Count;i++) {
      var c=parent.Controls[i];
      if(c.Right>parent.ClientSize.Width+2)throw new Exception("Horizontal overflow: "+c.Text);
      for(int j=i+1;j<parent.Controls.Count;j++)if(c.Bounds.IntersectsWith(parent.Controls[j].Bounds))throw new Exception("Overlap: "+c.Text+" / "+parent.Controls[j].Text);
      Check(c);
    }
  }
  static void Render(Control c,Graphics g,Point origin) {
    using(var b=new Bitmap(Math.Max(1,c.Width),Math.Max(1,c.Height))) {
      var handle=c.Handle;c.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size));g.DrawImageUnscaled(b,origin);
    }
    foreach(Control child in c.Controls)Render(child,g,new Point(origin.X+child.Left,origin.Y+child.Top));
  }
  [STAThread] static void Main(string[] args) {
    Application.EnableVisualStyles();
    var a=Assembly.LoadFrom(args[0]);
    foreach(float scale in new[]{1f,1.5f,2f}) {
      using(var f=(Form)Activator.CreateInstance(a.GetType("ChangpogoLauncher.LauncherForm"),true)) {
        f.CreateControl();f.PerformLayout();f.Scale(new SizeF(scale,scale));
        var changes=new System.Collections.Generic.List<Action>();Fonts(f,scale,changes);foreach(var change in changes)change();f.PerformLayout();
        Check(f.Controls[0]);
        var panel=f.Controls[0];
        using(var b=new Bitmap(panel.Width,panel.Height)) {using(var g=Graphics.FromImage(b)){g.Clear(f.BackColor);Render(panel,g,Point.Empty);}b.Save(args[1]+scale.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+".png");}
      }
    }
    Console.WriteLine("PASS layout 100/150/200%: no horizontal overflow or sibling overlaps");
  }
}
