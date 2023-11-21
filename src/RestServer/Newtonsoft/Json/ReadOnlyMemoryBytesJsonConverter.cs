// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class ReadOnlyMemoryBytesJsonConverter : JsonConverter<ReadOnlyMemory<byte>>
    {
        public override ReadOnlyMemory<byte> ReadJson(JsonReader reader, Type objectType, ReadOnlyMemory<byte> existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var o = JToken.Load(reader);
            return Convert.FromBase64String(o.ToObject<string>());
        }

        public override void WriteJson(JsonWriter writer, ReadOnlyMemory<byte> value, JsonSerializer serializer)
        {
            writer.WriteValue(Convert.ToBase64String(value.Span));
        }
    }
}
