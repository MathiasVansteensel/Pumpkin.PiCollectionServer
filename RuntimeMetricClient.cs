using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Runtime.InteropServices;

namespace Pumpkin.PiCollectioServer;
internal static class RuntimeMetricClient
{
#error TODO: make these performanceCounters unreachable on linux
	private static PerformanceCounter _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
	//using P/Invoke now (for windows anyway) bc thats way faster
	//private static PerformanceCounter _ramCounter = new PerformanceCounter("Memory", "Available MBytes");

	public enum OS 
	{
		Windows,//win
		Linux,	//unix
		MacOs,	//unix
		Android,//unix
		TvOs,	//unix
		IOS,
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

	//microsoft keeps removing usefull shit like microsoft.visualbasic.devices for the ComputerInfo class
	[DllImport("kernel32.dll")]
	public static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

	//on init call this slow ass counter so it can "warm up" because it needs a whole second to display a value the first time
    static RuntimeMetricClient() => _ = _cpuCounter.NextValue();

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
		float cpuUsage = _cpuCounter.NextValue();		
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
				Arguments = "-c \"free | awk '/Mem/{print $3/$2 * 100.0}'\"",
				RedirectStandardOutput = true,
				UseShellExecute = false,
				CreateNoWindow = true
			}
		};

		process.Start();
		string output = process.StandardOutput.ReadToEnd();
		process.WaitForExit();

		if (float.TryParse(output, out float usedRamPercentage)) return (0, usedRamPercentage, 0);
		return (0, 0, 0);
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
