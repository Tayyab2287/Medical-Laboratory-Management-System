using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using LabReportApp.Config;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class HistoryWindow : Window
    {
        public HistoryWindow()
        {
            InitializeComponent();
            SourceInitialized += (s, e) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");
            dgHistory.ItemsSource = DatabaseHelper.GetAllPatients();
        }

        private void btnOpenPdf_Click(object sender, RoutedEventArgs e)
        {
            if (dgHistory.SelectedItem is not Patient selected)
            {
                MessageBox.Show("Please select a report first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppSettings.LocalReportsFolder, $"{selected.SerialNumber}.pdf");

            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("The PDF file for this report was not found locally.\nIt may have been moved or deleted.",
                    "File Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}