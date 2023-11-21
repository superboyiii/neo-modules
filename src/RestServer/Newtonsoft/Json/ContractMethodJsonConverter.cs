// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.SmartContract.Manifest;
using Newtonsoft.Json;

namespace Neo.Plugins.RestServer.Newtonsoft.Json;

public class ContractMethodJsonConverter : JsonConverter<ContractMethodDescriptor>
{
    public override bool CanRead => false;

    public override bool CanWrite => true;

    public override ContractMethodDescriptor ReadJson(JsonReader reader, Type objectType, ContractMethodDescriptor existingValue, bool hasExistingValue, JsonSerializer serializer) => throw new NotImplementedException();
    public override void WriteJson(JsonWriter writer, ContractMethodDescriptor value, JsonSerializer serializer)
    {
        var j = RestServerUtility.ContractMethodToJToken(value, serializer);
        j.WriteTo(writer);
    }
}
