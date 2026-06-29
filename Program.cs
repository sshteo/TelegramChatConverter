using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using HtmlAgilityPack;


namespace TelegramChatConverter;

class Program
{
    private const int MAX_MESSAGES_PER_FILE = 50000;
    private static List<Message> allMessages = new();
    private static string? outputFolder;
    private static string? fileNameBase;
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Telegram HTML to TXT Converter by shteo ===\n");

        // 1. Выбор папки с экспортом
string? folderPath = null;

// Проверяем, передан ли путь через аргументы командной строки
if (args.Length > 0 && Directory.Exists(args[0]))
{
    folderPath = args[0];
    Console.WriteLine($"Использую папку из аргументов: {folderPath}");
}
else
{
    Console.WriteLine("Выберите папку с экспортом Telegram...");
    Console.WriteLine("💡 Подсказка: скопируйте путь к папке в проводнике (в адресе не должны быть пробелы и символы кроме английского языка)");
    Console.WriteLine("   (например: D:\\Downloads\\Telegram Desktop\\ChatExport_2026-06-29)");
    
    while (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
    {
        if (!string.IsNullOrEmpty(folderPath))
            Console.WriteLine($"❌ Папка '{folderPath}' не найдена.");
        
        Console.Write("\nВведите путь к папке с экспортом: ");
        folderPath = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(folderPath))
        {
            folderPath = Directory.GetCurrentDirectory();
            Console.WriteLine($"Использую текущую папку: {folderPath}");
            break;
        }
    }
}

// Проверяем, есть ли файлы в папке
if (!Directory.Exists(folderPath))
{
    Console.WriteLine($"❌ Папка '{folderPath}' не существует.");
    Console.WriteLine("Нажмите любую клавишу для выхода...");
    Console.ReadKey();
    return;
}
        // Проверяем, есть ли файлы в папке
        if (!Directory.Exists(folderPath))
        {
            Console.WriteLine($"❌ Папка '{folderPath}' не существует.");
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
            return;
        }

