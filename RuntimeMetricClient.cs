using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Reflection.Metadata.Ecma335;
using System.Text.RegularExpressions;

namespace Pumpkin.PiCollectionServer;
internal static class RuntimeMetricClient
{
	private static PerformanceCounter _cpuCounter;
	//using P/Invoke now (for windows anyway) bc thats way faster
	//private static PerformanceCounter _ramCounter = new PerformanceCounter("Memory", "Available MBytes");
	
	const string linuxPerformanceBashCommand = "top -bn 1 | grep -E '^%?Cpu|MiB Mem :'";
	const string linuxDecimalNumberRegex = @"([0-9.]+\.[0-9.])";

	const byte cpuCaptureIndex = 3;
	const byte usedRamCaptureIndex = 10;
	const byte maxRamCaptureIndex = 8;

	//WHY IS THE awk SYNTAX DIFFERENT ON DEBIAN VS UBUNTU... WTF
	//const string linuxPerformanceBashCommand = @"top -b -n 1 | awk " +
	//@"'{ " +
	//	$"if (match($0, /{linuxCpuRegex}/, arr)) printf(\"\"%s\\n\\r\"\", arr[1]);" +
	//	$"if (match($0, /{linuxUsedRamRegex}/, arr)) printf(\"\"%s\\n\\r\"\", arr[1]);" +
	//	$"if (match($0, /{linuxTotalRamRegex}/, arr)) printf(\"\"%s\\n\\r\"\", arr[1]);" +
	//@"}'";

	//const string linuxCpuRegex = @"([0-9.]+\.[0-9.])(?: id)";
	//const string linuxUsedRamRegex = @"([0-9]+\.[0-9]) used";
	//const string linuxTotalRamRegex = @"([0-9]+\.[0-9]) total";


	private static OS currentOs;

	public enum OS 
	{
		Windows,//win
		Linux,	//unix
		MacOs,	//unix
		Android,//unix
		TvOs,	//unix
		IOS,	//piece of shit
		Other	//idk, idc (browser or smart toaster or smart toilet or whatever)
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct MemoryStatusEx
	{
		public uint dwLength;
		public uint dwMemoryLoad;
		public ulong ullTotalPhys;
		public ulong ullAvailPhys;
		public ulong ullTotalPageFile;
		public ulong ullAvailPageFile;
		public ulong ullTotalVirtual;
		public ulong ullAvailVirtual;
		public ulong ullAvailExtendedVirtual;
	}

	//microsoft keeps removing useful shit like microsoft.visualbasic.devices for the ComputerInfo class
	[DllImport("kernel32.dll")]
	public static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

	//on init call this slow ass counter so it can "warm up" because it needs a whole second to display a value the first time
	//i love this short syntax
    static RuntimeMetricClient() => _ = (_cpuCounter ?? ((currentOs = DetectOs()) == OS.Windows ? _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total") : null ))?.NextValue();

    public static (float cpuPercentage, float usedRam, float maxRam) GetMetrics() 
	{
		switch (DetectOs())
		{
			case OS.Windows: return GetWinMetrics();
			case OS.Linux: return GetLinuxMetrics();
			default: 
				return default;
		}
	}

	private static (float cpuPercentage, float usedRam, float maxRam) GetWinMetrics()
	{
		float cpuUsage = _cpuCounter?.NextValue() ?? 0f;		
		MemoryStatusEx memStatus = new MemoryStatusEx();
		memStatus.dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));

		if (GlobalMemoryStatusEx(ref memStatus))
		{
			//1_073_741_824f is for  to gb
			float totalMemory = memStatus.ullTotalPhys / 1_073_741_824f;
			float usedMemory = totalMemory - memStatus.ullAvailPhys / 1_073_741_824f;
			return (cpuUsage, usedMemory, totalMemory);
		}
		else return (0, 0, 0);
	}

	private static (float cpuPercentage, float usedRam, float maxRam) GetLinuxMetrics()
	{
		Process process = new Process()
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = "bash",
				Arguments = $"-c \"{linuxPerformanceBashCommand}\"",
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			}
		};

		process.Start();
		string output = process.StandardOutput.ReadToEnd();
		process.WaitForExit();

		//Match cpuMatch, usedRamMatch, maxRamMatch;

		//These all work, just not on every system...

		//Attempt 1
		//string[] values = output.Split(Environment.NewLine);
		//if (values.Length < 3) return (0, 0, 0);
		//float cpuPercentage = float.TryParse(values[0], out cpuPercentage) ? 100 - cpuPercentage : 0,
		//usedRam = float.TryParse(values[1], out usedRam) ? usedRam / 1024f : 0,
		//maxRam = float.TryParse(values[2], out maxRam) ? maxRam / 1024f : 0;

		//Attempt 2
		//cpuPercentage = float.TryParse((cpuMatch = Regex.Match(output, linuxCpuRegex)).Success ? cpuMatch.Value.Replace(" id", string.Empty) : "100", out cpuPercentage) ? 100 - cpuPercentage : 0;
		//usedRam = float.TryParse((usedRamMatch = Regex.Match(output, linuxUsedRamRegex)).Success ? usedRamMatch.Value : "0", out usedRam) ? usedRam / 1024f : 0;
		//maxRam = float.TryParse((maxRamMatch = Regex.Match(output, linuxTotalRamRegex)).Success ? maxRamMatch.Value : "0", out maxRam) ? maxRam / 1024f : 0;

		//Attempt3
		MatchCollection numberMatches = Regex.Matches(output, linuxDecimalNumberRegex);
		float cpuPercentage = numberMatches.Count >= cpuCaptureIndex + 1 && float.TryParse(numberMatches[cpuCaptureIndex].Value, out cpuPercentage) ? 100 - cpuPercentage : 0;
		float usedRam = numberMatches.Count >= usedRamCaptureIndex + 1 && float.TryParse(numberMatches[usedRamCaptureIndex].Value, out usedRam) ? usedRam / 1024f : 0;
		float maxRam = numberMatches.Count >= maxRamCaptureIndex + 1 && float.TryParse(numberMatches[maxRamCaptureIndex].Value, out maxRam) ? maxRam / 1024f : 0;
		
		return (cpuPercentage, usedRam, maxRam);
	}

	public static OS DetectOs() 
	{
		if(OperatingSystem.IsWindows()) return OS.Windows;
		else if (OperatingSystem.IsLinux()) return OS.Linux;
		else if(OperatingSystem.IsMacOS()) return OS.MacOs;
		else if(OperatingSystem.IsAndroid()) return OS.Android;
		else if (OperatingSystem.IsIOS()) return OS.IOS;
		else if(OperatingSystem.IsTvOS()) return OS.TvOs;
		else return OS.Other;
	}
}
