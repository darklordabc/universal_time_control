using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Overlay.NET.Common;
using Overlay.NET.Wpf;
using Process.NET.Windows;
using OverlayWindow = Overlay.NET.Wpf.OverlayWindow;
using System.Globalization;
using System.Linq;
using System.Windows.Documents;
using System.Windows.Media.Media3D;
using Process.NET;
using Process.NET.Memory;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using RestSharp;
using GameOverlay.Drawing;
using System.Windows.Media.Imaging;
using RGiesecke.DllExport;

namespace GameOverlayPrototype
{
    static class Program
    {
		private static Overlay overlay1;
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        [DllExport("main", CallingConvention = CallingConvention.Cdecl)]
        static void Main()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            var f = new SystemProcessHookForm();
            f.WindowEvent += (sender, data) => GiveOverlay(data);
            System.Windows.Forms.Application.Run(new MyCustomApplicationContext());
        }
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
        
		static async void GiveOverlay(string data)
        {
			var dataSplit = data.Split(',');
			Console.WriteLine(data);
			var client = new RestClient("https://api-v3.igdb.com/games/");
			client.AddDefaultHeader("user-key", "9fac2faa6ff1f54909f1b07a0ae44ac0");
			var request = new RestRequest().AddParameter("text/plain", String.Format("search \"{0}\";", dataSplit[1]), ParameterType.RequestBody);
			request.AddHeader("Content-Type", "text/plain");
			var response = client.Post(request);
			string responseString = response.Content;
			Console.WriteLine(responseString);
			if (responseString != "[]")
            {
				overlay1 = new Overlay(dataSplit[1]);
            }
		}
    }

    public class MyCustomApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;
        public MyCustomApplicationContext()
        {
            // Initialize Tray Icon
            trayIcon = new NotifyIcon()
            {
                Icon = Properties.Resources.AppIcon,
                ContextMenuStrip = new ContextMenuStrip(),
                Visible = true
            };
            trayIcon.ContextMenuStrip.Items.Add("Setup Overlay", Properties.Resources.Exit, SetupOverlay);
            trayIcon.ContextMenuStrip.Items.Add("Exit", Properties.Resources.Exit, Exit);
		}

        void Exit(object sender, EventArgs e)
        {
            // Hide tray icon, otherwise it will remain shown until user mouses over it
            trayIcon.Visible = false;

            System.Windows.Forms.Application.Exit();
        }

        void SetupOverlay(object sender, EventArgs e)
        {
            Form1 overlayForm = new Form1();
            overlayForm.Activate();
            overlayForm.GoFullscreen(true);
            overlayForm.ShowDialog();
        }
    }
    public class OverlaySettings
    {
        // 60 frames/sec roughly
        public int UpdateRate { get; set; }

        public string Author { get; set; }
        public string Description { get; set; }
        public string Identifier { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
    }
    public class Overlay {
        /// <summary>
        ///     The overlay
        /// </summary>
        private static OverlayPlugin _overlay;

        /// <summary>
        ///     The process sharp
        /// </summary>
        private static ProcessSharp _processSharp;

        /// <summary>
        ///     The work
        /// </summary>
        private static bool _work;

        /// <summary>
        ///     Starts the demo.
        /// </summary>
        public Overlay(string processName = "")
        {
            var processes = System.Diagnostics.Process.GetProcesses();
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            foreach (System.Diagnostics.Process process1 in processes)
            {
                if(process1.MainWindowTitle.Contains(processName))
                {
                    process = process1;
                    break;
                }
            }
            if (process == null)
            {
                Log.Warn($"No process by the name of {processName} was found.");
                return;
            }

            _processSharp = new ProcessSharp(process, MemoryType.Remote);
            _overlay = new OverlayPlugin();

            var wpfOverlay = (OverlayPlugin)_overlay;

            // This is done to focus on the fact the Init method
            // is overriden in the wpf overlay demo in order to set the
            // wpf overlay window instance
            wpfOverlay.Initialize(_processSharp.WindowFactory.MainWindow);
            wpfOverlay.Enable();

            _work = true;

            // Log some info about the overlay.
            Log.Debug("Starting update loop (open the process you specified and drag around)");
            Log.Debug("Update rate: " + wpfOverlay.Settings.Current.UpdateRate.Milliseconds());

            var info = wpfOverlay.Settings.Current;

            Log.Debug($"Author: {info.Author}");
            Log.Debug($"Description: {info.Description}");
            Log.Debug($"Name: {info.Name}");
            Log.Debug($"Identifier: {info.Identifier}");
            Log.Debug($"Version: {info.Version}");

            Log.Info("Note: Settings are saved to a settings folder in your main app folder.");

            Log.Info("Give your window focus to enable the overlay (and unfocus to disable..)");

            Log.Info("Close the console to end the demo.");

            wpfOverlay.OverlayWindow.Draw += OnDraw;

            // Do work
            while (_work)
            {
                _overlay.Update();
            }

            Log.Debug("Demo complete.");
        }

        private static void OnDraw(object sender, System.Windows.Media.DrawingContext context)
        {

        }
    }

    public class OverlayPlugin : WpfOverlayPlugin
    {
        public ISettings<OverlaySettings> Settings { get; } = new SerializableSettings<OverlaySettings>();
        // Used to limit update rates via timestamps 
        // This way we can avoid thread issues with wanting to delay updates
        private readonly TickEngine _tickEngine = new TickEngine();
        private System.Windows.Shapes.Ellipse _ellipse;

        private bool _isDisposed;

        private bool _isSetup;

        // Shapes used in the demo
        private System.Windows.Controls.Image overlay1;

        public override void Enable()
        {
            _tickEngine.IsTicking = true;
            base.Enable();
        }

        public override void Disable()
        {
            _tickEngine.IsTicking = false;
            base.Disable();
        }

        public override void Initialize(IWindow targetWindow)
        {
            // Set target window by calling the base method
            base.Initialize(targetWindow);

            OverlayWindow = new OverlayWindow(targetWindow);

            // For demo, show how to use settings
            var current = Settings.Current;
            var type = GetType();

            current.UpdateRate = 1000 / 60;
            current.Author = GetAuthor(type);
            current.Description = GetDescription(type);
            current.Identifier = GetIdentifier(type);
            current.Name = GetName(type);
            current.Version = GetVersion(type);

            // File is made from above info
            Settings.Save();
            Settings.Load();

            // Set up update interval and register events for the tick engine.
            _tickEngine.Interval = Settings.Current.UpdateRate.Milliseconds();
            _tickEngine.PreTick += OnPreTick;
            _tickEngine.Tick += OnTick;
        }

        private void OnTick(object sender, EventArgs eventArgs)
        {
            // This will only be true if the target window is active
            // (or very recently has been, depends on your update rate)
            if (OverlayWindow.IsVisible)
            {
                OverlayWindow.Update();
            }
        }

        private void OnPreTick(object sender, EventArgs eventArgs)
        {
            // Only want to set them up once.
            if (!_isSetup)
            {
                SetUp();
                _isSetup = true;
            }

            var activated = TargetWindow.IsActivated;
            var visible = OverlayWindow.IsVisible;

            // Ensure window is shown or hidden correctly prior to updating
            if (!activated && visible)
            {
                OverlayWindow.Hide();
            }

            else if (activated && !visible)
            {
                OverlayWindow.Show();
            }
        }

        public override void Update() => _tickEngine.Pulse();

        // Clear objects
        public override void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            if (IsEnabled)
            {
                Disable();
            }

            OverlayWindow?.Hide();
            OverlayWindow?.Close();
            OverlayWindow = null;
            _tickEngine.Stop();
            Settings.Save();

            base.Dispose();
            _isDisposed = true;
        }

        ~OverlayPlugin()
        {
            Dispose();
        }

        private void SetUp()
        {
            overlay1 = new System.Windows.Controls.Image();
            BitmapImage bi3 = new BitmapImage();
            bi3.BeginInit();
            bi3.UriSource = new Uri(System.IO.Path.GetDirectoryName(System.Windows.Forms.Application.StartupPath) + "\\panel1.bmp", UriKind.RelativeOrAbsolute);
            bi3.EndInit();
            overlay1.Stretch = Stretch.Fill;
            overlay1.Source = bi3;
            OverlayWindow.Add(overlay1);
        }
    }
}
