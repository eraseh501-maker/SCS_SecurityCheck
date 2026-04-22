using ClosedXML.Excel;
using SCS.SecurityCheck.Api.Services.SecurityScan;

namespace SCS.SecurityCheck.Api.Services;

/// <summary>
/// Exports a <see cref="ScanResult"/> to a multi-sheet Excel workbook (.xlsx).
/// Sheet 1 – Summary   : scan metadata + severity counts
/// Sheet 2 – CVSS      : findings aggregated by rule, sorted by CVSS score
/// Sheet 3 – Findings  : every individual finding with full detail
/// </summary>
public sealed class SecurityScanExcelExportService
{
    // ── colour palette (ARGB hex strings) ──────────────────────────────────
    private static readonly XLColor HeaderBg   = XLColor.FromHtml("#1f2937");
    private static readonly XLColor HeaderFg   = XLColor.White;
    private static readonly XLColor CriticalBg = XLColor.FromHtml("#7f1d1d");
    private static readonly XLColor HighBg     = XLColor.FromHtml("#92400e");
    private static readonly XLColor MediumBg   = XLColor.FromHtml("#1e3a5f");
    private static readonly XLColor LowBg      = XLColor.FromHtml("#14532d");
    private static readonly XLColor CvssRed    = XLColor.FromHtml("#b91c1c");
    private static readonly XLColor CvssAmber  = XLColor.FromHtml("#b45309");
    private static readonly XLColor CvssBlue   = XLColor.FromHtml("#0369a1");
    private static readonly XLColor CvssGreen  = XLColor.FromHtml("#166534");
    private static readonly XLColor RowAlt     = XLColor.FromHtml("#f9fafb");

    public byte[] Export(ScanResult result)
    {
        using var wb = new XLWorkbook();

        BuildSummarySheet(wb, result);
        BuildCvssSheet(wb, result.Findings);
        BuildFindingsSheet(wb, result.Findings);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Sheet 1: 掃描摘要 ──────────────────────────────────────────────────
    private static void BuildSummarySheet(XLWorkbook wb, ScanResult result)
    {
        var ws = wb.Worksheets.Add("掃描摘要");
        var s  = result.Summary;

        // Title
        var title = ws.Cell(1, 1);
        title.Value = "C# 安全弱點掃描摘要";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 16;
        ws.Range("A1:B1").Merge();

        // Metadata
        var meta = new (string Label, string Value)[]
        {
            ("掃描時間",   DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")),
            ("掃描檔案數", s.TotalFilesScanned.ToString()),
            ("總弱點數",   s.TotalFindings.ToString()),
        };

        int row = 3;
        foreach (var (label, value) in meta)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 2).Value = value;
            row++;
        }

        row++;

        // Severity counts table
        var headerRow = ws.Row(row);
        ws.Cell(row, 1).Value = "嚴重度";
        ws.Cell(row, 2).Value = "件數";
        StyleHeaderRow(ws.Range(row, 1, row, 2));
        row++;

        var severities = new (string Label, int Count, XLColor Bg)[]
        {
            ("Critical", s.Critical, CriticalBg),
            ("High",     s.High,     HighBg),
            ("Medium",   s.Medium,   MediumBg),
            ("Low",      s.Low,      LowBg),
        };

        foreach (var (label, count, bg) in severities)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Fill.BackgroundColor = bg;
            ws.Cell(row, 1).Style.Font.FontColor       = XLColor.White;
            ws.Cell(row, 1).Style.Font.Bold            = true;
            ws.Cell(row, 2).Value = count;
            ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            row++;
        }

