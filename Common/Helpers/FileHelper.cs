using iText.Kernel.Pdf.Canvas.Parser;
using System.Text;
using Common.Enums;
using Common.Models;

using iTextSharp.text;
using iTextSharp.text.pdf;
using Document = iTextSharp.text.Document;
using PdfReader = iText.Kernel.Pdf.PdfReader;
using PdfDocument = iText.Kernel.Pdf.PdfDocument;
using PdfWriter = iTextSharp.text.pdf.PdfWriter;
using Common.Constants;

namespace Common.Helpers
{
	public static class FileHelper
	{
		public static string PdfToString(byte[] documentContent)
		{
			using var stream = new MemoryStream(documentContent);
			using var pdfReader = new PdfReader(stream);
			using var pdfDocument = new PdfDocument(pdfReader);

			StringBuilder text = new();
			for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
			{
				text.Append(PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(i)));
			}

			return text.ToString();
		}
		public static int CountPdfCharacters(byte[] documentContent)
		{
			using var stream = new MemoryStream(documentContent);
			using var pdfReader = new PdfReader(stream);
			using var pdfDocument = new PdfDocument(pdfReader);

			int pageCount = pdfDocument.GetNumberOfPages();
			int characterCount = 0;

			for (int i = 1; i <= pageCount; i++)
			{
				string text = PdfTextExtractor.GetTextFromPage(pdfDocument.GetPage(i));
				characterCount += text.Length;
			}

			return characterCount;
		}

		public static int CountTxtCharacters(byte[] documentContent)
		{
			return Encoding.UTF8.GetString(documentContent).Length;
		}

		public static string TxtToString(byte[] documentContent)
		{
			return Encoding.UTF8.GetString(documentContent);
		}

		public static int GetLengthBasedOnExtension(byte[] documentContent, DocumentExtension extension)
		{
			return extension switch
			{
				DocumentExtension.PDF => CountPdfCharacters(documentContent),
				DocumentExtension.TXT => CountTxtCharacters(documentContent),
				DocumentExtension.PY => CountTxtCharacters(documentContent),
				_ => throw new ArgumentException($"Unsupported document extension: {extension}")
			};
		}

		public static string GetTextBasedOnContent(byte[] documentContent, string contentType)
		{
			return contentType switch
			{
				Types.PyContentType => TxtToString(documentContent),
				Types.PdfContentType => PdfToString(documentContent),
				Types.TxtContentType => TxtToString(documentContent),
				_ => throw new ArgumentException($"Unsupported document content type: {contentType}")
			};
		}

		public static byte[] GeneratePdf(List<StudentProgress> studentProgress, DateTime? startDate, DateTime? endDate, string commonMistakes)
		{
			using MemoryStream stream = new();
			Document document = new(PageSize.A4, 50, 50, 50, 50);
			PdfWriter writer = PdfWriter.GetInstance(document, stream);
			document.Open();

			Font titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.BLACK);
			Paragraph title = new("Students Progress Report", titleFont)
			{
				Alignment = Element.ALIGN_CENTER
			};
			document.Add(title);
			document.Add(new Paragraph("\n"));
			document.Add(new Paragraph($"Period: {startDate:yyyy-MM-dd} - {endDate:yyyy-MM-dd}\n\n"));
			Font headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12);
			Font normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 10);

			var groupedByCourse = studentProgress.GroupBy(sp => sp.CourseName);

			foreach (var courseGroup in groupedByCourse)
			{
				document.Add(new Paragraph($"Course: {courseGroup.Key}\n", headerFont));
				document.Add(new Paragraph("\n"));
				PdfPTable table = new PdfPTable(4) { WidthPercentage = 100 };
				table.SetWidths(new float[] { 2, 4, 2, 2 });

				table.AddCell(new PdfPCell(new Phrase("#", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY });
				table.AddCell(new PdfPCell(new Phrase("Student", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY });
				table.AddCell(new PdfPCell(new Phrase("Avg. Score", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY });
				table.AddCell(new PdfPCell(new Phrase("File Name", headerFont)) { BackgroundColor = BaseColor.LIGHT_GRAY });

				int count = 1;
				foreach (var progress in courseGroup)
				{
					table.AddCell(new PdfPCell(new Phrase(count.ToString(), normalFont)));
					table.AddCell(new PdfPCell(new Phrase(progress.StudentFullName, normalFont)));
					table.AddCell(new PdfPCell(new Phrase(progress.AvgScore.ToString(), normalFont)));
					table.AddCell(new PdfPCell(new Phrase(progress.FileName, normalFont)));
					count++;
				}

				document.Add(table);
				document.Add(new Paragraph("\n"));
			}

			document.Add(new Paragraph("\n"));
			document.Add(new Paragraph(commonMistakes));


			document.Close();
			writer.Close();

			return stream.ToArray();
		}
	}
}
