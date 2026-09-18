using QRCoder;

namespace LabReportApp.Services
{
    public static class QrCodeService
    {
        /// <summary>
        /// Generates a QR code PNG (as bytes) that encodes the given URL/text.
        /// </summary>
        public static byte[] GenerateQrPng(string content)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            var pngQr = new PngByteQRCode(qrData);
            return pngQr.GetGraphic(20); // 20 = pixels per QR module (controls image size/resolution)
        }
    }
}
