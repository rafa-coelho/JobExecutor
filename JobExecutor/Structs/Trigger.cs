namespace JobExecutor.Structs;

public class TriggerConfig
{
    public Trigger[] Triggers { get; set; }
}

public class Trigger
{
    public string ScriptFileName { get; set; }
    public string Type { get; set; }

}

public class CronJobTrigger : Trigger
{
    public string CronExpression { get; set; }
}

public class FileWatcherJobTrigger : Trigger
{
    public string WatchedPath { get; set; }
}