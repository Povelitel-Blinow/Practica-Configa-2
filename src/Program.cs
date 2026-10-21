using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Net.Http;

namespace DependencyVisualizer
{
    /// <summary>CLI построения графа зависимостей пакетов Ubuntu.</summary>
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

        /// <summary>Выводит достижимый граф зависимостей или сообщение об ошибке.</summary>
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
                Dictionary<string, List<string>> nodes;
                if (settings.TestMode)
                {
                    using (StreamReader reader = new StreamReader(settings.Repository))
                        nodes = TestRepository.Read(reader);
                }
                else nodes = Repository.GetGraph(settings, client);
                DependencyGraph.Build(nodes, settings.Package).Write(output);
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

    }
}
