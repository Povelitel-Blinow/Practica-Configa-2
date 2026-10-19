using System;
using System.IO;
using System.Text;

namespace DependencyVisualizer
{
    /// <summary>Минимальный CLI-прототип визуализатора зависимостей.</summary>
    public static class Program
    {
        /// <summary>Запускает приложение с консольным выводом.</summary>
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            return Run(args, Console.Out, Console.Error);
        }

        /// <summary>Выводит настройки; возвращает 0 при успехе и 1 при ошибке.</summary>
        public static int Run(string[] args, TextWriter output, TextWriter error)
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
                output.WriteLine("package=" + settings.Package);
                output.WriteLine("repository=" + settings.Repository);
                output.WriteLine("test-mode=" + (settings.TestMode ? "true" : "false"));
                output.WriteLine("version=" + settings.Version);
                return 0;
            }
            catch (ArgumentException exception)
            {
                error.WriteLine("Ошибка: " + exception.Message);
                error.WriteLine("Справка: --help");
                return 1;
            }
        }
    }
}
