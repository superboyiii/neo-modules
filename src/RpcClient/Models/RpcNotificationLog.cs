using Neo.Json;
using Neo.VM;
using Neo.VM.Types;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcNotificationLog
    {
        public UInt160 ScriptHash { get; set; }
        public string EventName { get; set; }
        public List<StackItem> State { get; set; }

        public JObject ToJson() =>
            new()
            {
                ["contract"] = ScriptHash.ToString(),
                ["eventname"] = EventName,
                ["state"] = State.Select(s => s.ToJson()).ToArray()
            };

        public static RpcNotificationLog FromJson(JObject json) =>
            new()
            {
                ScriptHash = UInt160.Parse(json["contract"].AsString()),
                EventName = json["eventname"].AsString(),
                State = ((JArray)json["state"]).Select(s => Utility.StackItemFromJson((JObject)s)).ToList(),
            };
    }
}
