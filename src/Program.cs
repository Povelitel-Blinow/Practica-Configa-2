using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;

namespace DependencyVisualizer
{
    /// <summary>CLI сбора прямых зависимостей пакетов Ubuntu.</summary>
    public static class Program
    {
        /// <summary>Запускает приложение с консольным выводом.</summary>
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            return Run(args, Console.Out, Console.Error);
        }

        /// <summary>Запускает сбор данных с HTTP-клиентом стандартной библиотеки.</summary>
        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(60);
                return Run(args, output, error, client);
            }
        }

        /// <summary>Выводит зависимости или ошибку; тестовый граф будет реализован на этапе 3.</summary>
        public static int Run(string[] args, TextWriter output, TextWriter error, HttpClient client)
        {
            const int HelpArgumentCount = 1;
            if (args.Length == HelpArgumentCount && args[0] == "--help")
            {
                output.WriteLine("--package NAME --repository URL_OR_FILE --test-mode true|false --version VERSION");
                return 0;
            }
            try
            {
                Settings settings = CommandLine.Parse(args);
                if (!settings.TestMode)
                {
                    List<string> dependencies = Repository.GetDependencies(settings, client);
                    output.WriteLine("Прямые зависимости " + settings.Package + " " + settings.Version + ":");
                    if (dependencies.Count == 0) output.WriteLine("(нет)");
                    foreach (string dependency in dependencies) output.WriteLine(dependency);
                    return 0;
                }
                WriteSettings(settings, output);
                return 0;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IOException
                || exception is InvalidDataException || exception is HttpRequestException
                || exception is OperationCanceledException)
            {
                error.WriteLine("Ошибка: " + exception.Message);
                error.WriteLine("Справка: --help");
                return 1;
            }
        }

        /// <summary>Сохраняет вывод настроек первого этапа для тестового режима.</summary>
        private static void WriteSettings(Settings settings, TextWriter output)
        {
            output.WriteLine("package=" + settings.Package);
            output.WriteLine("repository=" + settings.Repository);
            output.WriteLine("test-mode=" + (settings.TestMode ? "true" : "false"));
            output.WriteLine("version=" + settings.Version);
        }
    }
}
