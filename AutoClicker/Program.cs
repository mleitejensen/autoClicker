using System;
using System.Windows.Forms;

namespace AutoClicker;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        NativeMethods.TimeBeginPeriod(1);
        try
        {
            Application.Run(new MainForm());
            return 0;
        }
        finally
        {
            NativeMethods.TimeEndPeriod(1);
        }
    }
}
