// 
// BLIPMessage.cs
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
    /// An enum representing the type of BLIP message
    /// </summary>
    public enum MessageType
    {
        /// <summary>
        /// A request to the other side for an action
        /// </summary>
        Request = 0,

        /// <summary>
        /// A successful response to a <see cref="Request"/>
        /// </summary>
        Response = 1,

        /// <summary>
        /// An error response to a <see cref="Request"/>
        /// </summary>
        Error = 2,

        /// <summary>
        /// For messages that require a long time to process,
        /// an acknowledgement that a <see cref="Request"/>
        /// has been received
        /// </summary>
        AckRequest = 4,

        /// <summary>
        /// For messages that require a long time to process,
        /// an acknowledgement that a <see cref="Response"/>
        /// has been received
        /// </summary>
        AckResponse = 5
    }

    /// <summary>
    /// Flags indicating information about a particular BLIP message
    /// </summary>
    [Flags]
    public enum FrameFlags
    {
        /// <summary>
        /// Used to isolate the <see cref="MessageType"/> of the message
        /// </summary>
        TypeMask = 0x07,

        /// <summary>
        /// Indicates that the message payload is compressed
        /// </summary>
        Compressed = 0x08,

        /// <summary>
        /// Indicates that this message needs a higher priority
        /// </summary>
        Urgent = 0x10,

        /// <summary>
        /// Indicates that no <see cref="MessageType.Response"/> needs
        /// to be send for this <see cref="MessageType.Request"/>
        /// </summary>
        NoReply = 0x20,

        /// <summary>
        /// Indicates that this message is not finished yet
        /// </summary>
        MoreComing = 0x40
    }

    /// <summary>
    /// A class representing a BLIP message
    /// </summary>
    public sealed class BLIPMessage
    {
        #region Properties

        /// <summary>
        /// The message body of the BLIP message
        /// </summary>
        public byte[] Body { get; set; }

        /// <summary>
        /// The CRC32 checksum at the end of the BLIP message
        /// </summary>
        public int Checksum { get; set; }

        /// <summary>
        /// The flags set on this message
        /// </summary>
        public FrameFlags Flags { get; set; }

        /// <summary>
        /// The message number (used for context in request-response
        /// situations)
        /// </summary>
        public ulong MessageNumber { get; set; }

        /// <summary>
        /// The properties of this message (string of key-value
        /// entries separated by ':')
        /// </summary>
        public string Properties { get; set; }

        /// <summary>
        /// The type of this message
        /// </summary>
        public MessageType Type { get; set; }

        #endregion
    }
}