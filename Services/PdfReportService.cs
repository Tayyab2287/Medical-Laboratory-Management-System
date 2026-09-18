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
    public static class PdfReportService
    {
        /// <summary>
        /// Builds the PDF report for a patient and saves it locally. Test results are grouped
        /// by their CategoryHeading, so a single report can show multiple category sections
        /// (e.g. "Complete Blood Count", "Liver Function Tests", "Kidney Function Tests"),
        /// each with its own heading and its own table of tests.
        /// Returns the full local file path of the generated PDF.
        /// </summary>
        public static string GenerateReport(Patient patient, List<TestResultItem> results, byte[] qrPngBytes)
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppSettings.LocalReportsFolder);
            Directory.CreateDirectory(folder);
            string filePath = Path.Combine(folder, $"{patient.SerialNumber}.pdf");

            // Group the flat list of results into categories, preserving the order in which
            // each category first appeared (so the report reads in the order it was entered).
            var groupedByCategory = results
                .GroupBy(r => string.IsNullOrWhiteSpace(r.CategoryHeading) ? "General" : r.CategoryHeading)
                .ToList();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    // ---------------- HEADER ----------------
                    page.Header().Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text(patient.LabName == "" ? AppSettings.LabName : patient.LabName)
                                    .FontSize(18).Bold();
                                c.Item().Text("Blood Test Laboratory Report ABC").FontSize(11).Italic();
                            });

                            row.ConstantItem(90).Height(90).Image(qrPngBytes); // QR code top-right
                        });

                        col.Item().PaddingTop(5).LineHorizontal(1);
                    });

                    // ---------------- CONTENT ----------------
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // Patient details block
                        col.Item().Border(1).Padding(8).Column(details =>
                        {
                            details.Item().Row(r =>
                            {
                                r.RelativeItem().Text(t => { t.Span("Serial No: ").Bold(); t.Span(patient.SerialNumber); });
                                r.RelativeItem().Text(t => { t.Span("Date: ").Bold(); t.Span(patient.CreatedAt.ToString("dd-MMM-yyyy hh:mm tt")); });
                            });
                            details.Item().Row(r =>
                            {
                                r.RelativeItem().Text(t => { t.Span("Patient Name: ").Bold(); t.Span(patient.Name); });
                                r.RelativeItem().Text(t => { t.Span("Age/Gender: ").Bold(); t.Span($"{patient.Age} / {patient.Gender}"); });
                            });
                            details.Item().Row(r =>
                            {
                                r.RelativeItem().Text(t => { t.Span("Referred By: ").Bold(); t.Span(patient.ReferredBy); });
                                r.RelativeItem().Text(t => { t.Span("Contact No: ").Bold(); t.Span(patient.PhoneNumber); });
                            });
                        });

                        // One heading + one table PER CATEGORY
                        foreach (var group in groupedByCategory)
                        {
                            col.Item().PaddingTop(15).Text(group.Key)
                                .FontSize(14).Bold().FontColor(Colors.Blue.Darken2);

                            col.Item().PaddingTop(8).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(3); // Test Name
                                    columns.RelativeColumn(2); // Result
                                    columns.RelativeColumn(2); // Unit
                                    columns.RelativeColumn(3); // Normal Range
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCell).Text("Test Name");
                                    header.Cell().Element(HeaderCell).Text("Result");
                                    header.Cell().Element(HeaderCell).Text("Unit");
                                    header.Cell().Element(HeaderCell).Text("Normal Range");

                                    static IContainer HeaderCell(IContainer c) =>
                                        c.Background(Colors.Grey.Lighten2).Padding(5).DefaultTextStyle(x => x.Bold());
                                });

                                foreach (var row in group)
                                {
                                    table.Cell().Element(BodyCell).Text(row.TestName);
                                    table.Cell().Element(BodyCell).Text(row.Result);
                                    table.Cell().Element(BodyCell).Text(row.Unit);
                                    table.Cell().Element(BodyCell).Text(row.NormalRange);
                                }

                                static IContainer BodyCell(IContainer c) =>
                                    c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(5);
                            });
                        }

                        col.Item().PaddingTop(20).Text("-- End of Report --")
                            .FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                    });

                    // ---------------- FOOTER ----------------
                    page.Footer().Column(col =>
                    {
                        col.Item().LineHorizontal(1);
                        col.Item().PaddingTop(5).AlignCenter().Text(AppSettings.LabAddress).FontSize(9);
                        col.Item().AlignCenter().Text(AppSettings.LabContact).FontSize(9);
                        col.Item().AlignCenter().Text("Scan the QR code above to view/download this report online.")
                            .FontSize(8).Italic();
                    });
                });
            })
            .GeneratePdf(filePath);

            return filePath;
        }
    }
}
