using System;
using System.Collections;

namespace GradebookMaintenance
{
    class UI
    {
        public const int Height = 30;
        public const int Width = 120;
        
        public static void ClearFormDisplay()
        {
            for (int x = 4; x < Height - 3; x++)
            {
                Console.SetCursorPosition(0, x);
                ClearLine();
            }

            Console.SetCursorPosition(0, UI.Height - 3);
            DrawLine();
            
            Console.SetCursorPosition(3, 4);
        }
        
        public static void UpdateTitle(string title)
        {
            var school = Datastore.selectedSchool;
            Console.SetCursorPosition(0, 0);
            Console.ForegroundColor = ConsoleColor.Gray;
            DrawLine();
            Console.SetCursorPosition(0, 1);
            ClearLine();
            Console.SetCursorPosition(3, 1);
            Console.Write($"GRADEBOOK MAINTENANCE ROUTINES - {title}");
            Console.CursorLeft = (UI.Width - 30);
            Console.Write("Selected school: ");
            Console.ForegroundColor = school == null ? ConsoleColor.Red : ConsoleColor.Green;
            Console.WriteLine(school?.shortname ?? "None");
            Console.ForegroundColor = ConsoleColor.Gray;
            DrawLine();

            ClearFormDisplay();
        }
        
        public static void AskForAnyKey()
        {
            Console.SetCursorPosition(0, Height - 2);
            ClearLine();
            Console.SetCursorPosition(3, Height - 2);
            Console.Write("Press any key to continue...");
            Console.ReadLine();
        }

        public static string AskForInput(string message)
        {
            Console.SetCursorPosition(0, Height - 2);
            ClearLine();
            Console.SetCursorPosition(3, Height - 2);
            Console.Write($"{message}: ");
            return Console.ReadLine();
        }
        
        public static int AskForInput(int maxValue, int[] disabledOptions = null)
        {
            disabledOptions ??= new int[0];

            string userChoice;
            var commandIndex = -1;
            do
            {
                userChoice = AskForInput("Select an option");
            }
            while (!int.TryParse(userChoice, out commandIndex) || commandIndex > maxValue || ((IList) disabledOptions).Contains(commandIndex));
            return commandIndex;
        }

        public static bool AskForConfirmation()
        {
            var userChoice = string.Empty;
            do
            {
                userChoice = AskForInput("Continue? Enter 'Y' to proceed or 'N' to go back");
            }
            while (userChoice != "Y" && userChoice != "N");
            return userChoice == "Y";
        }

        public static void ClearInput()
        {
            Console.SetCursorPosition(0, Height - 2);
            ClearLine();
        }
        
        private static void ClearLine()
        {
            Console.Write(new string(' ', Console.WindowWidth));
        }

        private static void DrawLine()
        {
            Console.WriteLine(new string('-', Console.WindowWidth));
        }
        
        public static void WriteIndented(string text)
        {
            Console.CursorLeft = 3;
            Console.Write(text);
        }

        public static void WriteIndentedLine(string text)
        {
            Console.CursorLeft = 3;
            Console.WriteLine(text);
        }

        public static void StatusInProgress()
        {
            Console.CursorLeft = 50;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("[In Progress]");
        }

        public static void StatusOk()
        {
            Console.CursorLeft = 50;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[OK]            ");
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        public static void StatusError()
        {
            Console.CursorLeft = 50;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[Error]         ");
            Console.CursorLeft = 3;
        }
    }
}