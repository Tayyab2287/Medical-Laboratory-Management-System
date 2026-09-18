using System;
using FluentFTP;
using LabReportApp.Config;

namespace LabReportApp.Services
{
    public static class FtpUploadService
    {
        /// <summary>
        /// Uploads a local PDF file to the lab's web hosting via FTP so it becomes reachable
        /// at AppSettings.ReportsBaseUrl + fileName. Returns true if upload succeeded.
        /// </summary>
        public static bool UploadReport(string localFilePath, string fileName, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!AppSettings.EnableCloudUpload)
                return false; // Upload disabled (e.g. while testing offline)

            try
            {
                using var ftp = new FtpClient(AppSettings.FtpHost, AppSettings.FtpUsername, AppSettings.FtpPassword);
                ftp.Connect();

                string remotePath = AppSettings.FtpRemoteDirectory.TrimEnd('/') + "/" + fileName;

                var status = ftp.UploadFile(localFilePath, remotePath, FtpRemoteExists.Overwrite, true);

                ftp.Disconnect();

                return status == FtpStatus.Success;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
