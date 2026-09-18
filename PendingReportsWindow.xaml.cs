using System.Windows;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class PendingReportsWindow : Window
    {
        public PendingReportsWindow()
        {
            InitializeComponent();
            SourceInitialized += (s, e) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");
            LoadPending();
        }

        private void LoadPending()
        {
            dgPending.ItemsSource = DatabaseHelper.GetPendingPatients();
        }

        private void btnEnterResults_Click(object sender, RoutedEventArgs e)
        {
            if (dgPending.SelectedItem is Patient selected)
            {
                var win = new EnterResultsWindow(selected.Id);
                win.ShowDialog();
                LoadPending(); // refresh - the completed patient will disappear from this list
            }
            else
            {
                MessageBox.Show("Please select a pending patient first.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnRefresh_Click(object sender, RoutedEventArgs e) => LoadPending();

        private void btnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}