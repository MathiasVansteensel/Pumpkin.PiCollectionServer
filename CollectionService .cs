using Microsoft.Extensions.Options;
using Pumpkin.Networking;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Pumpkin.PiCollectionServer;
public static class CollectionService
{
	public const string RemoteServerPath = "http://api.pumpkinapp.be/hub/post-data";

	private static readonly TimeSpan defaultCollectionInterval = TimeSpan.FromMinutes(20);
	private static readonly HttpClient httpClient = new HttpClient
	{
		BaseAddress = new(RemoteServerPath)
	};

	public static bool IsRunning { get; private set; } = false;

	public static TimeSpan CollectionInterval { get; set; } = defaultCollectionInterval;

	private static ConcurrentMessageBuffer messageBuffer = new();
	private static Timer collectionTimer = new Timer
	{
		AutoReset = true,
		Interval = CollectionInterval.TotalMilliseconds
	};

	public static void Start() => Start(defaultCollectionInterval);

	public static void Start(TimeSpan collectionInterval)
	{
		if (IsRunning) return;
		IsRunning = true;
		collectionTimer.Interval = collectionInterval.TotalMilliseconds;
		collectionTimer.Elapsed += async (sender, e) => await Collect();
		Network.PumpkinMessageReceived += OnMessageReceived;
		collectionTimer.Start();
		Console.WriteLine("Collection service started");
	}

	private static void OnMessageReceived(PumpkinMessage msg)
	{
		messageBuffer.Add(msg);
		ViewModel.Instance.DownloadedPackets++;
	}

	private static async Task Collect()
	{
		if (messageBuffer.Count == 0) return;
		byte[] jsonBuffer = await messageBuffer.SerializeAsync();
		await messageBuffer.Clear();
		Console.WriteLine($"Sent:\n{Encoding.UTF8.GetString(jsonBuffer)}");
		//new Thread(async () =>
		//{
		//	HttpContent content = new ByteArrayContent(jsonBuffer);
		//	content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
		//	var response = await httpClient.PostAsync(RemoteServerPath, content);
		//	if (response.IsSuccessStatusCode)
		//	{
		//		ViewModel.Instance.UploadedPackets++;
		//		ViewModel.Instance.Throughput += jsonBuffer.Length;
		//	}
		//	else ViewModel.Instance.Errors++;
		//	if (!IsRunning) Console.WriteLine("Collection service message buffer flushed");
		//}).Start();
	}

	public static async void Stop()
	{
		if (!IsRunning) return;
		IsRunning = false;
		collectionTimer.Stop();
		Network.PumpkinMessageReceived -= OnMessageReceived;
		await Collect();
		Console.WriteLine("Collection service stopped");
	}
}
