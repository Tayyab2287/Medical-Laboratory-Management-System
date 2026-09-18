using System;

namespace LabReportApp.Models
{
    public class Patient
    {
        public int Id { get; set; }
        public string SerialNumber { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Gender { get; set; } = string.Empty;
        public string ReferredBy { get; set; } = string.Empty;
        public string LabName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string ReportHeading { get; set; } = string.Empty; // e.g. "Complete Blood Count (CBC)"
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // "Pending" = bill created, tests not performed / results not entered yet.
        // "Completed" = results entered, report generated, uploaded and SMS sent.
        public string Status { get; set; } = "Pending";

        // Sum of the Price of every test on the bill, in PKR (before discount).
        public decimal TotalAmount { get; set; }

        // Discount percentage applied at billing time (e.g. 10 = 10% off), usually given
        // on a doctor's reference. 0 means no discount.
        public decimal DiscountPercentage { get; set; }

        // Final payable amount after discount: TotalAmount - (TotalAmount * DiscountPercentage / 100).
        public decimal NetAmount { get; set; }
    }
}