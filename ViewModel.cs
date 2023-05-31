using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pumpkin.PiCollectioServer;

//i know this is not a true viewmodel but i didn't know any other name... naming things is hard and MVVM is still my fav architecture
//also i know it's not static but that's because i'm lazy and then it's easier to serialize
public class ViewModel
{
	internal const string ModelFile = "ViewModel/Model.json";

	private long _downloadedPackets = 0;
	public long DownloadedPackets
	{
		get => _downloadedPackets;
		set
		{
			if (_downloadedPackets != value) return;
			_downloadedPackets = value;
			SaveModel();
		}
	}

	private long _uploadedPackets = 0;
	public long UploadedPackets
	{
		get => _uploadedPackets;
		set
		{
			if (_uploadedPackets != value) return;
			_uploadedPackets = value;
			SaveModel();
		}
	}

	private long _throughput = 0;
	public long Throughput
	{
		get => _throughput;
		set
		{
			if (_throughput != value) return;
			_throughput = value;
			SaveModel();
		}
	}

	private long _errors = 0;
	public long Errors
	{
		get => _errors;
		set
		{
			if (_errors != value) return;
			_errors = value;
			SaveModel();
		}
	}

	private Guid _hwid = Guid.NewGuid();
	public Guid HWID
	{
		get => _hwid;
		set
		{
			if (_hwid != value) return;
			_hwid = value;
			SaveModel();
		}
	}

	private static readonly object _lock = new object();

	private static ViewModel _instance;
	public static ViewModel Instance 
	{
		get 
		{
			if (_instance is null) 
			{
				lock (_lock) //i hate locks...
				{
					_instance = new();
					return _instance;
				}
			}
			return _instance;
		}
	}

	private ViewModel()
	{
		if (LoadModel().GetAwaiter().GetResult() is ViewModel model) 
		{
			DownloadedPackets = model.DownloadedPackets;
			UploadedPackets = model.UploadedPackets;
			Throughput = model.Throughput;
			Errors = model.Errors;
			return;
		}
		DownloadedPackets = default;
		UploadedPackets = default;
		Throughput = default;
		Errors = default;
	}

	internal async void SaveModel() 
	{
		using (var stream = File.Open(ModelFile, FileMode.OpenOrCreate, FileAccess.Write)) 
		{
			await JsonSerializer.SerializeAsync(stream, this);
			await stream.FlushAsync(); //this buffer crap can cause issue with file lock, but should be fine i guess (i want unbuffered streams, but i'm not gonna write one rn)
			stream.Close();
		}
	}

	//task bc async void may not complete before we start using the model, which would require a taskcompletionsource, but i dont wanna bother with that rn
	internal async Task<ViewModel> LoadModel()
	{
		if (!File.Exists(ModelFile))
		{
			string dirName = Path.GetDirectoryName(ModelFile);
			if (!Directory.Exists(dirName)) Directory.CreateDirectory(dirName);
			File.Create(ModelFile).Dispose();
			return null;
		}
		using (var stream = File.Open(ModelFile, FileMode.Open, FileAccess.Read))
		{
			try
			{
				return (ViewModel)await JsonSerializer.DeserializeAsync(stream, typeof(ViewModel));
			}
			catch (Exception)
			{
				return null;
			}
		}
	}
}
