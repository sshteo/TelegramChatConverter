using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace TelegramChatConverter;

class Program
{
    private const int MAX_MESSAGES_PER_FILE = 50000;
    
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Telegram HTML to TXT Converter ===\n");

        string? folderPath = args.Length > 0 ? args[0] : null;
        
        while (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            if (!string.IsNullOrEmpty(folderPath))
                Console.WriteLine($"Папка '{folderPath}' не найдена.");
            
            Console.Write("Введите путь к папке с экспортом Telegram: ");
            folderPath = Console.ReadLine()?.Trim();
            
            if (string.IsNullOrEmpty(folderPath))
            {
                folderPath = Directory.GetCurrentDirectory();
                Console.WriteLine($"Использую текущую папку: {folderPath}");
                break;
            }
        }

        // Выбор формата
        Console.WriteLine("\nВыберите формат сохранения:");
        Console.WriteLine("  1. TXT (простой текст, разбивается на части)");
        Console.WriteLine("  2. DOCX (Microsoft Word, один файл)");
        Console.Write("Ваш выбор (1 или 2): ");
        string? formatChoice = Console.ReadLine()?.Trim();
        
        bool useDocx = formatChoice == "2";
        
        if (useDocx)
            Console.WriteLine("\n📄 Выбран формат DOCX (Word) — отлично подходит для больших чатов!");
        else
            Console.WriteLine("\n📄 Выбран формат TXT");

        // ⭐ ГЛАВНОЕ ИЗМЕНЕНИЕ ЗДЕСЬ — натуральная сортировка
        var htmlFiles = Directory.GetFiles(folderPath, "messages*.html")
            .OrderBy(f => ExtractNumber(f))
            .ToList();

        if (htmlFiles.Count == 0)
        {
            Console.WriteLine("Файлы messages*.html не найдены в указанной папке.");
            return;
        }

        Console.WriteLine($"\nНайдено HTML-файлов: {htmlFiles.Count}");
        Console.WriteLine("Порядок файлов (первые 10):");
        for (int i = 0; i < Math.Min(10, htmlFiles.Count); i++)
        {
            Console.WriteLine($"  {i+1}. {Path.GetFileName(htmlFiles[i])}");
        }
        if (htmlFiles.Count > 10)
            Console.WriteLine($"  ... и еще {htmlFiles.Count - 10} файлов");
        
        Console.WriteLine("\nНачинаю обработку...\n");

        var allMessages = new List<Message>();
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
            Console.WriteLine("Не найдено ни одного сообщения.");
            return;
        }

        // Сохраняем в выбранном формате
        if (useDocx)
        {
            await SaveAsDocxAsync(folderPath, allMessages);
        }
        else
        {
            await SaveAsTxtAsync(folderPath, allMessages);
        }

        Console.WriteLine("\nНажмите любую клавишу для выхода...");
        Console.ReadKey();
    }

    // ⭐ НОВЫЙ МЕТОД — извлекает номер из имени файла
    static int ExtractNumber(string filePath)
    {
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        
        // messages.html — это первый файл (номер 0)
        if (fileName.Equals("messages", StringComparison.OrdinalIgnoreCase))
            return 0;
        
        // Ищем число в названии (messages123.html)
        var match = Regex.Match(fileName, @"messages(\d+)", RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
            return number;
        
        // Если что-то пошло не так — ставим в конец
        return int.MaxValue;
    }

    static async Task SaveAsTxtAsync(string folderPath, List<Message> allMessages)
    {
        int fileCounter = 1;
        int totalWritten = 0;
        var fileNames = new List<string>();
        
        Console.WriteLine($"\nСохраняю в TXT (по {MAX_MESSAGES_PER_FILE} сообщений)...");
        
        while (totalWritten < allMessages.Count)
        {
            var chunk = allMessages.Skip(totalWritten).Take(MAX_MESSAGES_PER_FILE).ToList();
            
            string outputFile;
            if (fileCounter == 1)
                outputFile = Path.Combine(folderPath, "chat_export.txt");
            else
                outputFile = Path.Combine(folderPath, $"chat_export_part{fileCounter}.txt");
            
            await WriteMessagesToTxtAsync(outputFile, chunk);
            
            long fileSizeKB = new FileInfo(outputFile).Length / 1024;
            Console.WriteLine($"  Часть {fileCounter}: {Path.GetFileName(outputFile)} ({chunk.Count} сообщений, {fileSizeKB:N0} КБ)");
            
            fileNames.Add(Path.GetFileName(outputFile));
            totalWritten += chunk.Count;
            fileCounter++;
        }

        Console.WriteLine($"\n✅ Готово!");
        Console.WriteLine($"📁 Создано TXT-файлов: {fileCounter - 1}");
        Console.WriteLine($"📝 Всего сообщений: {totalWritten:N0}");
        Console.WriteLine($"📂 Файлы сохранены в: {folderPath}");
    }

    static async Task SaveAsDocxAsync(string folderPath, List<Message> allMessages)
    {
        Console.WriteLine("\n📄 Создаю DOCX-файл...");
        
        string outputFile = Path.Combine(folderPath, "chat_export.docx");
        
        await Task.Run(() =>
        {
            using (var wordDocument = WordprocessingDocument.Create(outputFile, WordprocessingDocumentType.Document))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                // Заголовок
                var titleParagraph = new Paragraph();
                var titleRun = new Run();
                titleRun.AppendChild(new Bold());
                titleRun.AppendChild(new FontSize { Val = "28" });
                titleRun.AppendChild(new Text($"Экспорт чата Telegram\nВсего сообщений: {allMessages.Count:N0}\nДата: {DateTime.Now:dd.MM.yyyy}\n"));
                titleParagraph.AppendChild(titleRun);
                body.AppendChild(titleParagraph);

                body.AppendChild(new Paragraph(new Run(new Text(new string('-', 80)))));

                int messageCounter = 0;
                int progressStep = Math.Max(1000, allMessages.Count / 100);
                
                foreach (var msg in allMessages)
                {
                    messageCounter++;
                    
                    // Дата и отправитель (жирным)
                    var headerParagraph = new Paragraph();
                    var headerRun = new Run();
                    headerRun.AppendChild(new Bold());
                    headerRun.AppendChild(new Text($"{msg.Date} / {msg.Sender}"));
                    headerParagraph.AppendChild(headerRun);
                    body.AppendChild(headerParagraph);

                    // Текст сообщения
                    var textParagraph = new Paragraph();
                    var textRun = new Run();
                    textRun.AppendChild(new Text($"{msg.Text};"));
                    textParagraph.AppendChild(textRun);
                    body.AppendChild(textParagraph);

                    body.AppendChild(new Paragraph());

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
        Console.WriteLine($"\n✅ Готово!");
        Console.WriteLine($"📄 Файл: {Path.GetFileName(outputFile)}");
        Console.WriteLine($"📝 Сообщений: {allMessages.Count:N0}");
        Console.WriteLine($"📦 Размер: {fileSizeKB:N0} КБ ({fileSizeKB / 1024:N2} МБ)");
        Console.WriteLine($"📂 Сохранен в: {folderPath}");
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