using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;

namespace GameOverlayPrototype
{
    public static class Interop
    {
        public enum ShellEvents : int
        {
            HSHELL_WINDOWCREATED = 1,
            HSHELL_WINDOWDESTROYED = 2,
            HSHELL_ACTIVATESHELLWINDOW = 3,
            HSHELL_WINDOWACTIVATED = 4,
            HSHELL_GETMINRECT = 5,
            HSHELL_REDRAW = 6,
            HSHELL_TASKMAN = 7,
            HSHELL_LANGUAGE = 8,
            HSHELL_ACCESSIBILITYSTATE = 11,
            HSHELL_APPCOMMAND = 12
        }
        [DllImport("user32.dll", EntryPoint = "RegisterWindowMessageA", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern int RegisterWindowMessage(string lpString);
        [DllImport("user32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern int DeregisterShellHookWindow(IntPtr hWnd);
        [DllImport("user32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern int RegisterShellHookWindow(IntPtr hWnd);
        [DllImport("user32", EntryPoint = "GetWindowTextA", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern int GetWindowText(IntPtr hwnd, System.Text.StringBuilder lpString, int cch);
        [DllImport("user32", EntryPoint = "GetWindowTextLengthA", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        public static extern int GetWindowTextLength(IntPtr hwnd);
    }
    public class SystemProcessHookForm : Form
    {
        private readonly int msgNotify;
        public delegate void EventHandler(object sender, string data);
        public event EventHandler WindowEvent;
        protected virtual void OnWindowEvent(string data)
        {
            var handler = WindowEvent;
            if (handler != null)
            {
                handler(this, data);
            }
        }

        public SystemProcessHookForm()
        {
            // Hook on to the shell
            msgNotify = Interop.RegisterWindowMessage("SHELLHOOK");
            Interop.RegisterShellHookWindow(this.Handle);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == msgNotify)
            {
                // Receive shell messages
                switch ((Interop.ShellEvents)m.WParam.ToInt32())
                {
                    case Interop.ShellEvents.HSHELL_WINDOWCREATED:
                    case Interop.ShellEvents.HSHELL_WINDOWDESTROYED:
                    case Interop.ShellEvents.HSHELL_WINDOWACTIVATED:
                        string wName = GetWindowName(m.LParam);
                        var action = (Interop.ShellEvents)m.WParam.ToInt32();
                        if(action.ToString() == "HSHELL_WINDOWCREATED")
                        {
                            OnWindowEvent(string.Format("{0},{1}", m.LParam, wName));
                        }
                        break;
                }
            }
            base.WndProc(ref m);
        }

        private string GetWindowName(IntPtr hwnd)
        {
            StringBuilder sb = new StringBuilder();
            int longi = Interop.GetWindowTextLength(hwnd) + 1;
            sb.Capacity = longi;
            Interop.GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        protected override void Dispose(bool disposing)
        {
            try { Interop.DeregisterShellHookWindow(this.Handle); }
            catch { }
            base.Dispose(disposing);
        }
    }
    public partial class Form1 : Form
    {
        private TextBox txtBox;
        private OpenFileDialog openFile;
        private Button openFileButton;
        private Button saveButton;
        private ListView listView;
        private List<Image> imageListReal;
        private ImageList imageList;
        private int numImages;
        private Label label;
        private Panel panel1;
        private Rectangle RcDraw;
        private Image newImage;

        public Form1()
        {
            string filePath = String.Empty;
            string fileContent = String.Empty;
            numImages = 0;

            openFileButton = new Button();
            openFileButton.Click += new EventHandler(BrowseButton_Click);

            saveButton = new Button();
            saveButton.Text = "Save and Exit";
            saveButton.Location = new Point(Screen.PrimaryScreen.Bounds.Width - 400, 20);
            saveButton.Size = new Size(saveButton.Size.Height * 5, saveButton.Size.Height);
            saveButton.Click += new EventHandler(saveButton_Click);

            txtBox = new TextBox();
            listView = new ListView();
            imageList = new ImageList();
            label = new Label();
            panel1 = new Panel();
            RcDraw = new Rectangle();
            imageListReal = new List<Image>();

            panel1.BackColor = Color.White;
            panel1.BorderStyle = BorderStyle.Fixed3D;
            panel1.Location = new Point(321, 47);
            panel1.Size = new Size(Screen.PrimaryScreen.Bounds.Width - 349, 1020);
            panel1.Click += panel1_Click;
            panel1.Paint += DrawImageRect;

            txtBox.Text = String.Empty;
            txtBox.ReadOnly = true;

            imageList.ImageSize = new Size(128, 128);
            imageList.TransparentColor = Color.White;
            imageList.ColorDepth = ColorDepth.Depth32Bit;

            openFileButton.Text = "Add Image";
            openFileButton.Name = "BrowseButton";

            label.Text = String.Empty;
            label.BorderStyle = BorderStyle.Fixed3D;
            label.AutoSize = false;
            label.Height = 2;
            label.Location = new Point(305, 1067);
            label.Width = Screen.PrimaryScreen.Bounds.Width - 329;

            openFileButton.Location = new Point(20, 20);
            txtBox.Location = new Point(100, 20);
            listView.Location = new Point(20, 47);
            listView.Size = new Size(300, 1021);
            listView.MultiSelect = false;

            this.Controls.Add(txtBox);
            this.Controls.Add(openFileButton);
            this.Controls.Add(listView);
            this.Controls.Add(label);
            this.Controls.Add(panel1);
            this.Controls.Add(saveButton);

            InitializeComponent();
        }
        public void GoFullscreen(bool fullscreen)
        {
            if (fullscreen)
            {
                this.WindowState = FormWindowState.Normal;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
                this.Bounds = Screen.PrimaryScreen.Bounds;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
                this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            }
        }
        private void MoveDialogWhenOpened(String windowCaption, Point location)
        {
            Object[] argument = new Object[] { windowCaption, location };

            BackgroundWorker backgroundWorker = new BackgroundWorker();
            backgroundWorker.DoWork += new DoWorkEventHandler(MoveDialogThread);
            backgroundWorker.RunWorkerAsync(argument);
        }
        private void MoveDialogThread(Object sender, DoWorkEventArgs e)
        {
            const String DialogWindowClass = "#32770";

            String windowCaption = (String)(((Object[])e.Argument)[0]);
            Point location = (Point)(((Object[])e.Argument)[1]);

            // try for a maximum of 4 seconds (sleepTime * maxAttempts)
            Int32 sleepTime = 10; // milliseconds
            Int32 maxAttempts = 400;

            for (Int32 i = 0; i < maxAttempts; ++i)
            {
                // find the handle to the dialog
                IntPtr handle = Win32Api.FindWindow(DialogWindowClass, windowCaption);

                // if the handle was found and the dialog is visible
                if ((Int32)handle > 0 && Win32Api.IsWindowVisible(handle) > 0)
                {
                    // move it
                    Win32Api.SetWindowPos(handle, (IntPtr)0, location.X, location.Y,
                               0, 0, Win32Api.SWP_NOSIZE | Win32Api.SWP_NOZORDER);
                    break;
                }

                // if not found wait a brief sec and try again
                Thread.Sleep(sleepTime);
            }
        }
        private void saveButton_Click(object sender, EventArgs e)
        {
            Bitmap bmp = new Bitmap(newImage);
            bmp.Save(Path.GetDirectoryName(Application.StartupPath) + "\\panel1.bmp");
            this.Close();
        }
        private void BrowseButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog1 = new OpenFileDialog
            {
                InitialDirectory = "%USERPROFILE%",
                Title = "Browse Text Files",

                CheckFileExists = true,
                CheckPathExists = true,

                DefaultExt = "txt",
                Filter = "Image Files(*.BMP;*.JPG;*;*.JPEG;*.GIF)|*.BMP;*.JPG;*.JPEG;*.PNG",
                FilterIndex = 2,
                RestoreDirectory = true
            };

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                txtBox.Text = openFileDialog1.FileName;
                Image addImage = Image.FromFile(openFileDialog1.FileName);
                imageList.Images.Add(numImages.ToString(), addImage);
                imageListReal.Add(addImage);

                listView.LargeImageList = imageList;
                for (int j = numImages; j < this.imageList.Images.Count; j++)
                {
                    ListViewItem item = new ListViewItem();
                    item.ImageIndex = j;
                    this.listView.Items.Add(item);
                }

                listView.Refresh();
                numImages++;
            }
        }
        private void panel1_Click(object sender, EventArgs e)
        {
            if (listView.SelectedItems.Count > 0)
            {
                newImage = imageListReal[listView.SelectedItems[0].Index];
                RcDraw.X = panel1.PointToClient(Cursor.Position).X;
                RcDraw.Y = panel1.PointToClient(Cursor.Position).Y;
                RcDraw.Width = newImage.Width;
                RcDraw.Height = newImage.Height;
                panel1.Invalidate(RcDraw);
            }
        }
        private void DrawImageRect(object sender, System.Windows.Forms.PaintEventArgs e)
        {
            if (listView.SelectedItems.Count > 0)
            {
                // Create rectangle for displaying image.
                Rectangle destRect = new Rectangle(RcDraw.X, RcDraw.Y, newImage.Width, newImage.Height);

                // Make background transparent
                Bitmap myBitmap = new Bitmap(newImage);
                myBitmap.MakeTransparent();

                // Draw image to screen.
                e.Graphics.DrawImage(myBitmap, destRect);
            }
        }

        public class Win32Api
        {
            public const Int32 SWP_NOSIZE = 0x1;
            public const Int32 SWP_NOZORDER = 0x4;

            [DllImport("user32")]
            public static extern IntPtr FindWindow(String lpClassName,
                                String lpWindowName);

            [DllImport("user32")]
            public static extern Int32 IsWindowVisible(IntPtr hwnd);

            [DllImport("user32")]
            public static extern Int32 SetWindowPos(IntPtr hwnd,
                                IntPtr hwndInsertAfter,
                                Int32 x,
                                Int32 y,
                                Int32 cx,
                                Int32 cy,
                                Int32 wFlags);
        }
    }
}
