using System.IO; // Import the System.IO namespace for Path.Combine
using System.Windows;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WPF = System.Windows;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;
using System.Windows.Interop;
using Windows.Foundation.Metadata;
using System.Diagnostics;
using System.Configuration;
namespace SideHub
{
    public partial class MainWindow : Window
    {
        // Variable to hold the selected folder path
        private string selectedFolder;
        private string userIP;

        private void PhoneTab_checked(object sender, RoutedEventArgs e)
        {
            PhoneTab.Visibility = Visibility.Visible;
            Content2.Visibility = Visibility.Collapsed;
            Content3.Visibility = Visibility.Collapsed;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new WinForms.FolderBrowserDialog())
            {
                dialog.Description = "Select platform-tools folder";
                dialog.InitialDirectory = @"C:\";

                WinForms.DialogResult result = dialog.ShowDialog();
                if (result == WinForms.DialogResult.OK)
                {
                    selectedFolder = dialog.SelectedPath; // Save the folder path
                }
            }
        }

        private void StartPhone_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ensure the folder path is selected before proceeding
                if (string.IsNullOrEmpty(selectedFolder))
                {
                    WPF.MessageBox.Show("Please select the platform-tools folder.");
                    return;
                }

                // Get the IP address from the TextBox (UserIP)
                userIP = UserIP.Text;

                // Set up the command to execute
                string command = Path.Combine(selectedFolder, "adb");
                string arguments = $"tcpip 5555 && adb connect {userIP}:5555 && start .\\scrcpy";

                // Escape the paths with spaces
                string workingDirectory = selectedFolder;
                string adbCommand = $"\"{command}\" {arguments}";

                // Start the process
                System.Diagnostics.ProcessStartInfo processStartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/K cd /d \"{workingDirectory}\" && {adbCommand}",  // /K keeps the window open
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                System.Diagnostics.Process process = System.Diagnostics.Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Error: {ex.Message}");
            }
        }
    }
}
