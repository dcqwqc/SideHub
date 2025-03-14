using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Shapes;
using System.IO;

using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Windows.Media.Control;
using WindowsMediaController;
using static WindowsMediaController.MediaManager;


namespace SideHub
{
    public partial class MainWindow : Window
    {
        private MediaManager mediaManager;
        
        private static IntPtr _hookID = IntPtr.Zero;
        private static LowLevelMouseProc _proc = HookCallback;
        private bool isVisible = true;

        private readonly double hiddenPosition = -100; // Off-screen position
        private readonly double visiblePosition = 10;  // Target position
        public bool isPlaying;
        private double targetX;
        private DispatcherTimer timer;
        private MediaManager.MediaSession mediaSession;



        public MainWindow()
        {
            // Initialize MediaManager
            InitializeComponent();
            InitializeMediaManager();
            mediaManager.OnAnySessionOpened += MediaManager_OnAnySessionOpened;
            mediaManager.OnAnySessionClosed += MediaManager_OnAnySessionClosed;
            mediaManager.StartAsync();

            this.Height = SystemParameters.PrimaryScreenHeight - 20;
            this.Top = (SystemParameters.PrimaryScreenHeight - this.Height) / 2;
            this.Left = hiddenPosition;
            this.Opacity = 0;
            this.Topmost = true; // Basic always-on-top

            // Set up global mouse hook
            _hookID = SetHook(_proc);

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(10);
            timer.Tick += (sender, e) =>
            {
                double currentX = MediaBlockTransform.X;
                if (Math.Abs(currentX - targetX) > 0.01) // Stoppt, wenn die Bewegung fast abgeschlossen ist
                {
                    double step = (targetX - currentX) * 0.01; // Schrittgröße
                    MediaBlockTransform.X = currentX + step;
                    Debug.WriteLine("reached");
                }
                else
                {
                    MediaBlockTransform.X = targetX; // Genau an die Zielposition setzen
                    timer.Stop();

                }
            };

            mediaTitleTextBlock.MouseEnter += (sender, e) =>
            {
                if (mediaTitleTextBlock.ActualWidth >= 134)
                {
                    targetX = 134 - mediaTitleTextBlock.ActualWidth;
                    timer.Start();
                    Debug.WriteLine(134 - mediaTitleTextBlock.ActualWidth);
                }
            };

            mediaTitleTextBlock.MouseLeave += (sender, e) =>
            {
                targetX = 0;
                timer.Start();
            };

        }






