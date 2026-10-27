using System;
using System.Collections.Generic;
using System.Globalization;

namespace Augensicher
{
    // Two languages: English and Russian.
    //
    // English is the source language: every string appears in the code as its
    // English text, and this table maps it to Russian. A string with no entry
    // falls through unchanged, so a missing translation shows English rather
    // than a blank or a crash. tests\Tests.cs checks that every string the
    // code uses has a Russian entry.
    static class Tr
    {
        public static string Lang = "en";

        // "auto" follows the Windows display language: Russian Windows gets
        // Russian, everything else English.
        public static void SetLanguage(string setting)
        {
            if (setting == "en" || setting == "ru") Lang = setting;
            else Lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
        }

        public static string T(string english)
        {
            string ru;
            if (Lang == "ru" && Russian.TryGetValue(english, out ru)) return ru;
            return english;
        }

        public static string F(string english, params object[] args)
        {
            return string.Format(T(english), args);
        }

        public static string[] Tips { get { return Lang == "ru" ? TipsRu : TipsEn; } }

        static readonly string[] TipsEn =
        {
            "Look at something at least 20 feet (6 m) away",
            "Blink a few times and let your eyes relax",
            "Look away from the screen, out of a window if you can",
            "Close your eyes and take a slow, deep breath",
            "Roll your shoulders and stretch your neck",
        };

        static readonly string[] TipsRu =
        {
            "Посмотри вдаль, на 6 метров и дальше",
            "Поморгай несколько раз и расслабь глаза",
            "Отвернись от экрана и посмотри в окно",
            "Закрой глаза и сделай глубокий вдох",
            "Разомни шею и плечи",
        };

        public static readonly Dictionary<string, string> Russian = new Dictionary<string, string>
        {
            // --- name ---
            { "Augensicher", "Отдых для глаз" },
            { "Eye break reminder for Windows", "Напоминания о перерывах для глаз" },

            // --- break screen ---
            { "Eye break", "Перерыв для глаз" },
            { "Time left: {0}", "Осталось: {0}" },
            { "(press Esc or click anywhere to skip)", "(нажми Esc или щёлкни мышью, чтобы пропустить)" },
            { "Snooze for {0} min", "Отложить на {0} мин" },
            { "Short break", "Короткая пауза" },
            { "Look at something far away", "Посмотри вдаль" },

            // --- tray icon ---
            { "Break in progress: {0} left", "Идёт перерыв, осталось {0}" },
            { "Paused until {0}", "Пауза до {0}" },
            { "Away, timer stopped. Break in {0}", "Тебя нет, счётчик стоит. До перерыва {0}" },
            { "Next break in {0}", "До перерыва {0}" },
            { "The break is on.", "Идёт перерыв." },
            { "Timer stopped ({0}): {1} of work until the break.", "Пауза ({0}). Счётчик стоит, до перерыва {1} работы." },
            { "{0} of work until the break (one every {1} min at the computer).", "До перерыва {0} работы (перерыв каждые {1} мин за компьютером)." },
            { "Reminders are paused until {0}.", "Напоминания на паузе до {0}." },
            { "screen locked", "экран заблокирован" },
            { "display off", "монитор выключен" },
            { "no mouse or keyboard", "нет мыши и клавиатуры" },
            { "computer was asleep", "компьютер спал" },
            { "Running: a break every {0} min of work, time away does not count. Right-click the eye for settings.",
              "Запущена: перерыв каждые {0} мин работы, время без тебя не считается. Настройки — правый щелчок по глазу." },
            { "Break in {0} seconds.", "Перерыв через {0} секунд." },
            { "Already running: the eye icon is next to the clock.", "Программа уже работает: значок-глаз рядом с часами." },
            { "Reminders are back on.", "Напоминания снова включены." },

            // --- menu ---
            { "Take a break now", "Сделать перерыв сейчас" },
            { "Reset the timer", "Сбросить отсчёт" },
            { "Pause reminders", "Пауза" },
            { "For 30 minutes", "На 30 минут" },
            { "For 1 hour", "На 1 час" },
            { "For 2 hours", "На 2 часа" },
            { "Until tomorrow", "До завтра" },
            { "Resume", "Продолжить" },
            { "Settings...", "Настройки..." },
            { "Start with Windows", "Запускать вместе с Windows" },
            { "Support info", "Сведения для поддержки" },
            { "About", "О программе" },
            { "Exit", "Выход" },
            { "Could not change autostart:\n{0}", "Не удалось изменить автозапуск:\n{0}" },

            // --- support file ---
            { "The support file is saved:\n\n{0}\n\nSend it to whoever is helping you. It holds the version, the Windows version, the settings and the last lines of the program's log - nothing about what you do on the computer.",
              "Файл сведений сохранён:\n\n{0}\n\nОтправь его тому, кто помогает. В нём версия программы и Windows, настройки и последние строки журнала программы - ничего о том, что ты делаешь за компьютером." },
            { "Could not write the support file:\n{0}", "Не удалось записать файл сведений:\n{0}" },

            // --- settings window ---
            { "Settings", "Настройки" },
            { "Breaks", "Перерывы" },
            { "Work time before a break, minutes", "Работа до перерыва, минут" },
            { "Break length, minutes", "Длина перерыва, минут" },
            { "Snooze button postpones by, minutes", "Кнопка «Отложить», минут" },
            { "Warn before a break, seconds (0 = off)", "Предупредить до перерыва, секунд (0 = нет)" },
            { "Allow skipping a break (Esc or click)", "Можно пропустить перерыв (Esc или щелчок)" },
            { "Play a sound when a break ends", "Звук в конце перерыва" },
            { "Short breaks (20-20-20 rule)", "Короткие паузы (правило 20-20-20)" },
            { "Remind me to look into the distance", "Напоминать посмотреть вдаль" },
            { "Every, minutes", "Каждые, минут" },
            { "For, seconds", "На сколько, секунд" },
            { "When you are away", "Когда тебя нет за компьютером" },
            { "No mouse or keyboard this long means away, minutes", "Нет мыши и клавиатуры столько — тебя нет, минут" },
            { "Away this long means start over, minutes", "Не было столько — отсчёт с нуля, минут" },
            { "Full-screen video and games count as work", "Полноэкранное видео и игры — это работа" },
            { "General", "Общие" },
            { "Language", "Язык" },
            { "Same as Windows", "Как в Windows" },
            { "Save", "Сохранить" },
            { "Cancel", "Отмена" },
            { "Could not save the settings:\n{0}", "Не удалось сохранить настройки:\n{0}" },

            // --- about window ---
            { "Version {0}", "Версия {0}" },
            { "Free software under the MIT License.\nIt comes as is, with no warranty of any kind.\n\nNot a medical device and not medical advice. If your eyes hurt, see a doctor.",
              "Бесплатная программа, лицензия MIT.\nПредоставляется как есть, без каких-либо гарантий.\n\nЭто не медицинское изделие и не медицинский совет. Если болят глаза, обратись к врачу." },
            { "Project page", "Страница проекта" },
            { "License", "Лицензия" },
            { "Disclaimer", "Отказ от ответственности" },
            { "Close", "Закрыть" },
        };
    }
}
