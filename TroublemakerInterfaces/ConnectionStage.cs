﻿// 
// ConnectionStage.cs
// 
// Copyright (c) 2019 Couchbase, Inc All rights reserved.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// 

using System;

namespace TroublemakerInterfaces
{
    /// <summary>
    /// The kind of tampering that a <see cref="ITroublemakerPlugin"/>
    /// performs.
    /// </summary>
    [Flags]
    public enum TamperStyle
    {
        /// <summary>
        /// Tampers with the network connection between Couchbase Lite
        /// and the other side
        /// </summary>
        Network = 1,

        /// <summary>
        /// Tampers directly with the bytes being sent back and forth
        /// </summary>
        Bytes = 1 << 1,

        /// <summary>
        /// Tampers with the contents of BLIP messages
        /// </summary>
        Message = 1 << 2,

        /// <summary>
        /// Creates an entirely fabricated response without sending the
        /// original request through to the remote
        /// </summary>
        Response = 1 << 3
    }

    /// <summary>
    /// Used to indicate the network stage at which network tampering
    /// is currently taking place
    /// </summary>
    public enum NetworkStage
    {
        /// <summary>
        /// Used to create latency in requests
        /// </summary>
        Initial,

        /// <summary>
        /// Used to throttle read connections
        /// </summary>
        Read,

        /// <summary>
        /// Used to throttle write connections
        /// </summary>
        Write,

        /// <summary>
        /// Used to handle the initial HTTP handshake.
        /// </summary>
        Connect
    }

    /// <summary>
    /// Used to indicate an action at the network stage of troublemaking that is
    /// not accessible from the plugin level
    /// </summary>
    public enum NetworkAction
    {
        /// <summary>
        /// Default, continue transmitting
        /// </summary>
        Continue,

        /// <summary>
        /// Send a web socket close message to close the connection
        /// </summary>
        CloseWebSocket,

        /// <summary>
        /// Forcibly close the TCP stream
        /// </summary>
        BreakPipe,

        /// <summary>
        /// Return a non-success HTTP message during the web socket handshake
        /// </summary>
        CloseHTTP
    }
}