        // 2. Выбор места сохранения
        Console.Write("\nВведите папку для сохранения результатов (Enter = та же папка): ");
        outputFolder = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(outputFolder))
            outputFolder = folderPath;
        
        if (!Directory.Exists(outputFolder))
        {
            Directory.CreateDirectory(outputFolder);
            Console.WriteLine($"Создана папка: {outputFolder}");
        }

        // 3. Ввод названия файла
        Console.Write("Введите название для файлов (Enter = chat_export): ");
        fileNameBase = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(fileNameBase))
            fileNameBase = "chat_export";

        // 4. Поиск и сортировка файлов
        var htmlFiles = Directory.GetFiles(folderPath, "messages*.html")
            .OrderBy(f => ExtractNumber(f))
            .ToList();

        if (htmlFiles.Count == 0)
        {
            Console.WriteLine($"❌ Файлы messages*.html не найдены в папке: {folderPath}");
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
            return;
        }

        Console.WriteLine($"\n📁 Найдено HTML-файлов: {htmlFiles.Count}");
        Console.WriteLine("Начинаю обработку...\n");

        // 5. Парсинг всех сообщений
        int processedFiles = 0;
        
        foreach (var htmlFile in htmlFiles)
        {
            try
            {
                var messages = await ParseHtmlFileAsync(htmlFile);
                if (messages.Count > 0)
                {
                    allMessages.AddRange(messages);
                    processedFiles++;
                    
                    if (processedFiles % 10 == 0)
                    {
                        Console.Write($"\rОбработано файлов: {processedFiles}/{htmlFiles.Count}, собрано сообщений: {allMessages.Count:N0}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n  Ошибка в файле {Path.GetFileName(htmlFile)}: {ex.Message}");
            }
        }
        
        Console.WriteLine($"\rОбработано файлов: {processedFiles}/{htmlFiles.Count}, собрано сообщений: {allMessages.Count:N0}");
        Console.WriteLine();

        if (allMessages.Count == 0)
        {
            Console.WriteLine("❌ Не найдено ни одного сообщения.");
            Console.WriteLine("Нажмите любую клавишу для выхода...");
            Console.ReadKey();
            return;
        }

        // 6. Меню действий
        bool exit = false;
        while (!exit)
        {
            Console.WriteLine("\n=== МЕНЮ ===");
            Console.WriteLine("1. Сохранить в TXT (с разбивкой на части по 50000 символов)");
            Console.WriteLine("2. Сохранить в DOCX (один файл)");
            Console.WriteLine("3. Сохранить в JSON");
            Console.WriteLine("4. Сохранить в CSV (с разделением даты и времени)");
            Console.WriteLine("5. Показать статистику чата");
            Console.WriteLine("6. Экспорт по дате (фильтрация)");
            Console.WriteLine("7. ВСЁ СРАЗУ (все форматы + статистика)");
            Console.WriteLine("0. Выход");
            Console.Write("\nВаш выбор: ");
            
            string? choice = Console.ReadLine()?.Trim();
            Console.WriteLine();

            switch (choice)
            {
                case "1":
                    await SaveAsTxtAsync();
                    break;
                case "2":
                    await SaveAsDocxAsync();
                    break;
                case "3":
                    await SaveAsJsonAsync();
                    break;
                case "4":
                    await SaveAsCsvAsync();
                    break;
                case "5":
                    ShowStatistics();
                    break;
                case "6":
                    await ExportByDateAsync();
                    break;
                case "7":
                    await SaveAllFormatsAsync();
                    break;
                case "0":
                    exit = true;
                    Console.WriteLine("До свидания! shteo благодарит Вас");
                    break;
                default:
                    Console.WriteLine("Неверный выбор. Попробуйте снова.");
                    break;
            }
        }
    }

    // ========== ОСНОВНЫЕ ФУНКЦИИ ==========

    static async Task SaveAsTxtAsync()
    {
        int fileCounter = 1;
        int totalWritten = 0;
        
        Console.WriteLine($"\nСохраняю в TXT (по {MAX_MESSAGES_PER_FILE} сообщений)...");
        
        while (totalWritten < allMessages.Count)
        {
            var chunk = allMessages.Skip(totalWritten).Take(MAX_MESSAGES_PER_FILE).ToList();
            
            string outputFile;
            if (fileCounter == 1)
                outputFile = Path.Combine(outputFolder!, $"{fileNameBase}.txt");
            else
                outputFile = Path.Combine(outputFolder!, $"{fileNameBase}_part{fileCounter}.txt");
            
            await WriteMessagesToTxtAsync(outputFile, chunk);
            
            long fileSizeKB = new FileInfo(outputFile).Length / 1024;
            Console.WriteLine($"  Часть {fileCounter}: {Path.GetFileName(outputFile)} ({chunk.Count} сообщений, {fileSizeKB:N0} КБ)");
            
            totalWritten += chunk.Count;
            fileCounter++;
        }

        Console.WriteLine($"\n✅ TXT сохранён! Всего файлов: {fileCounter - 1}");
    }

    static async Task SaveAsDocxAsync()
    {
        Console.WriteLine("\n📄 Создаю DOCX-файл...");
        
        string outputFile = Path.Combine(outputFolder!, $"{fileNameBase}.docx");
        
        await Task.Run(() =>
        {
            using (var wordDocument = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(outputFile, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                var body = mainPart.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());

                // Заголовок
                var titleParagraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
                var titleRun = new DocumentFormat.OpenXml.Wordprocessing.Run();
                titleRun.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                titleRun.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "28" });
                titleRun.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text($"Экспорт чата Telegram\nВсего сообщений: {allMessages.Count:N0}\nДата: {DateTime.Now:dd.MM.yyyy}\n"));
                titleParagraph.AppendChild(titleRun);
                body.AppendChild(titleParagraph);

                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new DocumentFormat.OpenXml.Wordprocessing.Text(new string('-', 80)))));

                int messageCounter = 0;
                int progressStep = Math.Max(1000, allMessages.Count / 100);
                
                foreach (var msg in allMessages)
                {
                    messageCounter++;
                    
                    var headerParagraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
                    var headerRun = new DocumentFormat.OpenXml.Wordprocessing.Run();
                    headerRun.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                    headerRun.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text($"{msg.Date} / {msg.Sender}"));
                    headerParagraph.AppendChild(headerRun);
                    body.AppendChild(headerParagraph);

                    var textParagraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
                    var textRun = new DocumentFormat.OpenXml.Wordprocessing.Run();
                    textRun.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Text($"{msg.Text};"));
                    textParagraph.AppendChild(textRun);
                    body.AppendChild(textParagraph);

                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph());

                    if (messageCounter % progressStep == 0)
                    {
                        int percent = (int)((double)messageCounter / allMessages.Count * 100);
                        Console.Write($"\r  Прогресс: {percent}% ({messageCounter:N0}/{allMessages.Count:N0})");
                    }
                }

                Console.Write($"\r  Прогресс: 100% ({allMessages.Count:N0}/{allMessages.Count:N0})");
                Console.WriteLine();
            }
        });

        long fileSizeKB = new FileInfo(outputFile).Length / 1024;
        Console.WriteLine($"\n✅ DOCX сохранён!");
        Console.WriteLine($"  Файл: {Path.GetFileName(outputFile)}");
        Console.WriteLine($"  Размер: {fileSizeKB:N0} КБ ({fileSizeKB / 1024:N2} МБ)");
    }

    static async Task SaveAsJsonAsync()
    {
        Console.WriteLine("\n📄 Создаю JSON-файл...");
        
        string outputFile = Path.Combine(outputFolder!, $"{fileNameBase}.json");
        
        var jsonData = new
        {
            exportDate = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
            totalMessages = allMessages.Count,
            messages = allMessages
        };
        
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        
        string json = JsonSerializer.Serialize(jsonData, options);
        await File.WriteAllTextAsync(outputFile, json, Encoding.UTF8);
        
        long fileSizeKB = new FileInfo(outputFile).Length / 1024;
        Console.WriteLine($"✅ JSON сохранён!");
        Console.WriteLine($"  Файл: {Path.GetFileName(outputFile)}");
        Console.WriteLine($"  Размер: {fileSizeKB:N0} КБ");
    }

    static async Task SaveAsCsvAsync()
{
    Console.WriteLine("\n📄 Создаю CSV-файл...");
    
    string outputFile = Path.Combine(outputFolder!, $"{fileNameBase}.csv");
    
    var csv = new StringBuilder();
    
    // Заголовки столбцов (каждый в отдельной ячейке)
    csv.AppendLine("Дата;Время;Отправитель;Сообщение");
    
    int counter = 0;
    int progressStep = Math.Max(1000, allMessages.Count / 100);
    
    foreach (var msg in allMessages)
    {
        counter++;
        
        // Разделяем дату и время
        string date = "";
        string time = "";
        
        if (!string.IsNullOrEmpty(msg.Date))
        {
            var parts = msg.Date.Split(' ');
            if (parts.Length >= 2)
            {
                date = parts[0]; // "23.06.2023"
                time = parts[1]; // "13:34"
            }
            else
            {
                date = msg.Date;
            }
        }
        
        // Экранируем кавычки и точки с запятой в сообщении
        string text = msg.Text
            .Replace(";", ",")  // Заменяем ; на , чтобы не ломать CSV
            .Replace("\"", "\"\""); // Экранируем кавычки
        
        // Записываем строку с разделителями ;
        csv.AppendLine($"{date};{time};{msg.Sender};{text}");
        
        if (counter % progressStep == 0)
        {
            int percent = (int)((double)counter / allMessages.Count * 100);
            Console.Write($"\r  Прогресс: {percent}%");
        }
    }
    
    Console.Write($"\r  Прогресс: 100%");
    Console.WriteLine();
    
    await File.WriteAllTextAsync(outputFile, csv.ToString(), Encoding.UTF8);
    
    long fileSizeKB = new FileInfo(outputFile).Length / 1024;
    Console.WriteLine($"✅ CSV сохранён!");
    Console.WriteLine($"  Файл: {Path.GetFileName(outputFile)}");
    Console.WriteLine($"  Размер: {fileSizeKB:N0} КБ");
}

    static void ShowStatistics()
    {
        Console.WriteLine("\n📊 СТАТИСТИКА ЧАТА");
        Console.WriteLine(new string('=', 50));
        
        Console.WriteLine($"\n📝 Общее количество сообщений: {allMessages.Count:N0}");
        
        var dates = allMessages.Select(m => m.Date.Split(' ')[0]).ToList();
        var dateGroups = dates.GroupBy(d => d).OrderBy(g => g.Key);
        
        Console.WriteLine($"\n📅 Период: {dateGroups.First().Key} — {dateGroups.Last().Key}");
        Console.WriteLine($"   Всего дней: {dateGroups.Count()}");
        
        Console.WriteLine("\n📆 Самые активные дни (топ-10):");
        foreach (var group in dateGroups.OrderByDescending(g => g.Count()).Take(10))
        {
            Console.WriteLine($"   {group.Key}: {group.Count():N0} сообщений");
        }
        
        var senderGroups = allMessages.GroupBy(m => m.Sender)
            .OrderByDescending(g => g.Count())
            .ToList();
        
        Console.WriteLine($"\n👥 Участников: {senderGroups.Count}");
        Console.WriteLine("\n🏆 Активность участников:");
        int rank = 1;
        foreach (var group in senderGroups.Take(10))
        {
            double percent = (double)group.Count() / allMessages.Count * 100;
            Console.WriteLine($"   {rank}. {group.Key}: {group.Count():N0} сообщений ({percent:F1}%)");
            rank++;
        }
        
        int textMessages = allMessages.Count(m => !m.Text.StartsWith("["));
        int mediaMessages = allMessages.Count - textMessages;
        
        Console.WriteLine($"\n📎 Типы сообщений:");
        Console.WriteLine($"   Текстовые: {textMessages:N0} ({100.0 * textMessages / allMessages.Count:F1}%)");
        Console.WriteLine($"   Медиа: {mediaMessages:N0} ({100.0 * mediaMessages / allMessages.Count:F1}%)");
        
        double avgLength = allMessages.Average(m => m.Text.Length);
        Console.WriteLine($"\n📏 Средняя длина сообщения: {avgLength:F0} символов");
        
        var longest = allMessages.OrderByDescending(m => m.Text.Length).First();
        Console.WriteLine($"\n📄 Самое длинное сообщение: {longest.Text.Length} символов");
        Console.WriteLine($"   От: {longest.Sender}");
        Console.WriteLine($"   Дата: {longest.Date}");
        Console.WriteLine($"   Текст: {longest.Text.Substring(0, Math.Min(100, longest.Text.Length))}...");
        
        Console.WriteLine("\n" + new string('=', 50));
    }

    static async Task ExportByDateAsync()
    {
        Console.WriteLine("\n📅 Экспорт по дате");
        Console.Write("Введите дату в формате ДД.ММ.ГГГГ (например, 23.06.2023): ");
        string? dateInput = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(dateInput))
        {
            Console.WriteLine("Дата не введена.");
            return;
        }
        
        var filtered = allMessages.Where(m => m.Date.StartsWith(dateInput)).ToList();
        
        if (filtered.Count == 0)
        {
            Console.WriteLine($"Сообщений за {dateInput} не найдено.");
            return;
        }
        
        Console.WriteLine($"Найдено сообщений: {filtered.Count:N0}");
        
        Console.Write("Введите название файла (Enter = default): ");
        string? fileName = Console.ReadLine()?.Trim();
        if (string.IsNullOrEmpty(fileName))
            fileName = $"export_{dateInput.Replace(".", "_")}";
        
        string outputFile = Path.Combine(outputFolder!, $"{fileName}.txt");
        await WriteMessagesToTxtAsync(outputFile, filtered);
        
        Console.WriteLine($"✅ Сохранено в: {outputFile}");
    }

    static async Task SaveAllFormatsAsync()
    {
        Console.WriteLine("\n📦 Сохраняю ВСЕ форматы...");
        Console.WriteLine(new string('-', 40));
        
        await SaveAsTxtAsync();
        Console.WriteLine(new string('-', 40));
        await SaveAsDocxAsync();
        Console.WriteLine(new string('-', 40));
        await SaveAsJsonAsync();
        Console.WriteLine(new string('-', 40));
        await SaveAsCsvAsync();
        Console.WriteLine(new string('-', 40));
        ShowStatistics();
        
        Console.WriteLine("\n✅ Все форматы сохранены!");
        Console.WriteLine($"📂 Папка: {outputFolder}");
    }

    // ========== ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ ==========

    static int ExtractNumber(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        
        if (fileName.Equals("messages", StringComparison.OrdinalIgnoreCase))
            return 0;
        
        var match = Regex.Match(fileName, @"messages(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
            return number;
        
        return int.MaxValue;
    }

    static async Task WriteMessagesToTxtAsync(string filePath, List<Message> messages)
    {
        using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
        
        foreach (var msg in messages)
        {
            await writer.WriteLineAsync($"{msg.Date} / {msg.Sender}");
            await writer.WriteLineAsync($"{msg.Text};");
            await writer.WriteLineAsync();
        }
    }

    static async Task<List<Message>> ParseHtmlFileAsync(string filePath)
    {
        var messages = new List<Message>();
        
        var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var messageNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'message')]");
        
        if (messageNodes == null) 
            return messages;

        string currentDate = "";
        string currentSender = "";

        foreach (var node in messageNodes)
        {
            try
            {
                if (node.GetAttributeValue("class", "").Contains("service"))
                {
                    var dateNode = node.SelectSingleNode(".//div[contains(@class, 'details')]");
                    if (dateNode != null)
                    {
                        currentDate = dateNode.InnerText.Trim();
                    }
                    continue;
                }

                var dateElement = node.SelectSingleNode(".//div[contains(@class, 'date')]");
                var senderElement = node.SelectSingleNode(".//div[contains(@class, 'from_name')]");
                var textElement = node.SelectSingleNode(".//div[contains(@class, 'text')]");

                string time = dateElement?.InnerText.Trim() ?? "";
                
                string sender = senderElement?.InnerText.Trim() ?? "";
                if (!string.IsNullOrEmpty(sender))
                    currentSender = sender;

                string messageText = "";
                if (textElement != null)
                {
                    var mediaWrap = textElement.SelectSingleNode(".//div[contains(@class, 'media_wrap')]");
                    if (mediaWrap != null)
                    {
                        var title = mediaWrap.SelectSingleNode(".//div[contains(@class, 'title')]");
                        if (title != null && title.InnerText.Contains("Sticker"))
                            messageText = "[Стикер]";
                        else if (title != null && title.InnerText.Contains("Photo"))
                            messageText = "[Фото]";
                        else
                            messageText = "[Медиа]";
                    }
                    else
                    {
                        messageText = textElement.InnerText.Trim();
                        
                        if (messageText.Contains("In reply to"))
                        {
                            var replyPart = textElement.SelectSingleNode(".//div[contains(@class, 'reply_to')]");
                            if (replyPart != null)
                            {
                                var textNodes = textElement.SelectNodes("./text()");
                                if (textNodes != null)
                                {
                                    var actualText = string.Join("", textNodes.Select(n => n.InnerText.Trim()));
                                    if (!string.IsNullOrEmpty(actualText))
                                        messageText = actualText;
                                }
                            }
                        }
                    }
                }

                string fullDate = currentDate;
                if (!string.IsNullOrEmpty(time))
                {
                    var dateParts = currentDate.Split(' ');
                    if (dateParts.Length >= 3)
                    {
                        var day = dateParts[0];
                        var month = GetMonthNumber(dateParts[1]);
                        var year = dateParts[2];
                        fullDate = $"{day}.{month}.{year}";
                    }
                    
                    if (!string.IsNullOrEmpty(time))
                        fullDate = $"{fullDate} {time}";
                }

                if (string.IsNullOrEmpty(currentSender))
                    currentSender = "Unknown";

                if (string.IsNullOrEmpty(messageText))
                    continue;

                messages.Add(new Message
                {
                    Date = fullDate,
                    Sender = currentSender,
                    Text = messageText
                });
            }
            catch
            {
                // Пропускаем проблемное сообщение
            }
        }

        return messages;
    }

    static string GetMonthNumber(string monthName)
    {
        var months = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"January", "01"}, {"February", "02"}, {"March", "03"},
            {"April", "04"}, {"May", "05"}, {"June", "06"},
            {"July", "07"}, {"August", "08"}, {"September", "09"},
            {"October", "10"}, {"November", "11"}, {"December", "12"}
        };
        
        return months.TryGetValue(monthName, out var num) ? num : monthName;
    }
}

class Message
{
    public string Date { get; set; } = "";
    public string Sender { get; set; } = "";
    public string Text { get; set; } = "";
}