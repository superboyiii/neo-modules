// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Network.RPC is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Json;
using System.Linq;

namespace Neo.Network.RPC.Models
{
    public class RpcRequest
    {
        public JToken Id { get; set; }

        public string JsonRpc { get; set; }

        public string Method { get; set; }

        public JToken[] Params { get; set; }

        public static RpcRequest FromJson(JObject json)
        {
            return new RpcRequest
            {
                Id = json["id"],
                JsonRpc = json["jsonrpc"].AsString(),
                Method = json["method"].AsString(),
                Params = ((JArray)json["params"]).ToArray()
            };
        }

        public JObject ToJson()
        {
            var json = new JObject();
            json["id"] = Id;
            json["jsonrpc"] = JsonRpc;
            json["method"] = Method;
            json["params"] = new JArray(Params);
            return json;
        }
    }
}
