using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using LabReportApp.Config;
using LabReportApp.Database;
using LabReportApp.Models;
using LabReportApp.Services;

namespace LabReportApp
{
    public partial class EnterResultsWindow : Window
    {
        private readonly Patient _patient;
        private readonly ObservableCollection<TestResultItem> _results = new();

        public EnterResultsWindow(int patientId)
        {
            InitializeComponent();
            SourceInitialized += (s, e) => TitleBarHelper.ApplyTitleBarTheme(this, ThemeManager.CurrentTheme == "Dark");

            var loaded = DatabaseHelper.GetPatientById(patientId);
            if (loaded == null)
            {
                MessageBox.Show("This patient could not be found (it may have been removed).",
                    "Not Found", MessageBoxButton.OK, MessageBoxImage.Error);
                _patient = new Patient(); // keeps the compiler happy; window closes right after
                Loaded += (_, _) => Close();
                return;
            }

            _patient = loaded;

            txtPatientSummary.Text =
                $"{_patient.SerialNumber}  |  {_patient.Name}  ({_patient.Age} / {_patient.Gender})  |  {_patient.PhoneNumber}";

            foreach (var row in DatabaseHelper.GetResultsForPatient(_patient.Id))
                _results.Add(row);

            dgResults.ItemsSource = _results;
        }

        private void btnSaveResults_Click(object sender, RoutedEventArgs e)
        {
            var missing = _results.Where(r => string.IsNullOrWhiteSpace(r.Result)).ToList();
            if (missing.Count > 0)
            {
                var confirm = MessageBox.Show(
                    $"{missing.Count} test(s) still have no result entered. Save and send anyway?",
                    "Missing Results", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes)
                    return;
            }

            btnSaveResults.IsEnabled = false;
            txtStatus.Text = "Saving results...";

            try
            {
                // Stage 2: write the Result values back into the existing TestResults rows
                // and mark this patient's Status as "Completed".
                DatabaseHelper.SaveResultsAndComplete(_patient.Id, _results.ToList());

                // Same QR/URL that was already printed on the bill at billing time - this is
                // exactly where the report needs to be uploaded so that QR code starts working.
                string reportUrl = AppSettings.ReportsBaseUrl.TrimEnd('/') + "/" + _patient.SerialNumber + ".pdf";
                byte[] qrBytes = QrCodeService.GenerateQrPng(reportUrl);

                txtStatus.Text = "Generating PDF report...";
                string localPdfPath = PdfReportService.GenerateReport(_patient, _results.ToList(), qrBytes);

                try
                {
                    BackupService.BackupDatabase(out _);
                    BackupService.BackupPdfReport(localPdfPath);
                }
                catch
                {
                    // Backups must never block the completion flow.
                }

                txtStatus.Text = "Uploading report online...";
                bool uploaded = FtpUploadService.UploadReport(localPdfPath, _patient.SerialNumber + ".pdf", out string ftpError);

                /* ============================================================================
                 * SMS NOTIFICATION - DISABLED / COMMENTED OUT
                 * The patient now retrieves their report by scanning the QR code that was
                 * already printed on their bill (and is also on the report itself) - no SMS
                 * is sent. To re-enable SMS in future, uncomment this block, restore
                 * SmsApiUrlTemplate/EnableSmsSending in Config/AppSettings.cs, and uncomment
                 * Services/SmsService.cs.
                 *
                 * txtStatus.Text = "Sending SMS to patient...";
                 * bool smsSent = false;
                 * if (uploaded)
                 * {
                 *     string message = $"Dear {_patient.Name} (Age: {_patient.Age}, Gender: {_patient.Gender}), your lab report is ready.\n" +
                 *                       $"Serial No: {_patient.SerialNumber}\n" +
                 *                       $"Date & Time: {DateTime.Now:dd-MMM-yyyy hh:mm tt}\n" +
                 *                       $"Download your report here: {reportUrl}";
                 *     smsSent = await SmsService.SendSmsAsync(_patient.PhoneNumber, message);
                 * }
                 * ============================================================================ */

                string statusMsg = $"Report for {_patient.SerialNumber} completed and saved at:\n{localPdfPath}\n";
                statusMsg += uploaded
                    ? "Uploaded online successfully. The QR code on the bill/report will now work."
                    : $"Upload FAILED ({ftpError}). Report is only saved locally.";
                txtStatus.Text = statusMsg;

                var result = MessageBox.Show(
                    $"Report for {_patient.SerialNumber} has been completed.\n\nOpen the PDF now?",
                    "Success", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(new ProcessStartInfo(localPdfPath) { UseShellExecute = true });
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Failed: " + ex.Message;
            }
            finally
            {
                btnSaveResults.IsEnabled = true;
            }
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}