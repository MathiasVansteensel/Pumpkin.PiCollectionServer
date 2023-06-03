using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Pumpkin.PiCollectioServer;
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
		const string PortalWWWRoot = "Web/Portal";
		const string EmailWWWRoot = "Web/Email";
		const string PortalIndexFile = "index.html";
		const string EmailIndexFile = "EmailBody.html";
		const string EmailFormKey = "email";
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

		private static readonly TimeSpan NetworkInfoUpdateInterval = TimeSpan.FromMinutes(10);
		private static readonly TimeSpan PerformanceMetricUpdateInterval = TimeSpan.FromMilliseconds(350);
		private static readonly TimeSpan CollectionInterval = TimeSpan.FromMinutes(1);

		public static event EventHandler OnInitialized;

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
				if (value) OnInitialized?.Invoke(null, EventArgs.Empty);
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
			RAM_NUM,
			EMAIL
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
			"{RAM_NUM}",
			"{EMAIL}"
		};

		private static Dictionary<string, dynamic> variableData = new Dictionary<string, dynamic>
		{
			//what can i say i like intellisense on my arrays (also, if and index changes i just need to change the index and intellisense is handy for that)
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.DOWNLOADED_NUM], () => ViewModel.Instance.DownloadedPackets },
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.UPLOADED_NUM], () => ViewModel.Instance.UploadedPackets },
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.THROUGHPUT_NUM], () => ViewModel.Instance.Throughput },
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.ERROR_NUM], () => ViewModel.Instance.Errors },
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.CONNECTION_STRING], () => $"{NetworkInfo.networkName}  [{NetworkInfo.ipAddress}]"},
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.PUMPKIN_ID], () => ViewModel.Instance.HWID },
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.CPU_NUM], () => $"{PerformanceMetrics.cpuPercentage:0.0}%" },
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.RAM_NUM], () => $"{PerformanceMetrics.usedRam:0.0} / {PerformanceMetrics.maxRam:0.0} GB" },
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.EMAIL], () => ViewModel.Instance.Email },
		};

		private static WebpageUpdater updater = new($"{WWWRoot}/{IndexFile}", ref variableData, async (html, context) => await context.Response.WriteAsync(html));

		private static Dictionary<string, Func<dynamic>> emailVariableData = new Dictionary<string, Func<dynamic>>
		{
			{ EmailPlaceHolderStrings[(int)EmailPlaceholderIndex.DATE], () => ShortDateToday },
			{ EmailPlaceHolderStrings[(int)EmailPlaceholderIndex.TIME], () => DateTime.Now.ToShortTimeString() },
			{ EmailPlaceHolderStrings[(int)EmailPlaceholderIndex.IP], () => NetworkInfo.ipAddress },
			{ EmailPlaceHolderStrings[(int)EmailPlaceholderIndex.PORTAL_PORT], () => PortalPort},
			{ EmailPlaceHolderStrings[(int)EmailPlaceholderIndex.UDP_PORT], () => UdpPort },
			{ EmailPlaceHolderStrings[(int)EmailPlaceholderIndex.NETWORK_NAME], () => NetworkInfo.networkName }
		};

			Network.Initialize(UdpPort);
			Network.DatagramReceived += Network_DatagramReceived;
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

		private static void OnInitialized() //2nd thing that runs (async-ish)
		{
#if !DEBUG
			MailClient.MessageSent += (sender, msg) => Console.WriteLine($"Email sent to {string.Join(',', msg.To)}");
			string subject = string.Format(EmailSubject, ShortDateToday);
			MailClient.SendEmail(ViewModel.Instance.Email, subject, emailUpdater.GetUpdated(), true);
#else
			Console.WriteLine($"[DEBUG]: would have sent mail to {ViewModel.Instance.Email}");
#endif
		}

		static async Task Main()
		{
			CollectionService.Start(CollectionInterval);
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

		static async Task HandleRequest(HttpContext context)
		{
			string path = context.Request.Path;
			string requestPath = string.Empty;

			switch (path) //for special cases so i can add em here
			{
				case "/":
					portalUpdater.Update(context);
					return;
				case "/submit-email":
					var oldEmail = ViewModel.Instance.Email;
					try { ViewModel.Instance.Email = context.Request.Form[EmailFormKey]; }
					catch (Exception) {}
					finally 
					{ 
						context.Response.Redirect("/", true);
						if (oldEmail != ViewModel.Instance.Email) Console.WriteLine($"Email changed [{oldEmail} > {ViewModel.Instance.Email}]");
					}
					return;
				default:
					requestPath = PortalWWWRoot + path;
					break;
			}
			await context.Response.SendFileAsync(requestPath);
		}

		//static void UpdateDataTable()
		//{
		//	for (int i = 0; i < PortalPlaceHolderStrings.Length; i++)
		//	{
		//		dynamic newData;
		//		switch ((PortalPlaceholderIndex)i)
		//		{
		//			case PortalPlaceholderIndex.DOWNLOADED_NUM:
		//				newData = ViewModel.Instance.DownloadedPackets;
		//				break;
		//			case PortalPlaceholderIndex.UPLOADED_NUM:
		//				newData = ViewModel.Instance.UploadedPackets;
		//				break;
		//			case PortalPlaceholderIndex.THROUGHPUT_NUM:
		//				newData = ViewModel.Instance.Throughput;
		//				break;
		//			case PortalPlaceholderIndex.ERROR_NUM:
		//				newData = ViewModel.Instance.Errors;
		//				break;
		//			case PortalPlaceholderIndex.CONNECTION_STRING:
		//				newData = $"{networkName = GetNetworkName(out ipAddress)}  [{ipAddress}]";
		//				break;
		//			case PortalPlaceholderIndex.PUMPKIN_ID:
		//				newData = ViewModel.Instance.HWID;
		//				break;
		//			case PortalPlaceholderIndex.CPU_NUM:
		//				lastMetrics = RuntimeMetricClient.GetMetrics();
		//				newData = $"{lastMetrics.cpuPercentage:0.0}%";
		//				break;
		//			case PortalPlaceholderIndex.RAM_NUM:
		//				lastMetrics = RuntimeMetricClient.GetMetrics();
		//				newData = $"{lastMetrics.usedRam:0.0} / {lastMetrics.maxRam:0.0} GB";
		//				break;
		//			default:
		//				newData = "Whoops! Error";
		//				break;
		//		}

		//		//boolean logic is so much easier in c++
		//		if (!IsInit)
		//		{
		//			bitField ^= (byte)(1 << i);
		//			IsInit = (bitField & byte.MaxValue) == byte.MaxValue;
		//		}

		//		portalVariableData[PortalPlaceHolderStrings[i]] = newData;
		//	}
		//}

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