        private DispatcherTimer _timer;
        private MediaManager _mediaManager;
        private MediaSession _focusedSession;





        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

        }
        private void InitializeMediaManager()
        {
            mediaManager = new MediaManager();
            mediaManager.OnAnySessionOpened += MediaManager_OnAnySessionOpened;
            mediaManager.OnAnySessionClosed += MediaManager_OnAnySessionClosed;
            mediaManager.OnAnyMediaPropertyChanged += MediaManager_OnAnyMediaPropertyChanged;
            mediaManager.OnAnyPlaybackStateChanged += OnAnyPlaybackStateChanged;
            mediaManager.OnAnyTimelinePropertyChanged += MediaManager_OnAnyTimelinePropertyChanged;
            mediaManager.OnFocusedSessionChanged += MediaManager_OnFocusedSessionChanged;

            // Start the MediaManager asynchronously
            mediaManager.StartAsync();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(800);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private TimeSpan _illusionTime = TimeSpan.Zero;  // Variable to track the illusion time

        private void Timer_Tick(object sender, EventArgs e)
        {
            Debug.WriteLine(isPlaying);  // Debugging the _isPlaying flag

            if (_focusedSession == null || !isPlaying) return;  // Stop if not playing

            var timelineProperties = _focusedSession.ControlSession.GetTimelineProperties();
            var currentPosition = timelineProperties.Position;
            var duration = timelineProperties.EndTime;

            // Avoid division by zero
            if (duration.TotalSeconds == 0) return;

            // Check if currentPosition has changed and if the difference is >= 2 seconds
            if (currentPosition > _illusionTime && (currentPosition - _illusionTime).TotalSeconds >= 2)
            {
                // If currentPosition has advanced by 2 seconds or more, set illusion time to match it
                _illusionTime = currentPosition;
            }
            else
            {
                // If illusion time hasn't been reset by currentPosition, increment it
                if (_illusionTime < duration)
                {
                    _illusionTime = _illusionTime.Add(TimeSpan.FromSeconds(1));  // Add 1 second to illusion time
                }
                else
                {
                    _illusionTime = TimeSpan.Zero;  // Reset illusion time when reaching the end
                }
            }

            // Calculate progress percent based on illusion time
            double progressPercent = (_illusionTime.TotalSeconds / duration.TotalSeconds) * 100;

            // Update ProgressBar
            TimeProgressBar.Value = progressPercent;

            // Format the illusion time
            string formattedTime = $"{_illusionTime:mm\\:ss}";
            TimePlayed.Text = formattedTime;

            // Log the formatted illusion time for debugging
            Debug.WriteLine("TimerTick", formattedTime);
        }






        private void MediaManager_OnFocusedSessionChanged(MediaSession mediaSession)
        {
            _focusedSession = mediaSession;
        }








        private async void MediaManager_OnAnySessionOpened(MediaManager.MediaSession session)
        {
            // Get current media properties, such as title and thumbnail
            var mediaProperties = await session.ControlSession.TryGetMediaPropertiesAsync();

            // Get the title of the current media (e.g., song, video title)
            var mediaTitle = mediaProperties?.Title;
            var mediaThumbnailReference = mediaProperties?.Thumbnail;

            // Update the UI with the current title
            Dispatcher.Invoke(() =>
            {
                mediaTitleTextBlock.Text = mediaTitle ?? "No media playing";
                DisplayThumbnail(mediaThumbnailReference);
            });
            mediaSession = session;
            CheckPlaybackState();
        }
        private void MediaManager_OnAnyTimelinePropertyChanged(MediaSession mediaSession, GlobalSystemMediaTransportControlsSessionTimelineProperties timelineProperties)
        {


        }






        private async void OnAnyPlaybackStateChanged(MediaManager.MediaSession session, GlobalSystemMediaTransportControlsSessionPlaybackInfo args)
        {
            mediaSession = session;
            CheckPlaybackState();
            Console.WriteLine("playback state changed");
        }




        private async Task DisplayThumbnail(Windows.Storage.Streams.IRandomAccessStreamReference thumbnailReference)
        {
            if (thumbnailReference != null)
            {
                try
                {
                    // Open the random access stream
                    var randomStream = await thumbnailReference.OpenReadAsync();

                    // Convert the IRandomAccessStream to a .NET Stream
                    using (var stream = randomStream.AsStreamForRead())
                    {
                        // Create a BitmapImage and load the stream
                        var bitmapImage = new BitmapImage();
                        bitmapImage.BeginInit();
                        bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // Ensures the stream can be closed after loading
                        bitmapImage.StreamSource = stream;
                        bitmapImage.EndInit();
                        bitmapImage.Freeze(); // Makes the image cross-thread accessible

                        // Update the UI on the dispatcher thread
                        Dispatcher.Invoke(() =>
                        {
                            mediaThumbnailImage.Source = bitmapImage;  // Assuming you have an Image control named mediaThumbnailImage
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error displaying thumbnail: " + ex.Message);
                }
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    mediaThumbnailImage.Source = null;  // Or set a default image if no thumbnail
                });
            }
        }





        // Event handler when a media session is closed
        private void MediaManager_OnAnySessionClosed(MediaManager.MediaSession session)
        {

            Dispatcher.Invoke(() =>
            {
                mediaTitleTextBlock.Text = "No media playing"; // Reset UI when media is closed
            });

            mediaSession = session;
            CheckPlaybackState();
        }

        // Don't forget to stop the MediaManager when closing the window
        protected override void OnClosed(EventArgs e)
        {
            mediaManager?.ForceUpdate(); // Force update to stop the media manager
            base.OnClosed(e);
        }

        private async void MediaManager_OnAnyMediaPropertyChanged(MediaManager.MediaSession session, GlobalSystemMediaTransportControlsSessionMediaProperties args)
        {
            // Get the media title from the updated properties
            var mediaTitle = args?.Title;
            var mediaArtist = args?.Artist; // Get the artist name
            var mediaThumbnail = args?.Thumbnail; // Get the media thumbnail

            // Update the UI with the new title
            if (!string.IsNullOrEmpty(mediaTitle))
            {
                Dispatcher.Invoke(() =>
                {
                    mediaTitleTextBlock.Text = mediaTitle; // Update the media title
                });
            }

            // Update the artist if it exists
            if (!string.IsNullOrEmpty(mediaArtist))
            {
                Dispatcher.Invoke(() =>
                {
                    mediaArtistTextBlock.Text = mediaArtist; // Update the media artist
                });
            }

            // Update the thumbnail if it exists
            if (mediaThumbnail != null)
            {
                // Display the thumbnail in the UI
                await DisplayThumbnail(mediaThumbnail); // Call the DisplayThumbnail method to update the image
            }



            mediaSession = session;
            CheckPlaybackState();
        }










        private System.Windows.Shapes.Path playIcon;
        private System.Windows.Shapes.Path pauseIcon;


        private void PlayButton_Loaded(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            if (button != null && button.Template != null)
            {
                playIcon = button.Template.FindName("playicon", button) as System.Windows.Shapes.Path;
                pauseIcon = button.Template.FindName("pauseicon", button) as System.Windows.Shapes.Path;

            }
        }



        public void CheckPlaybackState()
        {
            if (mediaSession != null)
            {
                var playbackInfo = mediaSession.ControlSession.GetPlaybackInfo();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
                    {
                        Debug.WriteLine("Media is playing");
                        if (pauseIcon != null) pauseIcon.Visibility = Visibility.Visible;
                        if (playIcon != null) playIcon.Visibility = Visibility.Hidden;
                        isPlaying = true;
                    }
                    else if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused)
                    {
                        Debug.WriteLine("Media is paused");
                        if (playIcon != null) playIcon.Visibility = Visibility.Visible;
                        if (pauseIcon != null) pauseIcon.Visibility = Visibility.Hidden;
                        isPlaying = false;
                    }
                    else
                    {
                        Debug.WriteLine("No media is currently playing or media is stopped");
                        if (playIcon != null) playIcon.Visibility = Visibility.Visible;
                        if (pauseIcon != null) pauseIcon.Visibility = Visibility.Hidden;
                        isPlaying = false;
                    }
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Debug.WriteLine("No media session available");
                    if (playIcon != null) playIcon.Visibility = Visibility.Visible;
                    if (pauseIcon != null) pauseIcon.Visibility = Visibility.Hidden;
                    isPlaying = false;
                });
            }
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

