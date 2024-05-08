using JobExecutor.Structs;
using NCrontab;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Text;

namespace JobExecutor;

class Program
{
    private static List<Trigger> triggers;
    private static string configPath = Path.Combine(Environment.CurrentDirectory, "triggers.json");
    private static Dictionary<string, DateTime> scriptExecutionCache = new Dictionary<string, DateTime>();

    static void Main(string[] args)
    {
        SetupConfigFileWatcher();

        ReloadTriggers();

        Console.ReadLine();
    }

    private static void SetupConfigFileWatcher()
    {
        var configWatcher = new FileSystemWatcher
        {
            Path = Path.GetDirectoryName(configPath),
            Filter = Path.GetFileName(configPath),
            NotifyFilter = NotifyFilters.LastWrite
        };

        configWatcher.Changed += (sender, e) =>
        {
            Console.WriteLine("Configuration file changed. Reloading triggers...");
            ReloadTriggers();
        };

        configWatcher.EnableRaisingEvents = true;
    }

    private static void ReloadTriggers()
    {
        triggers = ReadTriggerConfig().Triggers.ToList();
        InitializeTriggers();
    }

    private static void InitializeTriggers()
    {
        foreach (var trigger in triggers)
        {
            switch (trigger.Type)
            {
                case "CronExpression":
                    SetupCronJob(trigger as CronJobTrigger);
                    break;
                case "FileWatcher":
                    SetupFileWatcher(trigger as FileWatcherJobTrigger);
                    break;
                default:
                    throw new NotImplementedException($"Trigger type {trigger.Type} is not implemented.");
            }
        }
    }

    private static void SetupFileWatcher(FileWatcherJobTrigger trigger)
    {
        var watcher = new FileSystemWatcher
        {
            Path = trigger.WatchedPath,
            Filter = "*",
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
        };

        void OnChange(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"File {e.Name} {e.ChangeType}");
            if (ScriptCanBeRunned(e.FullPath))
            {
                return;
            }

            ExecuteScript(
                trigger, 
                $"-EventType \"{e.ChangeType}\"", 
                $"-Name \"{e.Name}\"", 
                $"-FullPath \"{e.FullPath}\"");

            CacheScriptExecution(e.FullPath);
        }

        watcher.NotifyFilter = NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.Attributes;

        watcher.Changed += OnChange;
        watcher.Created += OnChange;
        watcher.Deleted += OnChange;
        watcher.Renamed += OnChange;
    }


    private static void CacheScriptExecution(string path)
    {
        scriptExecutionCache[path] = DateTime.Now;
    }

    private static bool ScriptCanBeRunned(string path)
    {
        if (!scriptExecutionCache.ContainsKey(path))
        {
            return false;
        }

        var lastExecution = scriptExecutionCache[path];
        return lastExecution.AddSeconds(1) > DateTime.Now;
    }

    private static void SetupCronJob(CronJobTrigger? trigger)
    {
        if (trigger == null)
        {
            return;
        }

        var schedule = CrontabSchedule.Parse(trigger.CronExpression, new CrontabSchedule.ParseOptions() { IncludingSeconds = true });
        var nextRun = schedule.GetNextOccurrence(DateTime.Now);
        var timer = new Timer((e) =>
        {
            ExecuteScript(trigger);
            SetupCronJob(trigger);
        }, null, (long)(nextRun - DateTime.Now).TotalMilliseconds, Timeout.Infinite);
    }

    private static void ExecuteScript(Trigger trigger, params string[] parameters)
    {
        if (!File.Exists(trigger.ScriptFileName))
        {
            Console.WriteLine($"File not found: {trigger.ScriptFileName}");
            return;
        }

        if (!trigger.ScriptFileName.EndsWith(".ps1"))
        {
            throw new NotImplementedException($"Script type {trigger.ScriptFileName} is not implemented.");
        }

        var startInfo = new ProcessStartInfo()
        {
            FileName = "powershell.exe",
            Arguments = $"-ExecutionPolicy Bypass -File \"{trigger.ScriptFileName}\" {string.Join(' ', parameters)}"
        };

        Process.Start(startInfo);
    }

    private static TriggerConfig ReadTriggerConfig()
    {
        CreateFileIfNotExists(Path.GetDirectoryName(configPath));

        var settings = new JsonSerializerSettings
        {

            Converters = new List<JsonConverter> { new TriggerConverter() }
        };

        using var stream = new StreamReader(configPath);
        string json = stream.ReadToEnd();
        return JsonConvert.DeserializeObject<TriggerConfig>(json, settings);

    }

    private static void CreateFileIfNotExists(string directoryPath)
    {
        var filePath = Path.Combine(directoryPath, "triggers.json");
        if (!File.Exists(filePath))
        {
            var triggers = new TriggerConfig
            {
                Triggers = new List<Trigger>().ToArray()
            };

            using var stream = new StreamWriter(filePath);
            stream.Write(JsonConvert.SerializeObject(triggers));
        }
    }
}
