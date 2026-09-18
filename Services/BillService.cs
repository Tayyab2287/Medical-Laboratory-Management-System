using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LabReportApp.Config;
using LabReportApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LabReportApp.Services
{
    public static class BillService
    {
        /// <summary>
        /// Generates a Bill/Invoice PDF for a patient at billing time (before any test results
        /// exist) - lists each test with its price and per-test discount, then a subtotal, the
        /// overall discount, and the final total payable. A QR code linking to the (not-yet-
        /// generated) online report is printed on the bill - since the report's URL is fully
        /// determined by the Serial Number, this QR code will work correctly once the Lab
        /// Attendant later finishes the report and uploads it under that same URL. Saves the
        /// bill locally and returns the file path.
        /// </summary>
        public static string GenerateBill(Patient patient, List<TestResultItem> tests, byte[] qrPngBytes)
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppSettings.LocalBillsFolder);
            Directory.CreateDirectory(folder);
            string filePath = Path.Combine(folder, $"{patient.SerialNumber}_Bill.pdf");

            decimal subtotal = tests.Sum(t => t.NetPrice);
            decimal overallDiscountAmount = Math.Round(subtotal * patient.DiscountPercentage / 100m, 2);
            decimal grandTotal = subtotal - overallDiscountAmount;

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(patient.LabName == "" ? AppSettings.LabName : patient.LabName)
                                    .FontSize(16).Bold();
                                c.Item().Text("Patient Bill / Invoice").FontSize(11).Italic();
                            });

                            row.ConstantItem(70).Height(70).Image(qrPngBytes); // report QR code, top-right
                        });

                        col.Item().PaddingTop(5).LineHorizontal(1);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Item().Border(1).Padding(8).Column(details =>
                        {
                            details.Item().Text(t => { t.Span("Serial No: ").Bold(); t.Span(patient.SerialNumber); });
                            details.Item().Text(t => { t.Span("Date: ").Bold(); t.Span(patient.CreatedAt.ToString("dd-MMM-yyyy hh:mm tt")); });
                            details.Item().Text(t => { t.Span("Patient Name: ").Bold(); t.Span(patient.Name); });
                            details.Item().Text(t => { t.Span("Age/Gender: ").Bold(); t.Span($"{patient.Age} / {patient.Gender}"); });
                            details.Item().Text(t => { t.Span("Contact No: ").Bold(); t.Span(patient.PhoneNumber); });
                            if (!string.IsNullOrWhiteSpace(patient.ReferredBy))
                                details.Item().Text(t => { t.Span("Referred By: ").Bold(); t.Span(patient.ReferredBy); });
                        });

                        col.Item().PaddingTop(12).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(4);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.6f);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Test Name");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Price (PKR)");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Discount");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Net (PKR)");

                                static IContainer HeaderCell(IContainer c) =>
                                    c.Background(Colors.Grey.Lighten2).Padding(5).DefaultTextStyle(x => x.Bold());
                            });

                            foreach (var t in tests)
                            {
                                table.Cell().Element(BodyCell).Text(t.TestName);
                                table.Cell().Element(BodyCell).AlignRight().Text(t.Price.ToString("N0"));
                                table.Cell().Element(BodyCell).AlignRight().Text(t.Discount > 0 ? $"{t.Discount:0.##}%" : "-");
                                table.Cell().Element(BodyCell).AlignRight().Text(t.NetPrice.ToString("N0"));
                            }

                            static IContainer BodyCell(IContainer c) =>
                                c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(5);
                        });

                        col.Item().PaddingTop(12).AlignRight().Width(220).Column(totals =>
                        {
                            totals.Item().Row(r =>
                            {
                                r.RelativeItem().Text("Subtotal:");
                                r.ConstantItem(90).AlignRight().Text($"Rs. {subtotal:N0}");
                            });

                            if (patient.DiscountPercentage > 0)
                            {
                                totals.Item().Row(r =>
                                {
                                    r.RelativeItem().Text($"Overall Discount ({patient.DiscountPercentage:0.##}%):");
                                    r.ConstantItem(90).AlignRight().Text($"-Rs. {overallDiscountAmount:N0}").FontColor(Colors.Red.Darken1);
                                });
                            }

                            totals.Item().PaddingTop(4).BorderTop(1).PaddingTop(4).Row(r =>
                            {
                                r.RelativeItem().Text("Total Payable:").Bold().FontSize(13);
                                r.ConstantItem(90).AlignRight().Text($"Rs. {grandTotal:N0}").Bold().FontSize(13);
                            });
                        });

                        col.Item().PaddingTop(20)
                            .Text("Note: This is a bill for the requested tests. Once your sample testing is complete, " +
                                  "scan the QR code above to download your report online.")
                            .FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1);
                        col.Item().PaddingTop(5).AlignCenter().Text(AppSettings.LabAddress).FontSize(8);
                        col.Item().AlignCenter().Text(AppSettings.LabContact).FontSize(8);
                    });
                });
            })
            .GeneratePdf(filePath);

            return filePath;
        }
    }
}