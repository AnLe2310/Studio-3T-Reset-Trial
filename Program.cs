using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Studio_3T_Reset_Trial;

internal class Program
{
    // SYSTEMTIME structure for setting system time
    public struct SYSTEMTIME
    {
        public ushort Year;
        public ushort Month;
        public ushort DayOfWeek;
        public ushort Day;
        public ushort Hour;
        public ushort Minute;
        public ushort Second;
        public ushort Milliseconds;

        public SYSTEMTIME(DateTime dt)
        {
            Year = (ushort)dt.Year;
            Month = (ushort)dt.Month;
            DayOfWeek = (ushort)dt.DayOfWeek;
            Day = (ushort)dt.Day;
            Hour = (ushort)dt.Hour;
            Minute = (ushort)dt.Minute;
            Second = (ushort)dt.Second;
            Milliseconds = (ushort)dt.Millisecond;
        }
    }

    public static string fileName = "";

    [DllImport("kernel32.dll")]
    private static extern bool SetSystemTime(ref SYSTEMTIME time);

    public static void KillStudio3TProcesses()
    {
        foreach (var process in Process.GetProcessesByName("studio3t"))
        {
            try
            {
                fileName = process.MainModule?.FileName ?? "";
                Console.WriteLine($"[INFO] Killing process {process.Id} ({fileName})");
                process.Kill();
                process.WaitForExit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to kill process {process.Id}: {ex.Message}");
            }
        }
    }

    public static List<string> CleanRegistry()
    {
        Console.WriteLine("[INFO] Cleaning registry...");
        var userKeys = new List<string>();
        
        try
        {
            if (OperatingSystem.IsWindows())
            {
                using (var registryKey =
                       Registry.CurrentUser.OpenSubKey("Software\\JavaSoft\\Prefs\\3t\\mongochef\\enterprise",
                           writable: true))
                {
                    if (registryKey == null)
                    {
                        Console.WriteLine("[INFO] No registry keys found.");
                        return userKeys;
                    }

                    foreach (var valueName in registryKey.GetValueNames())
                    {
                        Console.WriteLine($"[INFO] Deleting registry key: {valueName}");
                        userKeys.Add(valueName);
                        registryKey.DeleteValue(valueName);
                    }

                    registryKey.Close();
                }

                Console.WriteLine("[SUCCESS] Registry cleaned.");
            }
            else
            {
                Console.WriteLine("[INFO] Registry cleaning is not supported on this platform.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to clean registry: {ex.Message}");
        }

        return userKeys;
    }

    public static void CleanDirectories(List<string> userKeys)
    {
        Console.WriteLine("[INFO] Cleaning directories...");

        var filteredUserKeys = userKeys.Where(key => !key.Contains("installation-date")).ToList();

        var userFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var publicFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)[0] + ":\\Users\\Public";
        var cacheFolderPath = Path.Combine(userFolderPath, ".cache");
        var appDataLocalPath = Path.Combine(userFolderPath, "AppData", "Local");
        var tempFolderPath = Path.Combine(appDataLocalPath, "Temp");
        var cacheSubDirectories = Directory.GetDirectories(cacheFolderPath);
        
        var foldersToDelete = new List<string>
        {
            Path.Combine(publicFolderPath, "t3"),
            Path.Combine(appDataLocalPath, "t3"),
            Path.Combine(tempFolderPath, "t3")
        };

        foldersToDelete.AddRange(cacheSubDirectories);
        foldersToDelete.AddRange(cacheSubDirectories.Select(dir => Path.Combine(appDataLocalPath, Path.GetFileName(dir))));
        foldersToDelete.AddRange(cacheSubDirectories.Select(dir => Path.Combine(tempFolderPath, Path.GetFileName(dir))));
        foldersToDelete.AddRange(filteredUserKeys.Select(key => Path.Combine(userFolderPath, ".3T", "studio-3t", key)));

        foreach (var folder in foldersToDelete)
        {
            try
            {
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, recursive: true);
                    Console.WriteLine($"[SUCCESS] Removed folder: {folder}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Folder does not exist: {folder}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Cannot remove folder {folder}: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("[SUCCESS] directories cleaned");
            }
        }
    }

    public static async Task ResetSystemTime(DateTime newTime, int delay = 10000)
    {
        Console.WriteLine($"[INFO] Setting system time to: {newTime}");
        var time = new SYSTEMTIME(newTime);
        if (!SetSystemTime(ref time))
        {
            Console.WriteLine("[ERROR] Failed to set system time.");
        }
        else
        {
            Console.WriteLine("[SUCCESS] System time updated.");
        }

        await Task.Delay(delay); // Simulate wait
    }

    public static async Task Main(string[] args)
    {
        Console.Title = "Studio 3T Reset Trial (Improved)";
        Console.WriteLine("[INFO] Starting Studio 3T reset trial process...");
        var originalTime = DateTime.UtcNow;
        var futureTime = originalTime.AddYears(3);

        // Step 1: Kill Studio 3T processes
        KillStudio3TProcesses();

        // Step 2: Clean registry and get user keys
        var userKeys = CleanRegistry();
        
        // Step 3: Temporarily set system time
        if(!string.IsNullOrEmpty(fileName))
            await ResetSystemTime(futureTime, 1000);
        
        // Step 4: Clean relevant directories
        if (userKeys.Count > 0)
            CleanDirectories(userKeys);
        else
            Console.WriteLine("[INFO] No user keys found. Skipping directory cleaning.");
        
        // Step 5: Restart Studio 3T
        if (!string.IsNullOrEmpty(fileName))
        {
            try
            {
                Console.WriteLine($"[INFO] Restarting Studio 3T: {fileName}");
                Process.Start(fileName);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Failed to restart Studio 3T: {ex.Message}");
            }
        }
        
        // Step 6: Revert system time
        if (!string.IsNullOrEmpty(fileName))
        {
            Console.WriteLine("[INFO] Reverting system time to original.");
            await ResetSystemTime(originalTime);
        }

        Console.WriteLine("[SUCCESS] Process complete. Press any key to exit.");
        Console.ReadKey();
    }
}
