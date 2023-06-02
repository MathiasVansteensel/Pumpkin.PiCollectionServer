using Pumpkin.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Timers;
using Timer = System.Timers.Timer;

namespace Pumpkin.PiCollectionServer;
public static class ConnectionService
{
	private static readonly TimeSpan defaultCollectionInterval = TimeSpan.FromMinutes(20);
	private static readonly HttpClient httpClient = new HttpClient();	

	public static bool isRunning { get; private set; } = false;

	public static TimeSpan CollectionInterval { get; set; } = defaultCollectionInterval;

	private static ConcurrentBag<string> messageBuffer = new();
	private static Timer collectionTimer = new Timer
	{
		AutoReset = true,
		Interval = CollectionInterval.TotalMilliseconds
	};

	public static async Task Start()
	{
		if (isRunning) return;
		isRunning = true;

		collectionTimer.Elapsed += Collect;
#error start timers (also check concurency shit)
	}

	private static async void Collect(object? sender, ElapsedEventArgs e)
	{
#error TODO: serialize, write serialized to buffer, clear messageBuffer and send to remote
	}

	public static void Stop()
	{
		if (!isRunning) return;
		isRunning = false;
#error TODO: stop and flush buffers
	}
}
