using ClosedXML.Excel;

namespace SCS.SecurityCheck.Api.Services;

public sealed class ExcelExportService
{
    public byte[] ExportWeatherForecast(IEnumerable<WeatherForecastRow> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Weather Forecast");

        sheet.Cell(1, 1).Value = "Date";
        sheet.Cell(1, 2).Value = "Temp (C)";
        sheet.Cell(1, 3).Value = "Temp (F)";
        sheet.Cell(1, 4).Value = "Summary";

        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.SteelBlue;
        headerRow.Style.Font.FontColor = XLColor.White;

        int row = 2;
        foreach (var item in rows)
        {
            sheet.Cell(row, 1).Value = item.Date.ToString("yyyy-MM-dd");
            sheet.Cell(row, 2).Value = item.TemperatureC;
            sheet.Cell(row, 3).Value = item.TemperatureF;
            sheet.Cell(row, 4).Value = item.Summary ?? string.Empty;
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}

public sealed record WeatherForecastRow(
    DateOnly Date,
    int TemperatureC,
    int TemperatureF,
    string? Summary);
