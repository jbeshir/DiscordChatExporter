using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AmmySidekick;
using DiscordChatExporter.Models;
using DiscordChatExporter.Services;
using Tyrrrz.Extensions;

namespace DiscordChatExporter
{
    public static class Program
    {
        private class ExportAllOptions
        {
            public string Token { get; }

            public ExportFormat Format { get; }
            
            public int Year { get; }
            
            public int Month { get; }
            
            public bool AllUpTo { get; }

            public ExportAllOptions(string token, ExportFormat theme, int year, int month, bool allUpTo)
            {
                Token = token;
                Format = theme;
                Year = year;
                Month = month;
                AllUpTo = allUpTo;
            }
        }
        
        private static ExportAllOptions GetExportAllOptions(string[] args)
        {
            // Parse the arguments
            var argsDic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var arg in args)
            {
                var match = Regex.Match(arg, "/(.*?):\"?(.*?)\"?$");
                var key = match.Groups[1].Value;
                var value = match.Groups[2].Value;

                if (key.IsBlank())
                    continue;

                argsDic[key] = value;
            }

            // Extract required arguments
            var token = argsDic.GetOrDefault("token");
            var yearStr = argsDic.GetOrDefault("year");
            var monthStr = argsDic.GetOrDefault("month");

            // Verify arguments
            if (token.IsBlank() || yearStr.IsBlank() || monthStr.IsBlank())
                throw new ArgumentException("Some or all required command line arguments are missing");

            var year = int.Parse(yearStr);
            var month = int.Parse(monthStr);
            
            // Exract optional arguments
            var format = argsDic.GetOrDefault("format").ParseEnumOrDefault<ExportFormat>();

            // Create option set
            return new ExportAllOptions(token, format, year, month, argsDic.GetOrDefault("allUpTo") == "yes");
        }
        
        private static async Task ExportAll(ExportAllOptions options)
        {
            var token = options.Token;
            var format = options.Format;
            var year = options.Year;
            var month = options.Month;
            var allUpTo = options.AllUpTo;
            
            var settingsService = new SettingsService();
            var dataService = new DataService();
            var exportService = new ExportService(settingsService);
            var messageGroupService = new MessageGroupService(settingsService);
            
            DateTime? from = allUpTo ? null : (DateTime?)new DateTime(year, month, 1);
            DateTime to = new DateTime(year, month, 1).AddMonths(1);

            Console.WriteLine("Retrieving servers...");
            var guilds = await dataService.GetUserGuildsAsync(token);
            foreach (var guild in guilds)
            {
                Console.WriteLine("Retrieving channels for " + guild.Name + "...");
                var channels = await dataService.GetGuildChannelsAsync(token, guild.Id);
                foreach (var channel in channels)
                {
                    var filePath = "Export/" + guild.Name + "/" + channel.Name + "/" + year + "-" + month + (format == ExportFormat.PlainText ? ".txt" : ".html");

                    // Get messages
                    Console.WriteLine("Retrieving messages for " + guild.Name + ":" + channel.Name);
                    var messages = await dataService.GetChannelMessagesAsync(token, channel.Id, from, to);

                    // Group them
                    var messageGroups = messageGroupService.GroupMessages(messages);

                    // Create log
                    var log = new ChannelChatLog(guild, channel, messageGroups, messages.Count);

                    // Export
                    Console.WriteLine("Exporting messages...");
                    Directory.CreateDirectory("Export/" + guild.Name + "/" + channel.Name + "/");
                    await exportService.ExportAsync(format, filePath, log);
                }
            }
        }
        
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Length > 1)
            {
                var options = GetExportAllOptions(args);
                ExportAll(options).GetAwaiter().GetResult();
                return;
            }
            
            var app = new App();
            app.InitializeComponent();

            RuntimeUpdateHandler.Register(app, $"/{Ammy.GetAssemblyName(app)};component/App.g.xaml");

            app.Run();
        }

        public static string GetResourceString(string resourcePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream == null)
                throw new MissingManifestResourceException("Could not find resource");

            using (stream)
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}