using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace RimeTts;

public sealed class FileTypingListener(
	IOptFileInteractor Opt,
	ILogger<FileTypingListener> Log
):ITypingListener, IDisposable{
	public event Action<IDtoCommit>? CommitReceived;
	public event Action<IDtoKeyEvent>? KeyEventReceived;

	private readonly SemaphoreSlim _readLock = new(1, 1);
	private FileSystemWatcher? _watcher;
	private volatile bool _started;

	public Task StartAsync(CT Ct){
		if(_started){
			return Task.CompletedTask;
		}

		if(string.IsNullOrWhiteSpace(Opt.SignalFile) || string.IsNullOrWhiteSpace(Opt.ContentFile)){
			throw new InvalidOperationException("signal/content file is not configured");
		}

		var signalDir = Path.GetDirectoryName(Opt.SignalFile);
		var signalName = Path.GetFileName(Opt.SignalFile);
		if(string.IsNullOrWhiteSpace(signalDir) || string.IsNullOrWhiteSpace(signalName)){
			throw new InvalidOperationException("invalid signal file path");
		}

		Directory.CreateDirectory(signalDir);
		var contentDir = Path.GetDirectoryName(Opt.ContentFile);
		if(!string.IsNullOrWhiteSpace(contentDir)){
			Directory.CreateDirectory(contentDir);
		}

		_watcher = new FileSystemWatcher(signalDir, signalName){
			NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName | NotifyFilters.Size,
			EnableRaisingEvents = true,
		};
		_watcher.Changed += OnSignal;
		_watcher.Created += OnSignal;
		_watcher.Renamed += OnSignal;

		_started = true;
		Log.LogInformation("typing listener started. signal={SignalFile}; content={ContentFile}", Opt.SignalFile, Opt.ContentFile);
		return Task.CompletedTask;
	}

	public Task StopAsync(CT Ct){
		if(!_started){
			return Task.CompletedTask;
		}
		_started = false;
		if(_watcher is not null){
			_watcher.EnableRaisingEvents = false;
			_watcher.Changed -= OnSignal;
			_watcher.Created -= OnSignal;
			_watcher.Renamed -= OnSignal;
			_watcher.Dispose();
			_watcher = null;
		}
		Log.LogInformation("typing listener stopped");
		return Task.CompletedTask;
	}

	private void OnSignal(obj? Sender, FileSystemEventArgs E){
		if(!_started){
			return;
		}
		_ = Task.Run(ReadAndDispatchAsync);
	}

	private async Task ReadAndDispatchAsync(){
		await _readLock.WaitAsync();
		try{
			await Task.Delay(25);
			if(!File.Exists(Opt.ContentFile)){
				return;
			}

			var json = await File.ReadAllTextAsync(Opt.ContentFile);
			if(string.IsNullOrWhiteSpace(json)){
				return;
			}

			using var doc = JsonDocument.Parse(json);
			if(!doc.RootElement.TryGetProperty("Type", out var typeEl)){
				Log.LogWarning("dto has no Type field");
				return;
			}

			var type = typeEl.GetString() ?? "";
			if(type.Equals(ELuaDtoType.Commit.ToString(), StringComparison.OrdinalIgnoreCase)){
				var text = doc.RootElement.TryGetProperty("Text", out var textEl)
					? (textEl.GetString() ?? "")
					: "";

				if(string.IsNullOrWhiteSpace(text)){
					return;
				}
				CommitReceived?.Invoke(new DtoCommit{ Text = text });
				return;
			}

			if(type.Equals(ELuaDtoType.KeyEvent.ToString(), StringComparison.OrdinalIgnoreCase)){
				KeyEventReceived?.Invoke(new DtoKeyEvent{ AtUtc = DateTimeOffset.UtcNow });
			}
		}
		catch(Exception ex){
			Log.LogWarning(ex, "failed to read/dispatch signal file event");
		}
		finally{
			_readLock.Release();
		}
	}

	public void Dispose(){
		_ = StopAsync(CancellationToken.None);
		_readLock.Dispose();
	}
}
