using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Pumpkin.Networking;
using System.Runtime.CompilerServices;

namespace Pumpkin.PiCollectionServer
{
	internal class Program
	{
		const string WWWRoot = "Web";
		const string IndexFile = "index.html";
		const string EmailSubject = "{0} - 🎃Pumpkin Status Update🎃";
		const string MailBody = 
		"Dear User,\r\n\r\n" +
		"We are pleased to inform you that your Smart Home Hub has successfully come online and is now fully operational. As of {0} at {1}, the hub connected to your network and is ready to serve your smart home needs.\r\n\r\n" +
		"Please find below the essential details regarding the hub's connection:\r\n\r\n" +
		"Hub IP Address: {2}\r\n" +
		"Network Interface: {3}\r\n" +
		"To access the web portal and view comprehensive statistics about your smart home system, simply enter the hub's IP address ({2}) into your preferred web browser.\r\n\r\n" +
		"We highly recommend bookmarking the web portal for quick and easy access in the future. It provides valuable insights and allows you to manage and monitor your connected devices efficiently.\r\n\r\n" +
		"Visit our website (pumpkinapp.be) if you have any questions.\r\n\r\n" +
		"Best regards,\r\n\r\n" +
		"The Pumpkin Smarthome team 🎃";

		const ushort PortalPort = 6969;
		const ushort UdpPort = 8888;

		public static event Action OnLoadPortal;
		public static event Action OnInitialized;

		static (float cpuPercentage, float usedRam, float maxRam) lastMetrics = RuntimeMetricClient.GetMetrics();
		internal static IPAddress ipAddress;
		internal static string networkName;

		private static byte bitField = 0;
		private static bool _isInit = false;
		internal static bool IsInit
		{
			get => _isInit;
			private set
			{
				if (value) OnLoadPortal?.Invoke();
				_isInit = value;
			}
		}

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
			{ PlaceHolderStrings[(int)placeholderIndex.CONNECTION_STRING], $"{networkName = GetNetworkName(out ipAddress)}  [{ipAddress}]"},
			{ PlaceHolderStrings[(int)placeholderIndex.PUMPKIN_ID], ViewModel.Instance.HWID },
			{ PlaceHolderStrings[(int)placeholderIndex.CPU_NUM], $"{lastMetrics.cpuPercentage:0.0}%" },
			{ PlaceHolderStrings[(int)placeholderIndex.RAM_NUM], $"{lastMetrics.usedRam:0.0} / {lastMetrics.maxRam:0.0} GB" },
		};

		private static WebpageUpdater updater = new($"{WWWRoot}/{IndexFile}", ref variableData, async (html, context) => await context.Response.WriteAsync(html));

        static Program()
        {
			Network.Initialize(UdpPort);
			Network.DatagramReceived += Network_DatagramReceived;
			OnInitialized += () =>
			{
#if !DEBUG
				MailClient.MessageSent += (sender, msg) => Console.WriteLine($"Email sent to {string.Join(',', msg.To)}");
				DateTime today = DateTime.Now;
				string shortDate = today.ToString("ddd M MMM yyyy");
				string subject = string.Format(EmailSubject, shortDate);
				string body = string.Format(MailBody, shortDate, today.ToShortTimeString(), ipAddress, networkName);
				MailClient.SendEmail(ViewModel.Instance.Email, subject, body);
#else
				Console.WriteLine("[DEBUG]: would have sent mail");
#endif
			};
			OnInitialized?.Invoke();
		}

		private static void Network_DatagramReceived(string msg, IPAddress sender)
		{
			string response = null;
			switch (msg.Trim().ToLower())
			{
				case "#show#":
					response = $"[{ViewModel.Instance.HWID}]: {ipAddress}";
					break;
				default:
					return;
			}

			Network.Send(response, sender.ToString(), UdpPort);
		}

		static async Task Main()
		{
			Console.WriteLine("Started :)");
			var host = new WebHostBuilder()
				.UseKestrel()
				.UseUrls($"http://localhost:{PortalPort}", $"http://{ipAddress}:{PortalPort}/")
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
						newData = $"{networkName = GetNetworkName(out ipAddress)}  [{ipAddress}]";
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

				//boolean logic is so much easier in c++
				if (!IsInit)
				{
					bitField ^= (byte)(1 << i);
					IsInit = (bitField & byte.MaxValue) == byte.MaxValue;
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
		public static string GetNetworkName(out IPAddress ip)
		{
			NetworkInterface nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(ni => ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);
			ip = nic?.GetIPProperties().UnicastAddresses.Where(addr => addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).FirstOrDefault()?.Address ?? (GetLocalIp() ?? IPAddress.None);
			return nic?.Name ?? "Not Connected";
		}

		private static IPAddress GetLocalIp() 
		{
			using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.IP))
			{
				socket.Connect("8.8.8.8", ushort.MaxValue - 2);
				return ((IPEndPoint)socket.LocalEndPoint)?.Address;
			}
		}
	}
}