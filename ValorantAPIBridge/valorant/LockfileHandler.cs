using System;
using System.IO;

namespace ValorantAPITest;

public class LockfileHandler : IDisposable
{
    public event Action LockfileRemove;
    public event Action<LockfileData> LockfileUpdate;

    public LockfileData? LockfileData = null;
    
    private string _previousContent = "";
    private readonly FileSystemWatcher _watcher;
    private readonly string configPath;
    private readonly string lockfilePath;
    
    public LockfileHandler()
    {
        var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        configPath = localAppData + @"\Riot Games\Riot Client\Config";
        lockfilePath = configPath + @"\lockfile";
        _watcher = new FileSystemWatcher(configPath);
            
        _watcher.NotifyFilter = NotifyFilters.CreationTime
                                | NotifyFilters.FileName
                                | NotifyFilters.LastWrite
                                | NotifyFilters.Size;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnDeleted;

        _watcher.Filter = "lockfile";
        _watcher.IncludeSubdirectories = false;
        _watcher.EnableRaisingEvents = true;

        readAndProcess();
    }

    private void readAndProcess()
    {
        try
        {
            using (var fileStream = new FileStream(lockfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var textReader = new StreamReader(fileStream))
            {
                var content = textReader.ReadToEnd();
                processContent(content);
            }
        }
        catch (Exception ignored)
        {
        }
    }
    
    private void processContent(string content)
    {
        if (content == _previousContent || !content.Contains(':')) return;
                
        _previousContent = content;
            
        string[] parts = content.Split(':');
        if (parts.Length < 5) return;
        LockfileData = new LockfileData(parts[0], int.Parse(parts[1]), int.Parse(parts[2]), parts[3], parts[4]);
            
        LockfileUpdate?.Invoke(LockfileData);
    }
    
    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed) return;
        readAndProcess();
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        if (_previousContent == "") return;

        _previousContent = "";
        LockfileData = null;
        LockfileRemove?.Invoke();
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }
}