        ws.Columns().AdjustToContents();
        ws.Column(2).Width = Math.Max(ws.Column(2).Width, 50);
    }

    // ── Sheet 2: CVSS 修復優先序 ───────────────────────────────────────────
    private static void BuildCvssSheet(XLWorkbook wb, IReadOnlyList<ScanFinding> findings)
    {
        var ws = wb.Worksheets.Add("CVSS 修復優先序");

        // Header
        var headers = new[] { "#", "規則碼", "弱點名稱", "嚴重度", "CVSS v3", "次數", "影響檔案數" };
        for (int c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        StyleHeaderRow(ws.Range(1, 1, 1, headers.Length));

        // Aggregate
        var grouped = findings
            .GroupBy(f => new { f.RuleCode, f.Title, f.Severity, f.CvssScore })
            .OrderByDescending(g => g.Key.CvssScore)
            .ThenBy(g => g.Key.RuleCode)
            .ToList();

        int row = 2;
        foreach (var (g, idx) in grouped.Select((g, i) => (g, i)))
        {
            bool alt = idx % 2 == 1;
            ws.Cell(row, 1).Value = idx + 1;
            ws.Cell(row, 2).Value = g.Key.RuleCode;
            ws.Cell(row, 3).Value = g.Key.Title;

            var sev = ws.Cell(row, 4);
            sev.Value = g.Key.Severity;
            sev.Style.Fill.BackgroundColor = SeverityBg(g.Key.Severity);
            sev.Style.Font.FontColor       = XLColor.White;
            sev.Style.Font.Bold            = true;
            sev.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var cvssCell = ws.Cell(row, 5);
            cvssCell.Value = g.Key.CvssScore;
            cvssCell.Style.Font.FontColor = CvssColor(g.Key.CvssScore);
            if (g.Key.CvssScore >= 9.0) cvssCell.Style.Font.Bold = true;
            cvssCell.Style.NumberFormat.Format = "0.0";
            cvssCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 6).Value = g.Count();
            ws.Cell(row, 7).Value = g.Select(f => f.FilePath).Distinct().Count();

            if (alt)
            {
                var altRange = ws.Range(row, 1, row, headers.Length);
                foreach (var cell in altRange.Cells().Where(c => c.Style.Fill.BackgroundColor == XLColor.NoColor
                                                                   || c.Style.Fill.BackgroundColor == XLColor.Transparent))
                    cell.Style.Fill.BackgroundColor = RowAlt;
            }

            row++;
        }

        if (grouped.Count == 0)
        {
            ws.Cell(2, 1).Value = "無弱點發現";
            ws.Range(2, 1, 2, headers.Length).Merge();
        }

        ws.Columns().AdjustToContents();
        ws.Column(3).Width = 36;
        ws.Column(5).Width = 12;
        ws.Row(1).Height   = 20;
    }

    // ── Sheet 3: 弱點清單 ──────────────────────────────────────────────────
    private static void BuildFindingsSheet(XLWorkbook wb, IReadOnlyList<ScanFinding> findings)
    {
        var ws = wb.Worksheets.Add("弱點清單");

        var headers = new[] { "#", "規則碼", "弱點名稱", "嚴重度", "CVSS v3", "檔案路徑", "行號", "證據", "建議" };
        for (int c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        StyleHeaderRow(ws.Range(1, 1, 1, headers.Length));

        var sorted = findings
            .OrderByDescending(f => f.CvssScore)
            .ThenBy(f => f.FilePath)
            .ThenBy(f => f.Line)
            .ToList();

        int row = 2;
        foreach (var (f, idx) in sorted.Select((f, i) => (f, i)))
        {
            ws.Cell(row, 1).Value = idx + 1;
            ws.Cell(row, 2).Value = f.RuleCode;
            ws.Cell(row, 3).Value = f.Title;

            var sev = ws.Cell(row, 4);
            sev.Value = f.Severity;
            sev.Style.Fill.BackgroundColor = SeverityBg(f.Severity);
            sev.Style.Font.FontColor       = XLColor.White;
            sev.Style.Font.Bold            = true;
            sev.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var cvssCell = ws.Cell(row, 5);
            cvssCell.Value = f.CvssScore;
            cvssCell.Style.Font.FontColor = CvssColor(f.CvssScore);
            if (f.CvssScore >= 9.0) cvssCell.Style.Font.Bold = true;
            cvssCell.Style.NumberFormat.Format = "0.0";
            cvssCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 6).Value = f.FilePath;
            ws.Cell(row, 7).Value = f.Line;
            ws.Cell(row, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(row, 8).Value = f.Evidence;
            ws.Cell(row, 8).Style.Font.FontName = "Consolas";

            ws.Cell(row, 9).Value = f.Recommendation;

            if (idx % 2 == 1)
            {
                for (int col = 1; col <= headers.Length; col++)
                {
                    var cell = ws.Cell(row, col);
                    if (cell.Style.Fill.BackgroundColor == XLColor.NoColor
                     || cell.Style.Fill.BackgroundColor == XLColor.Transparent)
                        cell.Style.Fill.BackgroundColor = RowAlt;
                }
            }

            row++;
        }

        if (sorted.Count == 0)
        {
            ws.Cell(2, 1).Value = "無弱點發現";
            ws.Range(2, 1, 2, headers.Length).Merge();
        }

        ws.Columns().AdjustToContents();
        ws.Column(6).Width = 50;
        ws.Column(8).Width = 50;
        ws.Column(9).Width = 60;
        ws.Row(1).Height   = 20;
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private static void StyleHeaderRow(IXLRange range)
    {
        range.Style.Fill.BackgroundColor = HeaderBg;
        range.Style.Font.FontColor       = HeaderFg;
        range.Style.Font.Bold            = true;
        range.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
    }

    private static XLColor SeverityBg(string severity) => severity switch
    {
        "Critical" => CriticalBg,
        "High"     => HighBg,
        "Medium"   => MediumBg,
        _          => LowBg,
    };

    private static XLColor CvssColor(double score) => score switch
    {
        >= 9.0 => CvssRed,
        >= 7.0 => CvssAmber,
        >= 4.0 => CvssBlue,
        _      => CvssGreen,
    };
}
