using Microsoft.EntityFrameworkCore;
using StatsServer;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Stats Server API",
        Description = "Сервер для сбора статистики",
        Version = "v1"
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Stats Server API v1");
    c.RoutePrefix = "swagger";
});

var db = new AppDbContext();
db.Database.EnsureCreated();
Console.WriteLine("База данных создана!");
Console.WriteLine("Ожидание данных...");

app.MapGet("/", () =>
{
    return "Сервер статистики работает! Перейдите на /swagger для документации API";
});

app.MapPost("/api/stats", async (HttpContext context) =>
{
    Console.Clear();

    Console.WriteLine("======= ПОЛУЧЕНЫ НОВЫЕ ДАННЫЕ ========");

    string json = await new StreamReader(context.Request.Body).ReadToEndAsync();
    Console.WriteLine("Присланные данные:");
    Console.WriteLine(json);

    try
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        var data = new StatisticData
        {
            StartTime = DateTime.Parse(root.GetProperty("startTime").GetString() ?? "2000-01-01"),
            WorkMode = root.GetProperty("workMode").GetString() ?? "",
            ElevationResult = root.GetProperty("elevationResult").GetString() ?? "",
            DownloadResult = root.GetProperty("downloadResult").GetBoolean(),
            DownloadError = root.GetProperty("downloadError").GetString() ?? "",
            LaunchResult = root.GetProperty("launchResult").GetBoolean(),
            ReceivedAt = DateTime.Now,
            RawData = json
        };

        using var dbStats = new AppDbContext();
        dbStats.Statistics.Add(data);
        dbStats.SaveChanges(); 

        Console.WriteLine($"\nДанные сохранены в БД (ID: {data.Id})");

        ShowAllData();

        return Results.Ok(new
        {
            message = "Статистика сохранена",
            id = data.Id
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка: {ex.Message}");
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/download/7zip/{bits}", async (string bits, HttpContext context) =>
{
    try
    {
        if (bits != "32" && bits != "64")
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("Ошибка: Укажите битность - 32 или 64. Пример: /download/7zip/32 или /download/7zip/64");
            return;
        }

        var currentDirectory = AppContext.BaseDirectory;

        string fileName = bits == "32" ? "7z2600.exe" : "7z2600-x64.exe";

        var filePath = Path.Combine(currentDirectory, bits, fileName);

        Console.WriteLine($"Пытаемся найти файл: {filePath}");

        if (!File.Exists(filePath))
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync($"Ошибка: Файл {fileName} не найден.\n" +
                                             $"Искали здесь: {filePath}\n" +
                                             $"Убедитесь, что папка '{bits}' существует и содержит файл {fileName}");
            return;
        }

        var fileInfo = new FileInfo(filePath);
        Console.WriteLine($"Найден файл: {fileName} ({bits}-бит), размер: {fileInfo.Length} байт");
        Console.WriteLine($"Отдаем файл клиенту...");

        context.Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
        await context.Response.SendFileAsync(filePath);

        Console.WriteLine($"Файл успешно отправлен");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Ошибка при отправке файла: {ex.Message}");
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Внутренняя ошибка сервера: {ex.Message}");
    }
});

app.MapGet("/download/7zip", () =>
{
    var currentDirectory = AppContext.BaseDirectory;

    string path32 = Path.Combine(currentDirectory, "32", "7z2600.exe");
    bool has32 = File.Exists(path32);

    string path64 = Path.Combine(currentDirectory, "64", "7z2600-x64.exe");
    bool has64 = File.Exists(path64);

    var result = new
    {
        message = "Доступные версии 7-Zip для скачивания:",
        files = new
        {
            x32 = has32 ? "доступен" : "не найден",
            x64 = has64 ? "доступен" : "не найден"
        },
        paths = new
        {
            x32 = path32,
            x64 = path64
        },
        download_links = new
        {
            x32 = has32 ? "/download/7zip/32" : null,
            x64 = has64 ? "/download/7zip/64" : null
        }
    };

    return result;
});

void ShowAllData()
{
    using var dbShow = new AppDbContext();
    var allData = dbShow.Statistics.OrderByDescending(x => x.ReceivedAt).ToList();

    Console.WriteLine($"\n========== ВСЯ БАЗА ДАННЫХ ===========");
    Console.WriteLine($"Всего записей: {allData.Count}");
    Console.WriteLine("======================================");

    if (allData.Count == 0)
    {
        Console.WriteLine("База данных пуста");
    }
    else
    {
        foreach (var item in allData)
        {
            Console.WriteLine($"ID: {item.Id}");
            Console.WriteLine($"  Время старта: {item.StartTime}");
            Console.WriteLine($"  Режим работы: '{item.WorkMode}'");
            Console.WriteLine($"  Elevation: '{item.ElevationResult}'");
            Console.WriteLine($"  Download: '{item.DownloadResult}'");
            Console.WriteLine($"  DownloadError: '{item.DownloadError}'");
            Console.WriteLine($"  LaunchResult: '{item.LaunchResult}'");
            Console.WriteLine($"  Получено: {item.ReceivedAt}");
            Console.WriteLine("--------------------------------------");
        }
    }

    Console.WriteLine("\nОжидание следующих данных...");
}

app.Run("http://localhost:7268");