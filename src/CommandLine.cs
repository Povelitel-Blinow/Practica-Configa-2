using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DependencyVisualizer
{
    /// <summary>Читает и проверяет параметры командной строки.</summary>
    public static class CommandLine
    {
        /// <summary>Создаёт настройки или сообщает об ошибке аргументов.</summary>
        public static Settings Parse(string[] args)
        {
            Dictionary<string, string> values = ReadOptions(args);
            Settings settings = new Settings();
            settings.Package = Required(values, "--package");
            settings.Repository = Required(values, "--repository");
            string testMode = Required(values, "--test-mode");
            settings.Version = Required(values, "--version");
            if (testMode != "true" && testMode != "false")
            {
                throw new ArgumentException("--test-mode: допустимы только true и false.");
            }
            settings.TestMode = testMode == "true";
            ValidatePackage(settings);
            ValidateVersion(settings.Version);
            ValidateRepository(settings);
            return settings;
        }

        /// <summary>Читает пары опция-значение, запрещая повторы и неизвестные опции.</summary>
        private static Dictionary<string, string> ReadOptions(string[] args)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();
            for (int i = 0; i < args.Length; i++)
            {
                string option = args[i];
                if (option != "--package" && option != "--repository"
                    && option != "--test-mode" && option != "--version")
                {
                    throw new ArgumentException("Неизвестная опция: " + option);
                }
                if (values.ContainsKey(option))
                {
                    throw new ArgumentException("Повтор опции: " + option);
                }
                i++;
                if (i >= args.Length || args[i].StartsWith("--"))
                {
                    throw new ArgumentException("Не указано значение " + option);
                }
                if (string.IsNullOrWhiteSpace(args[i]))
                {
                    throw new ArgumentException("Пустое значение " + option);
                }
                values.Add(option, args[i]);
            }
            return values;
        }

        /// <summary>Возвращает значение обязательной опции.</summary>
        private static string Required(Dictionary<string, string> values, string option)
        {
            if (!values.ContainsKey(option))
            {
                throw new ArgumentException("Отсутствует обязательная опция " + option);
            }
            return values[option];
        }

        /// <summary>Проверяет имя обычного или тестового пакета.</summary>
        private static void ValidatePackage(Settings settings)
        {
            string pattern = @"\A[a-z0-9][a-z0-9+.-]+\z";
            if (settings.TestMode)
            {
                pattern = @"\A[A-Z]+\z";
            }
            if (!Regex.IsMatch(settings.Package, pattern))
            {
                throw new ArgumentException("--package: некорректное имя пакета для выбранного режима.");
            }
        }

        /// <summary>Проверяет формат версии: [эпоха:]версия[-ревизия].</summary>
        private static void ValidateVersion(string version)
        {
            string pattern = @"\A([0-9]+:)?[0-9][A-Za-z0-9.+~]*(?:-[A-Za-z0-9.+~]+)*\z";
            if (!Regex.IsMatch(version, pattern))
            {
                throw new ArgumentException("--version: ожидается версия вида 1.0, 2:1.0-3 или 1.0~rc1.");
            }
        }

        /// <summary>Проверяет URL или доступность файла тестового репозитория.</summary>
        private static void ValidateRepository(Settings settings)
        {
            if (settings.TestMode)
            {
                try
                {
                    using (FileStream file = File.OpenRead(settings.Repository))
                    {
                        return;
                    }
                }
                catch (IOException)
                {
                    throw new ArgumentException("--repository: файл не найден или недоступен для чтения.");
                }
                catch (UnauthorizedAccessException)
                {
                    throw new ArgumentException("--repository: нет доступа к файлу.");
                }
                catch (ArgumentException)
                {
                    throw new ArgumentException("--repository: некорректный путь к файлу.");
                }
            }
            Uri address;
            if (!Uri.TryCreate(settings.Repository, UriKind.Absolute, out address)
                || (address.Scheme != "http" && address.Scheme != "https")
                || string.IsNullOrEmpty(address.Host))
            {
                throw new ArgumentException("--repository: ожидается абсолютный HTTP или HTTPS URL.");
            }
        }
    }
}
