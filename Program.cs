using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System.Net.NetworkInformation;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Pumpkin.Networking;
using System.Runtime.CompilerServices;
using System.Linq;
using Pumpkin.PiCollectionServer.Tmp;

namespace Pumpkin.PiCollectionServer
{
	internal class Program
	{
		const string PortalWWWRoot = "Web/Portal";
		const string EmailWWWRoot = "Web/Email";
		const string PortalIndexFile = "index.html";
		const string EmailIndexFile = "EmailBody.html";
		const string EmailFormKey = "email";
		const string ColorFormKey = "color";
		const string CycleFormKey = "cycle";
		const string EmailSubject = "{0} - 🎃Pumpkin Status Update🎃";
		//const string MailBody =
		//"Dear User,<br/><br/>" +
		//"We are pleased to inform you that your Smart Home Hub has <strong>successfully come online</strong>. As of <strong>{0}</strong> at <strong>{1}</strong>, the hub connected to your network and is ready for further setup, or if already setup, to serve your smart home needs.<br/><br/>" +
		//"Please find below the essential details regarding the hub's connection:<br/>" +
		//"<ul><li>Hub IP Address: {2}</li>" +
		//"<li>Network Interface: {3}</li>" +
		//"<li>WebPortal Port: {4}</li>" +
		//"<li>UDP Port: {5}</li></ul>" +
		//"To access the web portal and view comprehensive statistics about your smart home system, simply enter the hub's IP address and port into your preferred web browser (like so: http://{2}:{4}/).<br/>" +
		//"You can also use any UDP client (app/program) to broadcast the message \"#Show#\" (without quotes) on the broadcast address <strong>(255.255.255.255)</strong> on <strong>port {5}</strong> to retrieve the IP and ID of your device. For a simpler, more first party, approach you can also download the <strong>Pumpkin wizzard</strong> from our website (pumpkinapp.be/download/wizzard) to finish setting up your device." +
		//"We highly recommend bookmarking the web portal for quick and easy access in the future. It provides valuable insights and allows you to keep an eye on the network side of your smarthome system.<br/><br/>" +
		//"Visit our website (pumpkinapp.be) if you have any questions.<br/><br/>" +
		//"Best regards,<br/><br/>" +
		//"The Pumpkin Smarthome team 🎃";

		private static readonly TimeSpan NetworkInfoUpdateInterval = TimeSpan.FromMinutes(10);
		private static readonly TimeSpan PerformanceMetricUpdateInterval = TimeSpan.FromMilliseconds(350);
		private static readonly TimeSpan CollectionInterval = TimeSpan.FromSeconds(20);

		private static ushort PortalPort { get; set; } = 8888;
		private static ushort UdpPort { get; set; } = 6969;
		private static string ShortDateToday
		{
			get => DateTime.Today.ToString("ddd d MMM yyyy");
		}

		private static Stopwatch networkInfoUpdateWatch = new();
		private static Stopwatch performanceMetricUpdateWatch = new();

		public static event Action Initialized;

		private static (float cpuPercentage, float usedRam, float maxRam)? _performanceMetrics = null;
		private static (float cpuPercentage, float usedRam, float maxRam) PerformanceMetrics
		{
			get
			{
				if (_performanceMetrics is null || !_performanceMetrics.HasValue || performanceMetricUpdateWatch.Elapsed > PerformanceMetricUpdateInterval)
				{
					performanceMetricUpdateWatch.Restart();
					return (_performanceMetrics = RuntimeMetricClient.GetMetrics()).Value;
				}
				return _performanceMetrics.Value;
			}
		}

		private static (string networkName, IPAddress ipAddress)? _networkInfo = null;
		internal static (string networkName, IPAddress ipAddress) NetworkInfo
		{
			get
			{
				if (_networkInfo is null || !_networkInfo.HasValue || networkInfoUpdateWatch.Elapsed > NetworkInfoUpdateInterval)
				{
					IPAddress ipAddr;
					string netName = GetNetworkName(out ipAddr);
					networkInfoUpdateWatch.Restart();
					return (netName, ipAddr);
				}
				return _networkInfo.Value;
			}
		}

		//private static byte bitField = 0;
		//private static bool _isInit = false;
		//internal static bool IsInit
		//{
		//	get => _isInit;
		//	private set
		//	{
		//		if (value) OnLoadPortal?.Invoke();
		//		_isInit = value;
		//	}
		//}

		internal enum PortalPlaceholderIndex
		{
			DOWNLOADED_NUM,
			UPLOADED_NUM,
			THROUGHPUT_NUM,
			ERROR_NUM,
			CONNECTION_STRING,
			PUMPKIN_ID,
			CPU_NUM,
			RAM_NUM,
			EMAIL,
			//VirtualSite:
			LAMP_COLOR,
			LAMP_STATE,
			LAMP_CYCLE_STATE,
			TEMPERATURE,
			HUMIDITY,
			HEAT_INDEX,
			LIGHT
		}

		internal enum EmailPlaceholderIndex
		{
			DATE,
			TIME,
			IP,
			PORTAL_PORT,
			UDP_PORT,
			NETWORK_NAME
		}

		internal static readonly string[] PortalPlaceHolderStrings =
		{
			"{DOWNLOADED_NUM}",
			"{UPLOADED_NUM}",
			"{THROUGHPUT_NUM}",
			"{ERROR_NUM}",
			"{CONNECTION_STRING}",
			"{PUMPKIN_ID}",
			"{CPU_NUM}",
			"{RAM_NUM}",
			"{EMAIL}",
			//VirtualSite:
			"{LAMP_COLOR}",
			"{LAMP_STATE}",
			"{LAMP_CYCLE_STATE}",
			"{TEMPERATURE}",
			"{HUMIDITY}",
			"{HEAT_INDEX}",
			"{LIGHT}"
		};

