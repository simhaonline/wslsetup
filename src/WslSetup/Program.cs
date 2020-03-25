using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Win32.TaskScheduler;

namespace WslSetup
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!IsWslEnabled())
            {
                Console.WriteLine("Enable WSL2...");
                EnableWsl();
                Console.WriteLine("Add to startup...");
                AddToStartup();
                Console.WriteLine("Reboot the system...");
                Reboot(3);
            }

            Console.WriteLine("Remove from startup...");
            RemoveFromStartup();
            Console.WriteLine("Download and install Ubuntu...");
            InstallUbuntu();
            Console.WriteLine("Update and upgrade Ubuntu...");
            UpgradeUbuntu();

            Console.WriteLine("Press a key to exit...");
            Console.ReadKey();
        }

        /// <summary>
        /// Enable the Microsoft Windows Subsystem for Linux feature.
        /// </summary>
        static void EnableWsl()
        {
            Process.Start("dism.exe", "/online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart").WaitForExit();
            Process.Start("dism.exe", "/online /enable-feature /featurename:VirtualMachinePlatform /all /norestart").WaitForExit();
        }

        /// <summary>
        /// Check if the Microsoft Windows Subsystem for Linux feature is enabled.
        /// </summary>
        static bool IsWslEnabled()
        {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = "powershell.exe";
            startInfo.Arguments = "-Command \"(Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux).State\"";
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;

            using (var process = Process.Start(startInfo))
            using (var reader = process.StandardOutput)
            {
                return reader.ReadToEnd().ToString().TrimEnd(Environment.NewLine.ToCharArray()) == "Enabled";
            }
        }

        /// <summary>
        /// Download and install the Ubuntu 18.04 .Appx executable.
        /// </summary>
        static void InstallUbuntu()
        {
            if (IsUbuntuInstalled())
            {
                return;
            }

            var ubuntuAppx = DownloadFromUrl("https://aka.ms/wsl-ubuntu-1804").Result;
            Process.Start("powershell.exe", $"-Command \"Add-AppxPackage {ubuntuAppx}\"").WaitForExit();
            Process.Start("ubuntu1804.exe", "install --root").WaitForExit();
            Process.Start("wsl.exe", "-s Ubuntu-18.04").WaitForExit();
            Process.Start("wsl.exe", "--set-version Ubuntu-18.04 2").WaitForExit();
        }

        /// <summary>
        /// Check if Ubuntu 18.04 is installed.
        /// </summary>
        static bool IsUbuntuInstalled()
        {
            var startInfo = new ProcessStartInfo();
            startInfo.FileName = "powershell.exe";
            startInfo.Arguments = "-Command \"(Get-Command -ErrorAction SilentlyContinue ubuntu1804) -ne $null\"";
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;

            using (var process = Process.Start(startInfo))
            using (var reader = process.StandardOutput)
            {
                return reader.ReadToEnd().ToString().TrimEnd(Environment.NewLine.ToCharArray()) == "True";
            }
        }

        /// <summary>
        /// Update and upgrade Ubuntu.
        /// </summary>
        static void UpgradeUbuntu()
        {
            Process.Start("ubuntu1804.exe", "run apt update").WaitForExit();
            Process.Start("ubuntu1804.exe", "run UCF_FORCE_CONFOLD=1 DEBIAN_FRONTEND=noninteractive apt -o Dpkg::Options::=\"--force-confdef\" -o Dpkg::Options::=\"--force-confold\" upgrade -y").WaitForExit();
        }

        /// <summary>
        /// Add the current executable to scheduled tasks.
        /// </summary>
        static void AddToStartup()
        {
            var executableName = Assembly.GetExecutingAssembly().GetName().Name;
            var executableFile = Process.GetCurrentProcess().MainModule.FileName;

            using (TaskService ts = new TaskService())
            {
                TaskDefinition td = ts.NewTask();
                td.Principal.RunLevel = TaskRunLevel.Highest;
                td.Triggers.Add(new LogonTrigger());
                td.Actions.Add(new ExecAction(executableFile));
                ts.RootFolder.RegisterTaskDefinition(executableName, td);
            }
        }

        /// <summary>
        /// Remove the current executable from scheduled tasks.
        /// </summary>
        static void RemoveFromStartup()
        {
            var executableName = Assembly.GetExecutingAssembly().GetName().Name;
            var executableFile = Process.GetCurrentProcess().MainModule.FileName;

            TaskService.Instance.RootFolder.DeleteTask(executableName, exceptionOnNotExists: false);
        }

        /// <summary>
        /// Reboot the Windows system.
        /// </summary>
        /// <param name="afterSeconds">The number of seconds after which the system will reboot.</param>
        static void Reboot(int afterSeconds)
        {
            Process.Start("cmd.exe", $"/c shutdown /r /f /t {afterSeconds}").WaitForExit();
            Environment.Exit(0);
        }

        /// <summary>
        /// Download file from URL.
        /// </summary>
        /// <param name="url">The URL of the file.</param>
        static async Task<string> DownloadFromUrl(string url)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:71.0) Gecko/20100101 Firefox/71.0");
                using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    var downloadedFile = Path.Combine(
                        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName,
                        response.Content.Headers.ContentDisposition?.FileName.Replace("\"", "").Replace("'", "") ?? response.RequestMessage.RequestUri.Segments[^1]
                    );

                    using (var streamToReadFrom = await response.Content.ReadAsStreamAsync())
                    using (var streamToReadTo = File.Open(downloadedFile, FileMode.Create))
                    {
                        await streamToReadFrom.CopyToAsync(streamToReadTo);
                    }

                    return downloadedFile;
                }
            }
        }
    }
}