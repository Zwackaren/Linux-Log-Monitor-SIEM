using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

class Program
{
    private static readonly string SplunkToken = Environment.GetEnvironmentVariable("SPLUNK_HEC_TOKEN") ?? "MISSING";
    private static readonly string SplunkUrl = "https://localhost:8088/services/collector";
    private static readonly HttpClient HttpClient = new HttpClient();

    private static readonly Regex FailedSshRegex = new Regex(
        @"Failed password for (?<invalid>invalid user )?(?<user>\S+) from (?<ip>[0-9a-fA-F.:]+)", 
        RegexOptions.Compiled);

    static void Main(string[] args)
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Blue;
        Console.WriteLine("===============================================================================");
        Console.WriteLine("[+] CYBER SECURITY COPILOT - LIVE SSH MONITOR & SIEM");
        Console.WriteLine("===============================================================================");
        Console.ResetColor();
        Console.WriteLine("[+] Monitoring system logs via journalctl...");
        Console.WriteLine("[+] Formatting data to JSON and forwarding to Splunk SIEM via HTTPS...");
        Console.WriteLine("[+] Press CTRL+C to terminate the session.\n");

        if (SplunkToken == "MISSING")
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[-] Error: Environment variable 'SPLUNK_HEC_TOKEN' is not set!");
            Console.WriteLine("[+] Please check your deployment instructions to export the token.");
            Console.ResetColor();
            return;
        }

        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        
        var clientHandler = new HttpClientHandler();
        clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
        
        using (var secureClient = new HttpClient(clientHandler))
        {
            secureClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Splunk", SplunkToken);
            WatchJournalLog(secureClient);
        }
    }

    private static void WatchJournalLog(HttpClient client)
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
                Console.WriteLine("[-] Error: Failed to initialize journalctl subprocess.");
                return;
            }

            using (var reader = process.StandardOutput)
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    AnalyzeAndSendLog(line, client).GetAwaiter().GetResult();
                }
            }
        }
    }

    private static async Task AnalyzeAndSendLog(string line, HttpClient client)
    {
        Match match = FailedSshRegex.Match(line);

        if (match.Success)
        {
            string username = match.Groups["user"].Value;
            string ipAddress = match.Groups["ip"].Value;
            bool isInvalid = match.Groups["invalid"].Success;
            string timestamp = DateTime.UtcNow.ToString("o");

            // Generate structured JSON payload
            var alertPayload = new SshEvent(timestamp, ipAddress, username, isInvalid);
            string jsonString = JsonSerializer.Serialize(alertPayload);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ALERT] [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Suspicious SSH Authentication Failure!");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  Target User:     {username}");
            Console.WriteLine($"  Source IP:       {ipAddress}");
            Console.WriteLine($"  Invalid User:    {(isInvalid ? "Yes" : "No")}");
            Console.WriteLine($"  Raw Log Line:    {line.Trim()}");
            Console.ResetColor();

            // Forward JSON payload to Splunk HEC
            try
            {
                var content = new StringContent(jsonString, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(SplunkUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("  [SIEM] 🟢 Payload successfully indexed in Splunk!\n");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  [SIEM] 🔴 Splunk HEC rejected payload with Status Code: {response.StatusCode}\n");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  [SIEM] ⚠️ Network anomaly during SIEM forwarding: {ex.Message}\n");
                Console.ResetColor();
            }
        }
    }
}

public class SshEvent
{
    public SshAlertData @event { get; set; }

    public SshEvent(string timestamp, string ipAddress, string user, bool isInvalidUser)
    {
        @event = new SshAlertData
        {
            timestamp = timestamp,
            source_ip = ipAddress,
            target_user = user,
            is_invalid_user = isInvalidUser,
            severity = "HIGH",
            host = "kali-vm",
            source = "journalctl:ssh",
            sourcetype = "_json"
        };
    }
}

public class SshAlertData
{
    public string timestamp { get; set; }
    public string source_ip { get; set; }
    public string target_user { get; set; }
    public bool is_invalid_user { get; set; }
    public string severity { get; set; }
    public string host { get; set; }
    public string source { get; set; }
    public string sourcetype { get; set; }
}

