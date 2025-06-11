using System;

public static class PrintHelper
{
    public static bool EnableColor { get; set; } = true;

    public static void PrintInfo(string message)
    {
        Print(message, ConsoleColor.Cyan);
    }

    public static void PrintWarning(string message)
    {
        Print(message, ConsoleColor.Yellow);
    }

    public static void PrintError(string message)
    {
        Print(message, ConsoleColor.Red);
    }

    public static void PrintSuccess(string message)
    {
        Print(message, ConsoleColor.Green);
    }

    public static void PrintHeader(string message)
    {
        Print(message, ConsoleColor.Magenta);
    }

    private static void Print(string message, ConsoleColor color)
    {
        if (EnableColor)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine(message);
        }
    }
}   