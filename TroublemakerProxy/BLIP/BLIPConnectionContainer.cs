﻿// 
// BLIPConnectionContainer.cs
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

#nullable enable

using System;
using System.IO;
using System.Runtime.InteropServices;

using TroublemakerInterfaces;

using TroublemakerProxy.Interop;

namespace TroublemakerProxy.BLIP
{
    internal sealed unsafe class BLIPConnectionContainer : NativeContainer
    {
        #region Variables

        private MemoryStream _buffer = new();
        private readonly string _name;

        #endregion

        #region Constructors

        public BLIPConnectionContainer(string name) 
            : this(Native.blip_connection_new(), name)
        {

        }

        private BLIPConnectionContainer(blip_connection_t* nativeHandle, string name) : base((IntPtr) nativeHandle)
        {
            _name = name;
        }

        #endregion

        #region Public Methods

        public static implicit operator blip_connection_t*(BLIPConnectionContainer container)
        {
            return (blip_connection_t*) container.NativeHandle.ToPointer();
        }

        public BLIPMessageContainer ReadMessage(Stream payload)
        {
            payload.Seek(0, SeekOrigin.Begin);
            payload.CopyTo(_buffer);
            var bytes = _buffer.ToArray();
            BLIPMessageContainer container;
            fixed (byte* b = bytes) {
                var nativeMessage = Native.blip_message_read(this, b, (UIntPtr) bytes.Length);
                container = new BLIPMessageContainer(nativeMessage);
            }

            _buffer.Dispose();
            _buffer = new MemoryStream();
            return container;
        }

        public byte[] SerializeMessage(BLIPMessageContainer container, BLIPMessage message)
        {
            container.ApplyMessage(message);
            UIntPtr size;
            byte* bytes = Native.blip_message_serialize(this, container, &size);
            var retVal = new byte[size.ToUInt32()];
            Marshal.Copy((IntPtr) bytes, retVal, 0, (int) size.ToUInt32());
            return retVal;
        }

        #endregion

        #region Overrides

        protected override void FreeHandle(IntPtr nativeHandle)
        {
            Native.blip_connection_free((blip_connection_t*) nativeHandle);
        }

        protected override void FreeManaged()
        {
            _buffer.Dispose();
        }

        public override string ToString()
        {
            return _name;
        }

        #endregion
    }
}