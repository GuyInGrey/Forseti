using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ForsetiFramework
{
    public class Program
    {
        public static NotifyIcon Icon;

        [STAThread]
        static void Main()
        {
            Task.Run(() =>
            {
                Icon = new NotifyIcon()
                {
                    Text = "Forseti",
                    Icon = new Icon(Config.Path + "ForsetiIcon.ico"),
                };
                Icon.Visible = true;
                Icon.Click += Icon_Click;
                Application.Run();
            });

            ShowWindow(GetConsoleWindow(), SW_HIDE);

            BotManager.Instantiate();
            BotManager.Start().GetAwaiter().GetResult();
        }

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        public static bool Showing;

        private static void Icon_Click(object sender, EventArgs e)
        {
            var handle = GetConsoleWindow();
            if (Showing)
            {
                ShowWindow(handle, SW_HIDE);
            }
            else
            {
                ShowWindow(handle, SW_SHOW);
            }
            Showing = !Showing;
        }
    }
}
