using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace ValorantAPIBridge.whitelist;

public class OriginWhitelist
{
    public ObservableCollection<WhitelistItem> Whitelist { get; set; } = new();
    public ObservableCollection<WhitelistRequest> Requests { get; } = new();
    private static readonly string FileName = "whitelist.json";

    public bool IsWhitelisted(string origin)
    {
        foreach (WhitelistItem item in Whitelist)
        {
            if (item.Origin == origin)
            {
                return true;
            }
        }

        return false;
    }

    public bool BumpIfExists(string origin)
    {
        foreach (WhitelistItem item in Whitelist)
        {
            if (item.Origin == origin)
            {
                item.LastUsed = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                return true;
            }
        }

        return false;
    }
    
    public void AddToWhitelist(WhitelistItem item)
    {
        Whitelist.Add(item);
    }

    public void RemoveFromWhitelist(WhitelistItem item)
    {
        Whitelist.Remove(item);
    }

    public void SaveToFile()
    {
        File.WriteAllBytes(FileName, JsonSerializer.SerializeToUtf8Bytes(Whitelist));
    }
    
    public void LoadFromFile(bool createIfNotExists)
    {
        try
        {
            byte[] data = File.ReadAllBytes(FileName);
            Whitelist = JsonSerializer.Deserialize<ObservableCollection<WhitelistItem>>(data);
        }
        catch (FileNotFoundException e)
        {
            if (createIfNotExists)
            {
                SaveToFile();
            }
        }
    }

    public bool RequestWhitelist(WhitelistRequest request, Action<bool> callback)
    {
        foreach (WhitelistRequest item in Requests)
        {
            if (item.Origin == request.Origin)
            {
                return false;
            }
        }

        Requests.Add(request);
        
        Application.Current.Dispatcher.Invoke((Action)delegate{
            var requestWindow = new WhitelistRequestWindow(passed =>
            {
                Requests.Remove(request);
                if (passed)
                {
                    AddToWhitelist(new WhitelistItem
                    {
                        Added = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                        LastUsed = 0,
                        Name = request.Name,
                        Origin = request.Origin
                    });
                    SaveToFile();
                }
                callback.Invoke(passed);
            }, request);
            requestWindow.Show();
            requestWindow.Activate();
            requestWindow.Focus();
        });
        
        return true;
    }
}