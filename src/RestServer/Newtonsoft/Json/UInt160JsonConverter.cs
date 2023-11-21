// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Plugins.RestServer.Exceptions;
using Newtonsoft.Json;

namespace Neo.Plugins.RestServer.Newtonsoft.Json
{
    public class UInt160JsonConverter : JsonConverter<UInt160>
    {
        public override bool CanRead => true;
        public override bool CanWrite => true;

        public override UInt160 ReadJson(JsonReader reader, Type objectType, UInt160 existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var value = reader.Value?.ToString();
            try
            {
                return RestServerUtility.ConvertToScriptHash(value, RestServerPlugin.NeoSystem.Settings);
            }
            catch (FormatException)
            {
                throw new ScriptHashFormatException($"{value} is invalid.");
            }
        }

        public override void WriteJson(JsonWriter writer, UInt160 value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }
    }
}
