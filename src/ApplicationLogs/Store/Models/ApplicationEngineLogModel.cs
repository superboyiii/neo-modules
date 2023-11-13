// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.ApplicationLogs is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Neo.Plugins.Store.States;

namespace Neo.Plugins.Store.Models
{
    public class ApplicationEngineLogModel
    {
        public UInt160 ScriptHash { get; set; } = new();
        public string Message { get; set; } = string.Empty;

        public static ApplicationEngineLogModel Create(EngineLogState logEventState) =>
            new()
            {
                ScriptHash = logEventState.ScriptHash,
                Message = logEventState.Message,
            };
    }
}
