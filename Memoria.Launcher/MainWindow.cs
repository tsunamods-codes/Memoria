﻿using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace Memoria.Launcher
{
    public partial class MainWindow : Window, IComponentConnector
    {
        public MainWindow()
        {
            InitializeComponent();
            TryLoadImage();
            TryShowDonation();

            PlayButton.GameSettings = GameSettings;
        }

        private void TryLoadImage()
        {
            try
            {
                String backgroundImagePath = ConfigurationManager.AppSettings["backgroundImagePath"];
                if (String.IsNullOrEmpty(backgroundImagePath) || !File.Exists(backgroundImagePath))
                    return;

                String path = Path.GetFullPath(backgroundImagePath);

                ImageSource imageSource = new BitmapImage(new Uri(path, UriKind.Absolute));
                Launcher.Source = imageSource;
            }
            catch
            {
            }
        }

        private void TryShowDonation()
        {
            try
            {
                String showDonationControl = ConfigurationManager.AppSettings[nameof(showDonationControl)];
                if (showDonationControl == "true")
                {
                    Donation.Visibility = Visibility.Visible;
                    Links.Visibility = Visibility.Visible;
                }
            }
            catch
            {

            }
        }

        [DllImport("user32.dll")]
        public static extern Int32 SendMessage(IntPtr hWnd, Int32 msg, Int32 wParam, Int32 lParam);

        [DllImport("user32.dll")]
        public static extern Boolean ReleaseCapture();

        private void Launcher_MouseDown(Object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
                return;

            Point position = Mouse.GetPosition(Launcher);
            if (position.Y > 50.0)
                return;

            ReleaseCapture();
            SendMessage(new WindowInteropHelper(this).Handle, 161, 2, 0);
        }

        private void OnHyperlinkClick(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(e.Uri.AbsoluteUri);
            e.Handled = true;
        }
    }
}