using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class Launcher
{
    [STAThread]
    private static int Main(string[] args)
    {
        string title = "Guardian of the Globe";
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        string script = Path.Combine(dir, "app", "app.ps1");
        if (!File.Exists(script))
        {
            MessageBox.Show("app\\app.ps1 was not found next to this program.", title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        string win = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
        string ps = Path.Combine(win, @"System32\WindowsPowerShell\v1.0\powershell.exe");
        ProcessStartInfo psi = new ProcessStartInfo(ps, "-STA -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\"");
        psi.WorkingDirectory = dir;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true;
        psi.RedirectStandardError = true;
        StringBuilder err = new StringBuilder();
        object gate = new object();
        Process p = new Process();
        p.StartInfo = psi;
        p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            lock (gate)
            {
                err.AppendLine(e.Data);
                if (err.Length > 6000) err.Remove(0, err.Length - 4000);
            }
        };
        try { p.Start(); }
        catch (Exception ex)
        {
            MessageBox.Show("Could not start Windows PowerShell:\n" + ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        p.BeginErrorReadLine();
        while (!p.HasExited) Thread.Sleep(250);
        Thread.Sleep(300);
        if (p.ExitCode != 0)
        {
            string text;
            lock (gate) { text = err.ToString().Trim(); }
            if (text.Length > 1500) text = text.Substring(text.Length - 1500);
            MessageBox.Show("The app stopped with an error.\n\n" + text, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        return p.ExitCode;
    }
}