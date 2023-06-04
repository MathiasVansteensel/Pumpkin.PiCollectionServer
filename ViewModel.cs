using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using System.Drawing;

namespace Pumpkin.PiCollectionServer;

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
			if (_downloadedPackets == value) return;
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
			if (_uploadedPackets == value) return;
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
			if (_throughput == value) return;
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
			if (_errors == value) return;
			_errors = value;
			SaveModel();
		}
	}

	private string _email = "mathias.vansteensel@gmail.com";
	public string Email
	{
		get => _email;
		set
		{
			if (_email == value) return;
			_email = value;
			SaveModel();
		}
	}

	private Guid _hwid = Guid.Empty;
	public Guid HWID
	{
		get => _hwid;
		set
		{
			if (_hwid == value) return;
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
					_instance = new(null);
					return _instance;
				}
			}
			return _instance;
		}
		private set => _instance = value;
	}

	public ViewModel()
	{
	}

	private ViewModel(object @private) //just here to solve stupid ambiguity
	{
		if (LoadModel().GetAwaiter().GetResult() is ViewModel model)
		{
			DownloadedPackets = model.DownloadedPackets;
			UploadedPackets = model.UploadedPackets;
			Throughput = model.Throughput;
			Errors = model.Errors;
			Email = model.Email;
			HWID = model.HWID;
			Console.WriteLine("Loaded viewmodel");
		}
		else
		{
			HWID = Guid.NewGuid();
			Console.WriteLine("Creating new viewmodel");
		}

		//yes... i know this is not how singletons work... but i need to deserialize this mess and i cant do that without a parameterless public ctor
		Instance = this;
	}

	//no i'm not gonna work with locks, they cant even be async... WHY
	private static SemaphoreSlim fileSemaphore = new(1, 1);

	internal async void SaveModel()
	{
		await Task.Run(async () =>
		{
			using (MemoryStream bufferStream = new())
			{
				try
				{
					await JsonSerializer.SerializeAsync(bufferStream, Instance);
				}
				catch (Exception)
				{
					return;
				}

				try
				{
					await fileSemaphore.WaitAsync();
					using (FileStream fileStream = File.OpenWrite(ModelFile))
					{
						bufferStream.Seek(0, SeekOrigin.Begin);
						fileStream.Seek(0, SeekOrigin.Begin);
						await bufferStream.CopyToAsync(fileStream);
						await fileStream.FlushAsync();
					}
				}
				finally 
				{ 
					fileSemaphore.Release(); 
				}
			}
		}).ConfigureAwait(false);
	}

	//task bc async void may not complete before we start using the model, which would require a taskcompletionsource, but i dont wanna bother with that rn
	internal async Task<ViewModel> LoadModel()
	{
		await fileSemaphore.WaitAsync();
		if (!File.Exists(ModelFile))
		{
			string dirName = Path.GetDirectoryName(ModelFile);
			if (!Directory.Exists(dirName)) Directory.CreateDirectory(dirName);
			using (Stream file = File.Create(ModelFile)) //u shouldnt manually dispose of async streams... bad idea
			fileSemaphore.Release();
			return null;
		}

		ViewModel result;
		MemoryStream bufferStream = new();

		try
		{
			using (var fileStream = File.Open(ModelFile, FileMode.Open, FileAccess.Read))
			{
				//copy file to ram first to avoid loading and saving the model simultaneously, which can obviously cause a deadlock (not speaking from experience or anything :) )
				fileStream.Seek(0, SeekOrigin.Begin);
				await fileStream.CopyToAsync(bufferStream);
				await bufferStream.FlushAsync();
			}
		}
		finally
		{
			fileSemaphore.Release();
		}

		try
		{
			bufferStream.Seek(0, SeekOrigin.Begin);
			result = (ViewModel)await JsonSerializer.DeserializeAsync(bufferStream, typeof(ViewModel));
			bufferStream.Close();
			if (bufferStream is not null) await bufferStream.DisposeAsync();
		}
		catch (Exception ex)
		{
			result = null;
		}
		return result;
	}
}
