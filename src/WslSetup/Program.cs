using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using Microsoft.Win32;

namespace WslSetup
{
    class Program
    {
        static void Main(string[] args)
        {
            if (!IsWslEnabled())
            {
                EnableWsl();
                AddSelfToStartup();
                Reboot(10);
            }

            RemoveSelfFromStartup();
            InstallUbuntu();
            UpgradeUbuntu();

            Console.WriteLine("Press a key to exit...");
            Console.ReadKey();
        }

        #region Methods

        /// <summary>
        /// Enable the Microsoft Windows Subsystem for Linux feature.
        /// </summary>
        static void EnableWsl()
        {
            RunWait("dism.exe", "/online /enable-feature /featurename:Microsoft-Windows-Subsystem-Linux /all /norestart");
            RunWait("dism.exe", "/online /enable-feature /featurename:VirtualMachinePlatform /all /norestart");
            RunWait("wsl.exe", "--set-default-version 2");
        }

        /// <summary>
        /// Check if the Microsoft Windows Subsystem for Linux feature is enabled.
        /// </summary>
        static bool IsWslEnabled()
        {
            return RunPowerShellWithOutput("(Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Windows-Subsystem-Linux).State") == "Enabled";
        }

        /// <summary>
        /// Download and install the Ubuntu 18.04 .Appx executable.
        /// </summary>
        static void InstallUbuntu()
        {
            var ubuntuAppx = Path.Combine(Path.GetTempPath(), "Ubuntu.Appx");
            var client = new WebClient();

            client.DownloadFile("https://aka.ms/wsl-ubuntu-1804", ubuntuAppx);
            RunPowerShell($"Add-AppxPackage {ubuntuAppx}");

            RunWait("ubuntu1804.exe", "install --root");
            RunWait("wsl.exe", "-s Ubuntu-18.04");
            RunWait("wsl.exe", "--set-version Ubuntu-18.04 2");
        }

        /// <summary>
        /// Update and upgrade Ubuntu.
        /// </summary>
        static void UpgradeUbuntu()
        {
            // Sub-process /usr/bin/dpkg returned an error code (1)" error.
            RunWait("ubuntu1804.exe", "rm -f /var/lib/dpkg/info/*");
            RunWait("ubuntu1804.exe", "rm -rf /var/cache/apt/archives/*");

            RunWait("ubuntu1804.exe", "run apt update");
            RunWait("ubuntu1804.exe", "run apt upgrade -y");
        }

        #endregion Methods

        #region Requirements

        /// <summary>
        /// Add an executable to the Windows startup.
        /// </summary>
        /// <param name="executableName">The name of the executable file.</param>
        /// <param name="executableFile">The absolute path of the executable file.</param>
        static void AddToStartup(string executableName, string executableFile)
        {
            var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            key.SetValue(executableName, executableFile);
            key.Close();
        }

        /// <summary>
        /// Remove an executable from the Windows startup.
        /// </summary>
        /// <param name="executableName">The name of the executable file.</param>
        static void RemoveFromStartup(string executableName)
        {
            var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            key.DeleteValue(executableName, throwOnMissingValue: false);
            key.Close();
        }

        /// <summary>
        /// Add the current executable to the Windows startup.
        /// </summary>
        static void AddSelfToStartup()
        {
            var executableName = Assembly.GetExecutingAssembly().GetName().Name;
            // var executableFile = Assembly.GetExecutingAssembly().GetName().CodeBase;
            var executableFile = Assembly.GetExecutingAssembly().Location;
            AddToStartup(executableName, executableFile);
        }

        /// <summary>
        /// Remove the current executable from the Windows startup.
        /// </summary>
        static void RemoveSelfFromStartup()
        {
            var executableName = Assembly.GetExecutingAssembly().GetName().Name;
            RemoveFromStartup(executableName);
        }

        /// <summary>
        /// Run an external program with arguments.
        /// </summary>
        /// <param name="programPath">The full path of the program.</param>
        /// <param name="programArgs">The arguments of the program.</param>
        public static Process Run(string programPath, string programArgs = null)
        {
            var process = new Process
            {
                StartInfo =
                {
                    FileName = programPath,
                    Arguments = programArgs,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();

            return process;
        }

        /// <summary>
        /// Run an external program and pause the execution until the program finishes.
        /// </summary>
        /// <param name="programPath">The full path of the program.</param>
        /// <param name="programArgs">The arguments of the program.</param>
        public static Process RunWait(string programPath, string programArgs = null)
        {
            var process = Run(programPath, programArgs);

            process.WaitForExit();

            return process;
        }

        /// <summary>
        /// Run a PowerShell command.
        /// </summary>
        /// <param name="command">The PowerShell command.</param>
        public static Process RunPowerShell(string command)
        {
            return RunWait("powershell.exe", $"-Command \"{command}\"");
        }

        /// <summary>
        /// Run a PowerShell command and return its raw output.
        /// </summary>
        public static string RunPowerShellWithOutput(string command)
        {
            var process = RunPowerShell(command);
            var output = process.StandardOutput.ReadToEnd().ToString().TrimEnd(Environment.NewLine.ToCharArray());

            return string.IsNullOrWhiteSpace(output) ? null : output;
        }

        /// <summary>
        /// Reboot the Windows system.
        /// </summary>
        /// <param name="afterSeconds">The number of seconds after which the system will reboot.</param>
        public static void Reboot(int afterSeconds)
        {
            Process.Start("cmd.exe", $"/c shutdown /r /f /t {afterSeconds}");
        }

        #endregion Requirements
    }
}