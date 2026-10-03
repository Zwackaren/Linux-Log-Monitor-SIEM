using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

class Program
{
    // Det här mönstret hanterar nu både "invalid user" och vanliga användare över både IPv4 och IPv6!
    private static readonly Regex FailedSshRegex = new Regex(
        @"Failed password for (invalid user )?(?<user>\S+) from (?<ip>[0-9a-fA-F.:]+)", 
        RegexOptions.Compiled);

    static void Main(string[] args)
    {
        Console.WriteLine("[+] Startar live-övervakning av alla SSH-enheter via journalctl...");
        Console.WriteLine("[+] Letar efter misslyckade inloggningar... Tryck på CTRL+C för att avsluta.\n");

        WatchJournalLog();
    }

    private static void WatchJournalLog()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "journalctl",
            Arguments = "-u ssh -f --no-pager",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(startInfo))
        {
            if (process == null)
            {
                Console.WriteLine("[-] Fel: Kunde inte starta journalctl.");
                return;
            }

            using (var reader = process.StandardOutput)
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    AnalyzeLogLine(line);
                }
            }
        }
    }

    private static void AnalyzeLogLine(string line)
    {
        Match match = FailedSshRegex.Match(line);

        if (match.Success)
        {
            string username = match.Groups["user"].Value;
            string ipAddress = match.Groups["ip"].Value;
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ALERT] [{timestamp}] Misstänkt SSH-inloggning misslyckades!");
            Console.ResetColor();
            Console.WriteLine($"\tAnvändarnamn: {username}");
            Console.WriteLine($"\tKäll-IP:     {ipAddress}");
            Console.WriteLine($"\tLoggrad:     {line.Trim()}\n");
        }
    }
}
