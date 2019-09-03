// 
// Native.cs
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
using System.Runtime.InteropServices;

using TroublemakerInterfaces;

namespace TroublemakerProxy.Interop
{
    internal struct blip_connection_t
    {
    }

    internal unsafe struct blip_message_t
    {
        #region Variables
        
        public ulong msg_no;
        public MessageType type;
        public FrameFlags flags;
        public byte* properties;
        public byte* body;
        private UIntPtr _body_size;
        public int checksum;
        private int calculated_checksum;
        private fixed ulong _private[4];

        #endregion

        #region Properties

        public ulong body_size
        {
            get => _body_size.ToUInt64();
            set => _body_size = (UIntPtr) value;
        }

        #endregion
    }

    internal static unsafe class Native
    {
        #region Public Methods

        [DllImport("CBlip", CallingConvention = CallingConvention.Cdecl)]
        public static extern void blip_connection_free(blip_connection_t* connection);

        [DllImport("CBlip", CallingConvention = CallingConvention.Cdecl)]
        public static extern blip_connection_t* blip_connection_new();

        [DllImport("CBlip", CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong blip_get_message_ack_size(blip_message_t* message);

        [DllImport("CBlip", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.Cdecl)]
        public static extern string blip_get_message_type(blip_message_t* message);

        [DllImport("CBlip", CallingConvention = CallingConvention.Cdecl)]
        public static extern void blip_message_free(blip_message_t* message);

        [DllImport("CBlip", CallingConvention = CallingConvention.Cdecl)]
        public static extern blip_message_t* blip_message_new(blip_connection_t* connection, byte* data,
            UIntPtr size);

        [DllImport("CBlip", CallingConvention = CallingConvention.Cdecl)]
        public static extern byte* blip_message_serialize(blip_message_t* message, UIntPtr* out_size);

        #endregion
    }
}