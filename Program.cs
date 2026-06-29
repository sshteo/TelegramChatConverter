using System.Text;
using HtmlAgilityPack;

namespace TelegramChatConverter;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Telegram HTML to TXT Converter ===\n");

        // Получаем путь к папке с экспортом
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

        // Ищем все файлы messages*.html
        var htmlFiles = Directory.GetFiles(folderPath, "messages*.html")
            .OrderBy(f => f)
            .ToList();

        if (htmlFiles.Count == 0)
        {
            Console.WriteLine("Файлы messages*.html не найдены в указанной папке.");
            Console.WriteLine($"Поиск в: {folderPath}");
            return;
        }

        Console.WriteLine($"Найдено HTML-файлов: {htmlFiles.Count}");
        Console.WriteLine();

        // Создаём один общий TXT-файл
        string outputFile = Path.Combine(folderPath, "chat_export.txt");
        
        using var writer = new StreamWriter(outputFile, false, Encoding.UTF8);
        
        int totalMessages = 0;
        int totalFiles = 0;

        foreach (var htmlFile in htmlFiles)
        {
            Console.WriteLine($"Обработка: {Path.GetFileName(htmlFile)}");
            
            try
            {
                var messages = await ParseHtmlFileAsync(htmlFile);
                totalMessages += messages.Count;
                
                if (messages.Count > 0)
                {
                    totalFiles++;
                    
                    foreach (var msg in messages)
                    {
                        await writer.WriteLineAsync($"{msg.Date} / {msg.Sender}");
                        await writer.WriteLineAsync($"{msg.Text};");
                        await writer.WriteLineAsync(); // Пустая строка между сообщениями
                    }
                }
                
                Console.WriteLine($"  Добавлено сообщений: {messages.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Ошибка при обработке: {ex.Message}");
            }
        }

        Console.WriteLine($"\n✅ Готово!");
        Console.WriteLine($"Обработано файлов: {totalFiles}");
        Console.WriteLine($"Всего сообщений: {totalMessages}");
        Console.WriteLine($"Результат сохранён в: {outputFile}");
        
        Console.WriteLine("\nНажмите любую клавишу для выхода...");
        Console.ReadKey();
    }

    static async Task<List<Message>> ParseHtmlFileAsync(string filePath)
    {
        var messages = new List<Message>();
        
        var html = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Находим все блоки сообщений
        var messageNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'message')]");
        
        if (messageNodes == null) 
            return messages;

        string currentDate = "";
        string currentSender = "";

        foreach (var node in messageNodes)
        {
            // Проверяем служебное сообщение с датой
            if (node.GetAttributeValue("class", "").Contains("service"))
            {
                var dateNode = node.SelectSingleNode(".//div[contains(@class, 'details')]");
                if (dateNode != null)
                {
                    currentDate = dateNode.InnerText.Trim();
                }
                continue;
            }

            // Обычное сообщение
            var dateElement = node.SelectSingleNode(".//div[contains(@class, 'date')]");
            var senderElement = node.SelectSingleNode(".//div[contains(@class, 'from_name')]");
            var textElement = node.SelectSingleNode(".//div[contains(@class, 'text')]");

            // Получаем дату (время)
            string time = dateElement?.InnerText.Trim() ?? "";
            
            // Получаем отправителя
            string sender = senderElement?.InnerText.Trim() ?? "";
            if (!string.IsNullOrEmpty(sender))
                currentSender = sender;

            // Получаем текст сообщения
            string messageText = "";
            if (textElement != null)
            {
                // Проверяем, не является ли сообщение стикером или медиа
                var mediaWrap = textElement.SelectSingleNode(".//div[contains(@class, 'media_wrap')]");
                if (mediaWrap != null)
                {
                    // Это медиа-сообщение, пропускаем или отмечаем как [Медиа]
                    var title = mediaWrap.SelectSingleNode(".//div[contains(@class, 'title')]");
                    if (title != null && title.InnerText.Contains("Sticker"))
                    {
                        messageText = "[Стикер]";
                    }
                    else if (title != null && title.InnerText.Contains("Photo"))
                    {
                        messageText = "[Фото]";
                    }
                    else
                    {
                        messageText = "[Медиа]";
                    }
                }
                else
                {
                    // Текстовое сообщение - очищаем от лишних тегов
                    messageText = textElement.InnerText.Trim();
                    
                    // Убираем ссылки типа "In reply to this message"
                    if (messageText.Contains("In reply to"))
                    {
                        var replyPart = textElement.SelectSingleNode(".//div[contains(@class, 'reply_to')]");
                        if (replyPart != null)
                        {
                            var replyText = replyPart.InnerText.Trim();
                            // Оставляем только сам текст после ответа
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

            // Формируем дату для сообщения
            string fullDate = currentDate;
            if (!string.IsNullOrEmpty(time))
            {
                // Если есть время, добавляем его к дате
                if (!string.IsNullOrEmpty(fullDate))
                {
                    // Парсим дату из service сообщения и добавляем время
                    // В HTML время указано в формате "13:34", дата - "23 June 2023"
                    // Преобразуем в читаемый формат
                    var dateParts = fullDate.Split(' ');
                    if (dateParts.Length >= 3)
                    {
                        // Формат: "23 June 2023" -> "23.06.2023"
                        var day = dateParts[0];
                        var month = GetMonthNumber(dateParts[1]);
                        var year = dateParts[2];
                        fullDate = $"{day}.{month}.{year}";
                    }
                }
                
                if (!string.IsNullOrEmpty(time))
                    fullDate = $"{fullDate} {time}";
            }

            // Если нет отправителя, используем последнего известного
            if (string.IsNullOrEmpty(currentSender))
                currentSender = "Unknown";

            // Пропускаем пустые сообщения
            if (string.IsNullOrEmpty(messageText))
                continue;

            messages.Add(new Message
            {
                Date = fullDate,
                Sender = currentSender,
                Text = messageText
            });
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