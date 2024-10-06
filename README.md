# SharpExclusionFinder

## Overview

This C# program finds Windows Defender folder exclusions using **Windows Defender** through its command-line tool (`MpCmdRun.exe`). The program processes directories recursively, with configurable depth and thread usage, and outputs information about exclusions and scan progress.

The program allows you to:
- Scan for folder exclusions up to a specified depth, **without relying on event logs or admin permissions**.
- Use multi-threading to speed the scan process.
- Log errors and exclusion messages to a specified output file.

## Usage

### Basic Command:
```powershell
program.exe <BasePath> [options]
```

### Options:
- `--max-threads N`: Set the maximum number of threads to use for scanning. Default is 3.
- `--depth N`: Specify the maximum directory depth to scan. Depth 1 means only immediate subdirectories.
- `--output <filePath>`: Specify a file path to log exclusions and errors.
- `-h`, `--help`: Display help and usage information.

### Example:
```powershell
program.exe "C:\MyDirectory" --max-threads 5 --depth 3 --output scan_log.txt
```
This will scan `C:\MyDirectory` up to a depth of 3 subdirectories, using 5 threads, and log any exclusions or errors to `scan_log.txt`.

## How It Works

A blog explaining the technique utilised can be viewed here - https://blog.fndsec.net/2024/10/04/uncovering-exclusion-paths-in-microsoft-defender-a-security-research-insight

## Example Output
```
Processed 2000 directories. Time elapsed: 23.78 seconds.
[+] Folder C:\users\user\Example is excluded
Processed 2500 directories. Time elapsed: 30.77 seconds.
```

## Prerequisites

- **.NET Framework 4.5.2** or later.
- **Windows Defender** must be installed and enabled on the system.
- **MpCmdRun.exe** must be located at `C:\Program Files\Windows Defender\MpCmdRun.exe`.
