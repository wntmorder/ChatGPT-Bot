using Telegram.Bot;
using Telegram.Bot.Types;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Audio;
using Microsoft.Extensions.Configuration;

namespace Bot
{
    public class Program
    {
        private static IConfiguration configuration;

        private static TelegramBotClient telegramBotClient;
        private static OpenAIClient openAIClient;

        private static readonly string audioPath = "voice_message.wav";

        private static List<OpenAI.Chat.Message> messages = new List<OpenAI.Chat.Message>();
        
        public static async Task Main(string[] args)
        {
            configuration = BuildConfiguration();

            InitializeClients();

            telegramBotClient.StartReceiving(Update, Error);
            Console.WriteLine("Bot started.");
            await Task.Delay(-1);
        }

        private static IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
        }

        private static void InitializeClients()
        {
            string apiTelegram = configuration["TelegramApiKey"];
            string apiGpt = configuration["GptApiKey"];

            telegramBotClient = new TelegramBotClient(apiTelegram);
            openAIClient = new OpenAIClient(apiGpt);
        }

        private static async Task Update(ITelegramBotClient botClient, Update update, CancellationToken token)
        {
            try
            {
                if (update.Message.Voice != null)
                {
                    await DownloadVoiceMessage(update);

                    var request = new AudioTranscriptionRequest(Path.GetFullPath(audioPath), language: "ru");
                    var result = await openAIClient.AudioEndpoint.CreateTranscriptionAsync(request);

                    var message = new OpenAI.Chat.Message(Role.User, result);
                    messages.Add(message);
                }
                else if (update.Message.Text != null)
                {
                    if (update.Message.Text.StartsWith("/start"))
                    {
                        await HandleStartCommand(botClient, update);
                    }
                    else
                    {
                        var message = new OpenAI.Chat.Message(Role.User, update.Message.Text);
                        messages.Add(message);
                    }
                }

                if (messages.Count > 0)
                {
                    var messageBot = await botClient.SendTextMessageAsync(update.Message.Chat, "One second. ChatGPT is processing your request...");
                    var messageBotId = messageBot.MessageId;

                    var chatRequest = new ChatRequest(messages);
                    var result = await openAIClient.ChatEndpoint.GetCompletionAsync(chatRequest);

                    Console.WriteLine($"User: {messages[messages.Count - 1].Content}");
                    Console.WriteLine($"ChatGPT: {result.FirstChoice.Message.Content}");

                    await botClient.DeleteMessageAsync(update.Message.Chat, messageBotId);
                    await botClient.SendTextMessageAsync(update.Message.Chat, result.FirstChoice.Message.Content);
                
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);

                await botClient.SendTextMessageAsync(update.Message.Chat, "Sorry, there's been an unexpected error");
            }
        }
        
        private static async Task HandleStartCommand(ITelegramBotClient botClient, Update update)
        {
            string responseMessage = "This is the ChatGPT bot on Telegram. If you want to ask a question, just send a message. ";

            await botClient.SendTextMessageAsync(update.Message.Chat, responseMessage);
        }

        private static async Task DownloadVoiceMessage(Update update)
        {
            var fileId = update.Message.Voice.FileId;
            var file = await telegramBotClient.GetFileAsync(fileId);

            using (var stream = new FileStream(audioPath, FileMode.Create))
            {
                await telegramBotClient.DownloadFileAsync(file.FilePath, stream);
            }
        }

        private static Task Error(ITelegramBotClient botClient, Exception exception, CancellationToken token)
        {
            Console.WriteLine("Error: " + exception.Message);
            return Task.CompletedTask;
        }
    }
}