                if (button == XBUTTON2) // MB4 Pressed
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
        private const int XBUTTON2 = 2; // MB4

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

        // 🔹 Play/Pause Media Key Functionality
        [DllImport("user32.dll", SetLastError = true)]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3; // Virtual key code for Play/Pause/skip/prev
        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;







        private void PlayClick(object sender, RoutedEventArgs e)
        {
            // Simulate pressing the Play/Pause media key
            keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 0, 0);
            keybd_event(VK_MEDIA_PLAY_PAUSE, 0, 2, 0); // Key release
        }
        private void SkipClick(object sender, RoutedEventArgs e)
        {
            // Simulate pressing the Media Next Track key (VK_MEDIA_NEXT_TRACK)
            keybd_event(VK_MEDIA_NEXT_TRACK, 0, 0, 0);
            keybd_event(VK_MEDIA_NEXT_TRACK, 0, 2, 0); // Key release
        }

        private void PrevClick(object sender, RoutedEventArgs e)
        {
            // Simulate pressing the Media Previous Track key (VK_MEDIA_PREV_TRACK)
            keybd_event(VK_MEDIA_PREV_TRACK, 0, 0, 0);
            keybd_event(VK_MEDIA_PREV_TRACK, 0, 2, 0); // Key release
        }

        private void SessionSkipClick(object sender, RoutedEventArgs e)
        {
            SwitchToNextSession();
        }


        private void SwitchToNextSession()
        {
            //help
        }
    }
}