		private static Dictionary<string, Func<dynamic>> portalVariableData = new Dictionary<string, Func<dynamic>>
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
			//VirtualSite:
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.TEMPERATURE], () => VirtualSite.LastTemperature},
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.LAMP_CYCLE_STATE], () => VirtualSite.LastLampCycleState },
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.HUMIDITY], () => VirtualSite.LastHumidity },
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.HEAT_INDEX], () => VirtualSite.LastHeatIndex },
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.LAMP_COLOR], () => VirtualSite.LastLampColor },
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.LAMP_STATE], () => VirtualSite.LastLampState },
			{ PortalPlaceHolderStrings[(int)PortalPlaceholderIndex.LIGHT], () => VirtualSite.LastLight },
		};

		internal static readonly string[] EmailPlaceHolderStrings =
		{
			"{DATE}",
			"{TIME}",
			"{IP}",
			"{PORTAL_PORT}",
			"{UDP_PORT}",
			"{NETWORK_NAME}"
		};

		private static Dictionary<string, Func<dynamic>> emailVariableData = new Dictionary<string, Func<dynamic>>
		{
			{ EmailPlaceHolderStrings[(int)EmailPlaceholderIndex.DATE], () => ShortDateToday },
			{ EmailPlaceHolderStrings[(int)EmailPlaceholderIndex.TIME], () => DateTime.Now.ToShortTimeString() },
			{ EmailPlaceHolderStrings[(int)EmailPlaceholderIndex.IP], () => NetworkInfo.ipAddress },
			{ EmailPlaceHolderStrings[(int)EmailPlaceholderIndex.PORTAL_PORT], () => PortalPort},
			{ EmailPlaceHolderStrings[(int)EmailPlaceholderIndex.UDP_PORT], () => UdpPort },
			{ EmailPlaceHolderStrings[(int)EmailPlaceholderIndex.NETWORK_NAME], () => NetworkInfo.networkName }
		};

		private static WebpageUpdater portalUpdater = new($"{PortalWWWRoot}/{PortalIndexFile}", ref portalVariableData, async (html, context) => await context.Response.WriteAsync(html));
		private static WebpageUpdater emailUpdater = new($"{EmailWWWRoot}/{EmailIndexFile}", ref emailVariableData);

		private static ushort GetUniquePort(ushort originalPort, params ushort[] exclusions)
		{
			IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
			IEnumerable<IPEndPoint> activeListeners = ipProperties.GetActiveTcpListeners().Concat(ipProperties.GetActiveUdpListeners());
			ushort port = originalPort;
			Random portRandom = new();
			while (activeListeners.Any(con => con.Port == port || exclusions.Contains(port))) port = (ushort)portRandom.Next(1, ushort.MaxValue - 1);
			return port;
		}

		private static ushort GetUniquePort(params ushort[] exclusions) => GetUniquePort(0, exclusions);

		static Program() //First thing that runs
		{
			networkInfoUpdateWatch.Start();
			performanceMetricUpdateWatch.Start();
			Network.Initialize(UdpPort = GetUniquePort(UdpPort, 443, 8080, 80, 25, 2525));
			
			PortalPort = GetUniquePort(PortalPort, 443, 2525, 25);
			Network.DatagramReceived += Network_DatagramReceived;
			Initialized += OnInitialized;
			Initialized?.Invoke();
		}

		private static void OnInitialized() //2nd thing that runs (async-ish)
		{
#if !DEBUG
			MailClient.MessageSent += (sender, msg) => Console.WriteLine($"Email sent to {string.Join(',', msg.To)}");
			string subject = string.Format(EmailSubject, ShortDateToday);
			MailClient.SendEmail(ViewModel.Instance.Email, subject, emailUpdater.GetUpdated());
#else
			Console.WriteLine($"[DEBUG]: would have sent mail to {ViewModel.Instance.Email}");
#endif
		}

		static async Task Main() //3rd thing that runs (async)
		{
			CollectionService.Start(CollectionInterval);
			var host = new WebHostBuilder()
				.UseKestrel()
				.UseUrls($"http://localhost:{PortalPort}", $"http://{NetworkInfo.ipAddress}:{PortalPort}/")
				.Configure(app => app.Run(async (context) => await HandleRequest(context)))
				.Build();

			await host.RunAsync();
		}

		private static void Network_DatagramReceived(string msg, IPAddress sender)
		{
			string response = null;
			switch (msg.Trim().ToLower())
			{
				case "#show#":
					response = $"[{ViewModel.Instance.HWID}] IPv4: {NetworkInfo.ipAddress}";
					break;
				default:
					return;
			}

			Network.Send(response, sender.ToString(), UdpPort);
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
					catch (Exception) { }
					finally
					{
						context.Response.Redirect("/", true);
						if (oldEmail != ViewModel.Instance.Email) Console.WriteLine($"Email changed [{oldEmail} > {ViewModel.Instance.Email}]");
					}
					return;
				case "/setcolor":
					try 
					{
						string color = context.Request.Form[ColorFormKey];
						Network.Send($"#SETCOLOR{color}", "255.255.255.255", 6969);
					}
					catch (Exception) { Console.WriteLine("Failed to perform command"); }
					finally
					{
					}
					context.Response.Redirect("/", true);
					return;
				case "/cycle":
					try { Network.Send($"#CYCLE#", "255.255.255.255", 6969); }
					catch (Exception) { Console.WriteLine("Failed to perform command"); }
					finally
					{
					}
					context.Response.Redirect("/", true);
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
			ip =GetLocalIp() ?? (nic?.GetIPProperties().UnicastAddresses.Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork).FirstOrDefault()?.Address ?? IPAddress.None);
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