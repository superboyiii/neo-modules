// Copyright (C) 2015-2023 The Neo Project.
//
// The Neo.Plugins.RestServer is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

namespace Neo.Plugins.RestServer.Models.Wallet
{
    /// <summary>
    /// Create account object.
    /// </summary>
    public class WalletCreateAccountModel
    {
        /// <summary>
        /// Private key of the address you want to create. Can be null or empty.
        /// </summary>
        /// <example>CHeABTw3Q5SkjWharPAhgE+p+rGVN9FhlO4hXoJZQqA=</example>
        public byte[] PrivateKey { get; set; }
    }
}
