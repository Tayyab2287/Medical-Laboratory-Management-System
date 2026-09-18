/*
 * ================================================================================
 * SMS SYSTEM - DISABLED / COMMENTED OUT
 * ================================================================================
 * The lab switched from SMS notifications to QR-code based report delivery:
 * the QR code is printed on the patient's bill at billing time (see BillService.cs)
 * and again on the final report (see PdfReportService.cs), so patients simply scan
 * it to get their report - no SMS needed.
 *
 * To re-enable SMS in the future:
 *   1. Uncomment everything below.
 *   2. In Config/AppSettings.cs, uncomment SmsApiUrlTemplate and EnableSmsSending.
 *   3. In EnterResultsWindow.xaml.cs, uncomment the SmsService.SendSmsAsync(...) call
 *      and the smsSent status-message lines (also commented out there).
 * ================================================================================

using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using LabReportApp.Config;

namespace LabReportApp.Services
{
    public static class SmsService
    {
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Sends an SMS by calling the URL template configured in AppSettings, with
        /// {phone} and {message} replaced. Works with most simple HTTP-based SMS gateways.
        /// </summary>
        public static async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            if (!AppSettings.EnableSmsSending)
                return false; // SMS disabled (e.g. while testing offline)

            try
            {
                string encodedMessage = HttpUtility.UrlEncode(message);
                string url = AppSettings.SmsApiUrlTemplate
                    .Replace("{phone}", phoneNumber)
                    .Replace("{message}", encodedMessage);

                var response = await httpClient.GetAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}

*/