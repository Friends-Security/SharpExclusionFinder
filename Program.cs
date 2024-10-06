using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

class Program
{
  static HashSet<string> excludedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

  // Counter for progress
  static int counter = 0;

  static int totalDirectories = 0;

  static int maxThreads = 3;

  static int maxDepth = int.MaxValue; // By default, scan all depths

  // Stopwatch to measure performance
  static Stopwatch stopwatch = new Stopwatch();

  static string outputFile = null;

  static StreamWriter logWriter = null;

  static readonly object logWriterLock = new object(); 

  static void Main(string[] args)
  {
    if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
    {
      PrintHelp();
      return;
    }

    string basePath = args[0];

    for (int i = 1; i < args.Length; i++)
    {
      if (args[i] == "--max-threads" && i + 1 < args.Length)
      {
        if (int.TryParse(args[i + 1], out int threads))
        {
          maxThreads = threads;
          i++; 
        }
      }
      else if (args[i] == "--depth" && i + 1 < args.Length)
      {
        if (int.TryParse(args[i + 1], out int depth))
        {
          maxDepth = depth;
          i++; 
        }
      }
      else if (args[i] == "--output" && i + 1 < args.Length)
      {
        outputFile = args[i + 1];
        i++; 
      }
    }

    if (outputFile != null)
    {
      try
      {
        logWriter = new StreamWriter(outputFile);
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Error opening output file {outputFile}: {ex.Message}");
        return;
      }
    }

    stopwatch.Start();

    GetExcludedFoldersByTier(basePath, 0); 

    if (logWriter != null)
    {
      logWriter.Close();
    }
  }

  static void PrintHelp()
  {
    Console.WriteLine("Usage: SharpExclusionFinder.exe <BasePath> [options]");
    Console.WriteLine("Authors: Hoshea Yarden, Hai Vaknin, Yehuda Smirnov, Noam Pomerantz");
    Console.WriteLine("Options:");
    Console.WriteLine("  --max-threads N      Set the maximum number of threads (default 3)");
    Console.WriteLine("  --depth N            Set the maximum directory depth to scan (1 = immediate subdirectories)");
    Console.WriteLine("  --output <filePath>  Specify a file to log exclusions and errors");
    Console.WriteLine("  -h, --help           Display help and usage information");
  }

  // Function to get excluded folders using a tiered approach, with depth limitation
  static void GetExcludedFoldersByTier(string basePath, int currentDepth)
  {
    if (currentDepth > maxDepth)
      return;

    var directoriesByTier = new Queue<List<string>>();
    List<string> currentTierDirectories = new List<string>();

    try
    {
      // First, add the top-level directories (first tier)
      currentTierDirectories.AddRange(Directory.GetDirectories(basePath));
      directoriesByTier.Enqueue(currentTierDirectories);
    }
    catch (Exception ex)
    {
      LogMessage($"Error retrieving top-level directories from {basePath}: {ex.Message}", isError: true);
    }

    // Process each tier until all directories are scanned or max depth is reached
    while (directoriesByTier.Count > 0 && currentDepth <= maxDepth)
    {
      var currentTier = directoriesByTier.Dequeue(); 

      totalDirectories += currentTier.Count; 

      // Filter out excluded directories before processing
      List<string> filteredDirectories = new List<string>();
      foreach (var dir in currentTier)
      {
        if (!IsDirectoryExcluded(dir))
        {
          filteredDirectories.Add(dir); 
        }
      }
      
      ProcessTierDirectories(filteredDirectories);

      List<string> nextTierDirectories = new List<string>();

      foreach (string dir in filteredDirectories)
      {
        try
        {
          var subDirs = Directory.GetDirectories(dir);
          nextTierDirectories.AddRange(subDirs);
        }
        catch (UnauthorizedAccessException)
        {
          LogMessage($"Access denied to {dir}. Skipping this directory and its subdirectories.", isError: true);
        }
        catch (Exception ex)
        {
          LogMessage($"Error retrieving subdirectories from {dir}: {ex.Message}", isError: true);
        }
      }

      if (nextTierDirectories.Count > 0)
      {
        directoriesByTier.Enqueue(nextTierDirectories);
      }

      currentDepth++;
    }

    // Final message
    if (currentDepth <= maxDepth)
    {
      stopwatch.Stop();
      Console.WriteLine($"Scan completed up to depth {maxDepth}. Total time: {stopwatch.Elapsed.TotalSeconds:F2} seconds.");
    }
  }

  static void ProcessTierDirectories(List<string> directories)
  {
    SemaphoreSlim semaphore = new SemaphoreSlim(maxThreads);

    List<Task> tasks = new List<Task>();

    foreach (string dir in directories)
    {
      semaphore.Wait();

      Task task = Task.Run(() =>
      {
        try
        {
          ScanDirectory(dir);
        }
        finally
        {
          semaphore.Release();
        }
      });

      tasks.Add(task);
    }

    Task.WaitAll(tasks.ToArray());
  }

  // Function to scan each directory
  static void ScanDirectory(string currentPath)
  {
    try
    {
      int currentCount = Interlocked.Increment(ref counter);

      // Print every 500 directories processed
      if (currentCount % 500 == 0)
      {
        TimeSpan elapsed = stopwatch.Elapsed;
        Console.WriteLine($"Processed {currentCount} directories. Time elapsed: {elapsed.TotalSeconds:F2} seconds.");
      }

      // Run the Windows Defender scan command on the current directory
      string command = $@"C:\Program Files\Windows Defender\MpCmdRun.exe";
      string args = $"-Scan -ScanType 3 -File \"{currentPath}\\|*\" -CpuThrottling 0";

      string output = RunProcess(command, args);

      if (output.Contains("was skipped"))
      {
        LogMessage($"[+] Folder {currentPath} is excluded", isError: false);
        excludedDirectories.Add(currentPath); // Add the directory to the exclusion list
      }
    }
    catch (UnauthorizedAccessException)
    {
      LogMessage($"Skipping {currentPath} due to UnauthorizedAccessException.", isError: true);
    }
    catch (Exception ex)
    {
      LogMessage($"An error occurred while scanning directory {currentPath}: {ex.Message}", isError: true);
    }
  }

  // Function to check if a directory or its parent is excluded
  static bool IsDirectoryExcluded(string directory)
  {
    string currentDirectory = directory;
    while (!string.IsNullOrEmpty(currentDirectory))
    {
      if (excludedDirectories.Contains(currentDirectory))
      {
        return true; 
      }
      currentDirectory = Path.GetDirectoryName(currentDirectory); 
    }
    return false;
  }

  // Function to run a process and return the output
  static string RunProcess(string command, string arguments)
  {
    try
    {
      ProcessStartInfo processInfo = new ProcessStartInfo(command, arguments)
      {
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true
      };

      using (Process process = Process.Start(processInfo))
      {
        using (StreamReader reader = process.StandardOutput)
        {
          string result = reader.ReadToEnd();
          return result;
        }
      }
    }
    catch (Exception ex)
    {
      LogMessage($"Error running process: {ex.Message}", isError: true);
      return string.Empty;
    }
  }

  // Function to log messages either to console or output file
  static void LogMessage(string message, bool isError)
  {
    if (logWriter != null && (isError || message.Contains("[+] Folder")))
    {
      lock (logWriterLock) 
      {
        logWriter.WriteLine(message);
        logWriter.Flush(); 
      }
    }

    Console.WriteLine(message);
  }
}
