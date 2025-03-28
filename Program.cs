using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

public class TaskItem
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public string Text { get; set; } = string.Empty; 
    public DateTime ReminderTime { get; set; }
}

class Program
{
    private static List<TaskItem> tasks = new();
    private const string DataFilePath = "tasks.json";
    private static ITelegramBotClient? botClient; 
    private static Timer? timer; 

    static async Task Main(string[] args)
    {
        LoadTasks();
        // бот который был использован во 2 задание вызова
        botClient = new TelegramBotClient("7817714746:AAGwFvW8m70NUawfgpo6oedS41aH3_rTOFA");
        
        timer = new Timer(CheckReminders, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));

        if (botClient != null)
        {
            botClient.StartReceiving(UpdateHandler, ErrorHandler);
            Console.WriteLine("БОТ ВЫПОЛНЯЕТ СВОЮ ФУНКЦИЮ");
            await Task.Delay(-1);
        }
    }

    private static async Task UpdateHandler(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        if (update.Message is not { Text: { } messageText } || update.Message.From is null)
            return;

        var userId = update.Message.From.Id;
        var chatId = update.Message.Chat.Id;

        try
        {
            if (messageText.StartsWith("/add"))
                await HandleAddCommand(messageText, userId, chatId);
            else if (messageText.StartsWith("/list"))
                await HandleListCommand(userId, chatId);
            else if (messageText.StartsWith("/delete"))
                await HandleDeleteCommand(messageText, userId, chatId);
        }
        catch (Exception ex)
        {
            if (botClient != null)
                await botClient.SendTextMessageAsync(chatId, $"Ошибка: {ex.Message}");
        }
    }

    private static Task ErrorHandler(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Console.WriteLine($"Ошибка: {ex.Message}");
        return Task.CompletedTask;
    }

    private static async Task HandleAddCommand(string messageText, long userId, long chatId)
    {
        var parts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            await SendMessage(chatId, "Формат: /add [текст] [ЧЧ:MM]");
            return;
        }

        var timePart = parts.Last();
        if (!DateTime.TryParseExact(timePart, "HH:mm", null, System.Globalization.DateTimeStyles.None, out var reminderTime))
        {
            await SendMessage(chatId, "❌Неверный формат времени. Используйте ЧЧ:MM❌");
            return;
        }

        var taskText = string.Join(' ', parts[1..^1]);
        AddTask(userId, taskText, reminderTime);
        await SendMessage(chatId, "✅ Задача добавлена");
    }

    private static async Task HandleListCommand(long userId, long chatId)
    {
        var userTasks = tasks.Where(t => t.UserId == userId).ToList();
        if (!userTasks.Any())
        {
            await SendMessage(chatId, "Сегодня ты не заполнял список задач солнышко");
            return;
        }

        var response = "Ваши задачи:\n" + string.Join("\n", 
            userTasks.Select(t => $"#{t.Id}: {t.Text} - {t.ReminderTime:HH:mm}"));
        
        await SendMessage(chatId, response);
    }

    private static async Task HandleDeleteCommand(string messageText, long userId, long chatId)
    {
        var parts = messageText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var taskId))
        {
            await SendMessage(chatId, "Формат: /delete [номер задачи]");
            return;
        }

        var task = tasks.FirstOrDefault(t => t.Id == taskId && t.UserId == userId);
        if (task == null)
        {
            await SendMessage(chatId, "❌Задача не найдена❌");
            return;
        }

        tasks.Remove(task);
        SaveTasks();
        await SendMessage(chatId, "💐Задача удалена(спасибо, что используешь именно меня)💐");
    }

    private static async Task SendMessage(long chatId, string text)
    {
        if (botClient != null)
            await botClient.SendTextMessageAsync(chatId, text);
    }

    private static void CheckReminders(object? state) 
    {
        var now = DateTime.Now;
        foreach (var task in tasks.Where(t => t.ReminderTime <= now).ToList())
        {
            if (botClient != null && task.UserId != 0)
                botClient.SendTextMessageAsync(task.UserId, $"💥 Напоминание: {task.Text}💥").Wait();
            
            tasks.Remove(task);
        }
        if (tasks.Count > 0) SaveTasks();
    }

    private static void AddTask(long userId, string text, DateTime time)
    {
        var newId = tasks.Any() ? tasks.Max(t => t.Id) + 1 : 1;
        tasks.Add(new TaskItem { Id = newId, UserId = userId, Text = text, ReminderTime = time });
        SaveTasks();
    }

    private static void LoadTasks()
    {
        if (System.IO.File.Exists(DataFilePath))
            tasks = JsonSerializer.Deserialize<List<TaskItem>>(System.IO.File.ReadAllText(DataFilePath)) ?? new List<TaskItem>();
    }

    private static void SaveTasks()
    {
        System.IO.File.WriteAllText(DataFilePath, JsonSerializer.Serialize(tasks));
    }
}