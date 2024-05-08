using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace JobExecutor.Structs;

public class TriggerConverter : CustomCreationConverter<Trigger>
{
    public override Trigger Create(Type objectType)
    {
        // Este método não será usado, a criação ocorrerá em ReadJson
        return null;
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var jsonObject = JObject.Load(reader);
        Trigger trigger;
        if (jsonObject["CronExpression"] != null)
        {
            trigger = new CronJobTrigger();
        }
        else if (jsonObject["WatchedPath"] != null)
        {
            trigger = new FileWatcherJobTrigger();
        }
        else
        {
            throw new JsonSerializationException("Unknown trigger type");
        }

        // Use serializer to populate the properties of the object
        serializer.Populate(jsonObject.CreateReader(), trigger);
        return trigger;
    }
}
