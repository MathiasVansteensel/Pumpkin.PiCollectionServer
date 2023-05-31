using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Pumpkin.PiCollectioServer;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace Pumpkin.PiCollectionServer
{
	internal class Program
	{
		const string WWWRoot = "Web";
		const string IndexFile = "index.html";

		static (float cpuPercentage, float usedRam, float maxRam) lastMetrics = RuntimeMetricClient.GetMetrics();

		internal enum placeholderIndex
		{
			DOWNLOADED_NUM,
			UPLOADED_NUM,
			THROUGHPUT_NUM,
			ERROR_NUM,
			CONNECTION_STRING,
			PUMPKIN_ID,
			CPU_NUM,
			RAM_NUM
		}

		internal static readonly string[] PlaceHolderStrings = 
		{
			"{DOWNLOADED_NUM}",
			"{UPLOADED_NUM}",
			"{THROUGHPUT_NUM}",
			"{ERROR_NUM}",
			"{CONNECTION_STRING}",
			"{PUMPKIN_ID}",
			"{CPU_NUM}",
			"{RAM_NUM}"
		};

		private static Dictionary<string, dynamic> variableData = new Dictionary<string, dynamic>
		{
			//what can i say i like intellisense on my arrays (also, if and index changes i just need to change the index and intellisense is handy for that)
			//i am also aware that initializing these values is useless but it doesn't hurt to get a couple values from the cpu counter before requesting actual values
			{ PlaceHolderStrings[(int)placeholderIndex.DOWNLOADED_NUM], ViewModel.Instance.DownloadedPackets },
			{ PlaceHolderStrings[(int)placeholderIndex.UPLOADED_NUM], ViewModel.Instance.UploadedPackets },
			{ PlaceHolderStrings[(int)placeholderIndex.THROUGHPUT_NUM], ViewModel.Instance.Throughput },
			{ PlaceHolderStrings[(int)placeholderIndex.ERROR_NUM], ViewModel.Instance.Errors },
			{ PlaceHolderStrings[(int)placeholderIndex.CONNECTION_STRING], GetNetworkName() },
			{ PlaceHolderStrings[(int)placeholderIndex.PUMPKIN_ID], ViewModel.Instance.HWID },
			{ PlaceHolderStrings[(int)placeholderIndex.CPU_NUM], $"{lastMetrics.cpuPercentage:0.0}%" },
			{ PlaceHolderStrings[(int)placeholderIndex.RAM_NUM], $"{lastMetrics.usedRam:0.0} / {lastMetrics.maxRam:0.0} GB" },
		};

		private static WebpageUpdater updater = new($"{WWWRoot}/{IndexFile}", ref variableData, async (html, context) => await context.Response.WriteAsync(html));

		static async Task Main()
		{
			var host = new WebHostBuilder()
				.UseKestrel()
				.UseUrls("http://192.168.1.131:80/", "http://localhost:80/")
				.Configure(app => app.Run(async (context) => await HandleRequest(context)))
				.Build();

			await host.RunAsync();
		}

		static void UpdateDataTable() 
		{
			for (int i = 0; i < PlaceHolderStrings.Length; i++)
			{
				dynamic newData;
				switch ((placeholderIndex)i)
				{
					case placeholderIndex.DOWNLOADED_NUM:
						newData = ViewModel.Instance.DownloadedPackets;
						break;
					case placeholderIndex.UPLOADED_NUM:
						newData = ViewModel.Instance.UploadedPackets;
						break;
					case placeholderIndex.THROUGHPUT_NUM:
						newData = ViewModel.Instance.Throughput;
						break;
					case placeholderIndex.ERROR_NUM:
						newData = ViewModel.Instance.Errors;
						break;
					case placeholderIndex.CONNECTION_STRING:
						newData = GetNetworkName();
						break;
					case placeholderIndex.PUMPKIN_ID:
						newData = ViewModel.Instance.HWID;
						break;
					case placeholderIndex.CPU_NUM:
						lastMetrics = RuntimeMetricClient.GetMetrics();
						newData = $"{lastMetrics.cpuPercentage:0.0}%";
						break;
					case placeholderIndex.RAM_NUM:
						lastMetrics = RuntimeMetricClient.GetMetrics();
						newData = $"{lastMetrics.usedRam:0.0} / {lastMetrics.maxRam:0.0} GB";
						break;
					default:
						newData = "Whoops! Error";
						break;
				}

				variableData[PlaceHolderStrings[i]] = newData;
			}
		}

		static async Task HandleRequest(HttpContext context)
		{
			string path = context.Request.Path;
			string requestPath = string.Empty;

			switch (path) //for special cases so i can add em here
			{
				case "/":
					UpdateDataTable();
					updater.Update(context);
					return;
				default:
					requestPath = WWWRoot + path;
					break;
			}
			await context.Response.SendFileAsync(requestPath);			
		}
		
		//what a mess
		public static string GetNetworkName() => NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)?.Name ?? "Not Connected";
	}
}