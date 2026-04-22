using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using SCS.SecurityCheck.Api.Data;
using SCS.SecurityCheck.Api.Services;
using SCS.SecurityCheck.Api.Services.SecurityScan;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSingleton<ExcelExportService>();
builder.Services.AddSingleton<SecurityScanExcelExportService>();
builder.Services.AddHttpClient<IAiSuggestionService, HttpAiSuggestionService>();
builder.Services.AddSingleton<SecurityScannerService>();
builder.Services.AddSingleton<ScanJobManager>();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "scs.securitycheck.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var defaultConnection = builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(defaultConnection))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection is missing.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(defaultConnection));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapGet("/export/excel", (ExcelExportService excelService) =>
{
    var rows = Enumerable.Range(1, 10).Select(index =>
    {
        var tempC = Random.Shared.Next(-20, 55);
        return new WeatherForecastRow(
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            tempC,
            32 + (int)(tempC / 0.5556),
            summaries[Random.Shared.Next(summaries.Length)]);
    });

    var fileBytes = excelService.ExportWeatherForecast(rows);
    var fileName = $"weather-forecast-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";

    return Results.File(
        fileBytes,
        contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileDownloadName: fileName);
})
.WithName("ExportWeatherForecastExcel")
.Produces(200)
.WithSummary("匯出天氣預報 Excel 報表");

app.MapPost("/api/scans/run", async (
    ScanRequest request,
    SecurityScannerService scanner,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await scanner.ScanAsync(request, cancellationToken);
        return Results.Ok(result);
    }
    catch (ScanValidationException ex) when (ex.Code == "PROJECT_PATH_NOT_FOUND")
    {
        return Results.UnprocessableEntity(new { code = ex.Code, message = ex.Message.Trim() });
    }
    catch (ScanValidationException ex) when (ex.Code == "NO_SOURCE_FILES")
    {
        return Results.UnprocessableEntity(new { code = ex.Code, message = ex.Message.Trim() });
    }
    catch (ScanValidationException ex)
    {
        return Results.BadRequest(new { code = ex.Code, message = ex.Message.Trim() });
    }
})
.WithName("RunSecurityScan")
.RequireAuthorization()
.Produces<ScanResult>(200)
.Produces(401)
.Produces(422)
.Produces(400)
.WithSummary("執行 C# 專案弱點掃描並產出繁中 Markdown 報告（需登入）");

app.MapPost("/api/scans", (ScanRequest request, ScanJobManager jobManager) =>
{
    try
    {
        var started = jobManager.Start(request);
        return Results.Accepted($"/api/scans/{started.ScanId}", started);
    }
    catch (InvalidOperationException ex) when (ex.Message == "CONCURRENT_SCAN_LIMIT_REACHED")
    {
        return Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }
})
.WithName("StartSecurityScan")
.Produces<ScanJobStartResponse>(202)
.Produces(429)
.WithSummary("建立非同步掃描任務");

app.MapGet("/api/scans/{scanId}", (string scanId, string? key, ScanJobManager jobManager) =>
{
    if (string.IsNullOrWhiteSpace(key))
    {
        return Results.BadRequest(new { code = "SCAN_KEY_REQUIRED", message = "缺少掃描任務存取金鑰。" });
    }

    try
    {
        var status = jobManager.Get(scanId, key);
        return status is null
            ? Results.NotFound(new { code = "SCAN_NOT_FOUND", message = "找不到掃描任務。" })
            : Results.Ok(status);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbid();
    }
})
.WithName("GetSecurityScanStatus")
.Produces<ScanJobStatusResponse>(200)
.Produces(400)
.Produces(404)
.Produces(403)
.WithSummary("查詢掃描進度與結果");

app.MapPost("/api/scans/{scanId}/cancel", (string scanId, string? key, ScanJobManager jobManager) =>
{
    if (string.IsNullOrWhiteSpace(key))
    {
        return Results.BadRequest(new { code = "SCAN_KEY_REQUIRED", message = "缺少掃描任務存取金鑰。" });
    }

    try
    {
        var cancelled = jobManager.Cancel(scanId, key);
        return cancelled
            ? Results.Ok(new { scanId, status = "cancellation_requested" })
            : Results.NotFound(new { code = "SCAN_NOT_FOUND", message = "找不到掃描任務。" });
    }
    catch (UnauthorizedAccessException)
    {
        return Results.Forbid();
    }
})
.WithName("CancelSecurityScan")
.Produces(200)
.Produces(400)
.Produces(404)
.Produces(403)
.WithSummary("取消掃描任務");

app.MapGet("/api/scans/{scanId}/export/excel",
    (string scanId, string? key, ScanJobManager jobManager, SecurityScanExcelExportService excelExporter) =>
    {
        if (string.IsNullOrWhiteSpace(key))
            return Results.BadRequest(new { code = "SCAN_KEY_REQUIRED", message = "缺少掃描任務存取金鑰。" });

        ScanJobStatusResponse? status;
        try
        {
            status = jobManager.Get(scanId, key);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.Forbid();
        }

        if (status is null)
            return Results.NotFound(new { code = "SCAN_NOT_FOUND", message = "找不到掃描任務。" });
        if (status.Status != "completed" || status.Result is null)
            return Results.BadRequest(new { code = "SCAN_NOT_COMPLETED", message = "掃描尚未完成，無法匯出。" });

        var bytes = excelExporter.Export(status.Result);
        var fileName = $"security-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        return Results.File(
            bytes,
            contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileDownloadName: fileName);
    })
.WithName("ExportSecurityScanExcel")
.Produces(200)
.Produces(400)
.Produces(403)
.Produces(404)
.WithSummary("將掃描結果匯出為 Excel (.xlsx)");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
