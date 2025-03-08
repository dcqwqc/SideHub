using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Animation;

namespace SideHub
{
    public partial class MainWindow : Window
    {
        private static IntPtr _hookID = IntPtr.Zero;
        private static LowLevelMouseProc _proc = HookCallback;
        private bool isVisible = true;

        private readonly double hiddenPosition = -100; // Off-screen position
        private readonly double visiblePosition = 10;  // Target position

        public MainWindow()
        {
            InitializeComponent();
            this.Height = SystemParameters.PrimaryScreenHeight - 20;
            this.Top = (SystemParameters.PrimaryScreenHeight - this.Height) / 2;
            this.Left = hiddenPosition;
            this.Opacity = 0;
            this.Topmost = true; // Basic always-on-top

            // Set up global mouse hook
            _hookID = SetHook(_proc);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            UnhookWindowsHookEx(_hookID);
        }

        private static IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }
        }

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && wParam == (IntPtr)WM_XBUTTONDOWN)
            {
                MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                int button = (hookStruct.mouseData >> 16) & 0xFFFF;

                if (button == XBUTTON1) // MB5 Pressed
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MainWindow mainWindow = (MainWindow)Application.Current.MainWindow;
                        mainWindow.ToggleVisibility();
                    });
                }
            }

            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        public void ToggleVisibility()
        {
            isVisible = !isVisible;

            if (isVisible)
            {
                this.Visibility = Visibility.Visible;
                AnimateWindow(visiblePosition, 1.0, () => ForceAlwaysOnTop()); // Slide in & Fade in
            }
            else
            {
                AnimateWindow(hiddenPosition, 0.0, () => this.Visibility = Visibility.Hidden); // Slide out & Fade out
            }
        }

        private void AnimateWindow(double targetX, double targetOpacity, Action onComplete = null)
        {
            Duration animationDuration = TimeSpan.FromSeconds(0.3);

            // Slide Animation
            DoubleAnimation slideAnimation = new DoubleAnimation
            {
                To = targetX,
                Duration = animationDuration,
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut }
            };

            // Opacity Fade Animation
            DoubleAnimation fadeAnimation = new DoubleAnimation
            {
                To = targetOpacity,
                Duration = animationDuration,
                EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut }
            };

            fadeAnimation.Completed += (s, e) => onComplete?.Invoke();
            this.BeginAnimation(Window.LeftProperty, slideAnimation);
            this.BeginAnimation(Window.OpacityProperty, fadeAnimation);
        }

        private void ForceAlwaysOnTop()
        {
            IntPtr hWnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetWindowPos(hWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }

        private const int WH_MOUSE_LL = 14;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int XBUTTON1 = 1; // MB5

        private const int HWND_TOPMOST = -1;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_SHOWWINDOW = 0x0040;

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public int mouseData;
            public int flags;
            public int time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
    }
}
