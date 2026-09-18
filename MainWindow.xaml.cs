using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using LabReportApp.Config;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class MainWindow : Window
    {
        private readonly ObservableCollection<TestResultItem> _results = new();

        // Master data used for suggestions. AllCategories is bound directly from XAML.
        public ObservableCollection<string> AllCategories { get; set; } = new();
        private System.Collections.Generic.List<TestDefinition> _testDefinitions = new();

        public MainWindow()
        {
            InitializeComponent();
            dgResults.ItemsSource = _results;
            DataContext = this; // not required for the AncestorType=Window bindings, but harmless

            SourceInitialized += (s, e) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");

            _results.CollectionChanged += Results_CollectionChanged;

            LoadMasterData();
            UpdateThemeButtonLabel();

            for (int i = 0; i < 5; i++)
                _results.Add(new TestResultItem());
        }

        // ---------------- LIGHT / DARK THEME TOGGLE ----------------

        private void btnToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.ToggleTheme();
            TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");
            UpdateThemeButtonLabel();
        }

        private void UpdateThemeButtonLabel()
        {
            btnToggleTheme.Content = ThemeManager.CurrentTheme == "Dark" ? "☀️ Light Mode" : "🌙 Dark Mode";
        }

        /// <summary>
        /// (Re)loads the predefined test list and the distinct category list from the database.
        /// Call this again after the "Manage Tests" window is closed so new tests show up immediately.
        /// </summary>
        private void LoadMasterData()
        {
            _testDefinitions = DatabaseHelper.GetAllTestDefinitions();

            AllCategories.Clear();
            foreach (var category in _testDefinitions.Select(t => t.CategoryHeading).Distinct().OrderBy(c => c))
                AllCategories.Add(category);
        }

        private void btnManageTests_Click(object sender, RoutedEventArgs e)
        {
            var win = new TestDefinitionsWindow();
            win.ShowDialog();
            LoadMasterData(); // refresh suggestions in case tests were added/edited/deleted
        }

        // ---------------- RUNNING TOTAL (sum of each row's Price) ----------------

        private void Results_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (TestResultItem item in e.NewItems)
                    item.PropertyChanged += Row_PropertyChanged;

            if (e.OldItems != null)
                foreach (TestResultItem item in e.OldItems)
                    item.PropertyChanged -= Row_PropertyChanged;

            RecalculateTotal();
        }

        private void Row_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TestResultItem.Price) || e.PropertyName == nameof(TestResultItem.Discount))
                RecalculateTotal();
        }

        private void txtOverallDiscount_TextChanged(object sender, TextChangedEventArgs e)
        {
            RecalculateTotal();
        }

        /// <summary>
        /// Recomputes Subtotal (sum of each test's Price after its own per-test Discount %),
        /// then applies the Overall Discount % on top of that subtotal to get the final total.
        /// </summary>
        private void RecalculateTotal()
        {
            // These controls are created in XAML in a specific order. txtOverallDiscount's
            // TextChanged event can fire the moment its Text="0" is set during XAML loading -
            // at that exact instant, txtDiscountAmount/txtTotalAmount (declared further down
            // in the XAML) may not exist yet. Bail out safely if that's the case.
            if (txtSubtotalAmount == null || txtDiscountAmount == null || txtTotalAmount == null || txtOverallDiscount == null)
                return;

            decimal subtotal = _results.Sum(r => r.NetPrice);

            decimal.TryParse(txtOverallDiscount.Text, out decimal overallDiscountPercent);
            if (overallDiscountPercent < 0) overallDiscountPercent = 0;
            if (overallDiscountPercent > 100) overallDiscountPercent = 100;

            decimal discountAmount = Math.Round(subtotal * overallDiscountPercent / 100m, 2);
            decimal grandTotal = subtotal - discountAmount;

            txtSubtotalAmount.Text = $"Rs. {subtotal:N0}";
            txtDiscountAmount.Text = $"Discount ({overallDiscountPercent:0.##}%): -Rs. {discountAmount:N0}";
            txtTotalAmount.Text = $"Total Payable: Rs. {grandTotal:N0}";
        }

        // ---------------- TEST NAME AUTOCOMPLETE ----------------

        private void TestNameCombo_Loaded(object sender, RoutedEventArgs e)
        {
            var combo = (ComboBox)sender;
            combo.ItemsSource = _testDefinitions.Select(t => t.TestName).ToList();
        }

        private void TestNameCombo_KeyUp(object sender, KeyEventArgs e)
        {
            // Don't refilter on navigation keys, let the ComboBox handle them normally.
            if (e.Key == Key.Enter || e.Key == Key.Tab || e.Key == Key.Escape ||
                e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Left || e.Key == Key.Right)
                return;

            var combo = (ComboBox)sender;
            string typed = combo.Text ?? string.Empty;

            var matches = string.IsNullOrEmpty(typed)
                ? _testDefinitions.Select(t => t.TestName).ToList()
                : _testDefinitions
                    .Where(t => t.TestName.IndexOf(typed, StringComparison.OrdinalIgnoreCase) >= 0)
                    .Select(t => t.TestName)
                    .ToList();

            combo.ItemsSource = matches;
            combo.IsDropDownOpen = matches.Count > 0;
        }

        private void TestNameCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combo = (ComboBox)sender;

            if (combo.SelectedItem is string selectedName && combo.DataContext is TestResultItem row)
            {
                var definition = _testDefinitions.FirstOrDefault(
                    t => t.TestName.Equals(selectedName, StringComparison.OrdinalIgnoreCase));

                if (definition != null)
                {
                    row.TestName = definition.TestName;
                    row.Unit = definition.Unit;
                    row.NormalRange = definition.NormalRange;
                    row.CategoryHeading = definition.CategoryHeading;
                    row.Price = definition.Price; // auto-fills the bill price
                }

                combo.IsDropDownOpen = false;
            }
        }

        // ---------------- ROW MANAGEMENT ----------------

        private void btnAddRow_Click(object sender, RoutedEventArgs e)
        {
            _results.Add(new TestResultItem());
        }

        private void btnRemoveRow_Click(object sender, RoutedEventArgs e)
        {
            if (dgResults.SelectedItem is TestResultItem selected)
                _results.Remove(selected);
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            txtName.Text = "";
            txtPhone.Text = "";
            txtAge.Text = "";
            cmbGender.SelectedIndex = -1;
            txtReferredBy.Text = "";
            txtLabName.Text = "";
            txtOverallDiscount.Text = "0";
            _results.Clear();
            for (int i = 0; i < 5; i++)
                _results.Add(new TestResultItem());
            txtStatus.Text = "";
        }

        // ---------------- STAGE 1: GENERATE BILL (patient + tests, no results, no upload yet) ----------------

        private void btnGenerateBill_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text) ||
                string.IsNullOrWhiteSpace(txtPhone.Text) ||
                string.IsNullOrWhiteSpace(txtAge.Text) ||
                cmbGender.SelectedItem == null)
            {
                MessageBox.Show("Please fill Name, Phone, Age and Gender (fields marked *).",
                    "Missing Information", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtAge.Text.Trim(), out int age))
            {
                MessageBox.Show("Age must be a number.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var validResults = _results.Where(r => !string.IsNullOrWhiteSpace(r.TestName)).ToList();
            if (validResults.Count == 0)
            {
                MessageBox.Show("Please select at least one test.", "Missing Tests",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtOverallDiscount.Text.Trim(), out decimal overallDiscountPercent) ||
                overallDiscountPercent < 0 || overallDiscountPercent > 100)
            {
                MessageBox.Show("Overall Discount % must be a number between 0 and 100.",
                    "Invalid Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (var row in validResults)
            {
                if (row.Discount < 0 || row.Discount > 100)
                {
                    MessageBox.Show($"Discount % for '{row.TestName}' must be between 0 and 100.",
                        "Invalid Discount", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            // Any row left without a category is grouped under "General" automatically.
            foreach (var row in validResults)
            {
                if (string.IsNullOrWhiteSpace(row.CategoryHeading))
                    row.CategoryHeading = "General";
            }

            btnGenerateBill.IsEnabled = false;
            txtStatus.Text = "Saving patient and generating bill...";

            try
            {
                decimal subtotal = validResults.Sum(r => r.NetPrice);
                decimal discountAmount = Math.Round(subtotal * overallDiscountPercent / 100m, 2);
                decimal netTotal = subtotal - discountAmount;

                var patient = new Patient
                {
                    Name = txtName.Text.Trim(),
                    Age = age,
                    Gender = (cmbGender.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "",
                    ReferredBy = txtReferredBy.Text.Trim(),
                    LabName = string.IsNullOrWhiteSpace(txtLabName.Text) ? AppSettings.LabName : txtLabName.Text.Trim(),
                    PhoneNumber = txtPhone.Text.Trim(),
                    ReportHeading = "Laboratory Test Report", // overall title; category headings do the real work now
                    CreatedAt = DateTime.Now,
                    Status = "Pending", // results not entered yet - report is completed later
                    TotalAmount = subtotal,
                    DiscountPercentage = overallDiscountPercent,
                    NetAmount = netTotal
                };

                // Stage 1 only: save patient + selected tests (Result is empty for now).
                string serialNumber = DatabaseHelper.SavePatientAndResults(patient, validResults);
                patient.SerialNumber = serialNumber;

                // The report's online URL is fully determined by the Serial Number, so we can
                // generate its QR code right now and print it on the bill - it will start
                // working as soon as the report is completed and uploaded later.
                string reportUrl = AppSettings.ReportsBaseUrl.TrimEnd('/') + "/" + serialNumber + ".pdf";
                byte[] qrBytes = QrCodeService.GenerateQrPng(reportUrl);

                string billPath = BillService.GenerateBill(patient, validResults, qrBytes);

                try
                {
                    BackupService.BackupDatabase(out _);
                }
                catch
                {
                    // Backups must never block the primary save flow.
                }

                txtStatus.Text = $"Bill generated for {serialNumber}. Total Payable: Rs. {netTotal:N0}. " +
                                  "The bill's QR code will work once the report is completed from \"Pending Reports\".";

                var result = MessageBox.Show(
                    $"Bill generated for {serialNumber}.\n" +
                    $"Subtotal: Rs. {subtotal:N0}\n" +
                    $"Overall Discount ({overallDiscountPercent:0.##}%): -Rs. {discountAmount:N0}\n" +
                    $"Total Payable: Rs. {netTotal:N0}\n\n" +
                    "The report is not ready yet - use \"Pending Reports\" once test results are ready.\n" +
                    "The QR code printed on this bill will work as soon as the report is completed.\n\n" +
                    "Do you want to open/print the bill now?",
                    "Bill Generated", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(billPath) { UseShellExecute = true });
                }

                btnClear_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Failed: " + ex.Message;
            }
            finally
            {
                btnGenerateBill.IsEnabled = true;
            }
        }

        // ---------------- STAGE 2 ENTRY POINT ----------------

        private void btnEnterResults_Click(object sender, RoutedEventArgs e)
        {
            var win = new PendingReportsWindow();
            win.ShowDialog();
        }

        private void btnHistory_Click(object sender, RoutedEventArgs e)
        {
            var historyWindow = new HistoryWindow();
            historyWindow.ShowDialog();
        }

        // ---------------- BACKUP / RESTORE ----------------

        private void btnBackupNow_Click(object sender, RoutedEventArgs e)
        {
            bool success = BackupService.BackupDatabase(out string message);

            MessageBox.Show(
                success ? message : $"Backup failed.\n{message}",
                success ? "Backup Complete" : "Backup Failed",
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Warning);
        }

        private void btnRestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "This will replace your CURRENT database with the backup file you choose.\n" +
                "Make sure no other computer/attendant is using this app right now.\n\nContinue?",
                "Restore Database", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            var dialog = new OpenFileDialog
            {
                Title = "Select a database backup file (.db)",
                Filter = "Database Backup (*.db)|*.db|All Files (*.*)|*.*",
                InitialDirectory = System.IO.Directory.Exists(AppSettings.BackupFolder) ? AppSettings.BackupFolder : ""
            };

            if (dialog.ShowDialog() == true)
            {
                bool success = BackupService.RestoreDatabase(dialog.FileName, out string message);

                MessageBox.Show(message,
                    success ? "Restore Successful" : "Restore Failed",
                    MessageBoxButton.OK,
                    success ? MessageBoxImage.Information : MessageBoxImage.Error);

                if (success)
                {
                    MessageBox.Show(
                        "The application will now close. Please reopen it to use the restored data.",
                        "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);
                    Application.Current.Shutdown();
                }
            }
        }
    }
}