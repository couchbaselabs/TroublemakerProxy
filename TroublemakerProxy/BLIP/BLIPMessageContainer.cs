// 
// BLIPMessageContainer.cs
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

using TroublemakerProxy.Interop;

namespace TroublemakerProxy.BLIP
{
    internal sealed unsafe class BLIPMessageContainer : NativeContainer
    {
        private bool _hasExtra;

        #region Constructors

        public BLIPMessageContainer() : base((IntPtr) Native.blip_message_new())
        {
        }

        public BLIPMessageContainer(blip_message_t* messageHandle) : base((IntPtr) messageHandle)
        {
        }

        #endregion

        #region Public Methods

        public void ApplyMessage(BLIPMessage message)
        {
            blip_message_t* msg = this;
            if (message.Body != null) {
                _hasExtra = true;
                IntPtr newBody = Marshal.AllocHGlobal(message.Body?.Length ?? 0);
                Marshal.Copy(message.Body, 0, newBody, message.Body.Length);
                msg->body_size = (ulong) message.Body.Length;
                msg->body = (byte*) newBody.ToPointer();
            } else {
                msg->body = null;
                msg->body_size = 0;
            }

            if (message.Properties != null) {
                _hasExtra = true;
                IntPtr newProps = Marshal.StringToCoTaskMemUTF8(message.Properties);
                msg->properties = (byte*) newProps.ToPointer();
            } else {
                msg->properties = null;
            }

            msg->msg_no = message.MessageNumber;
            msg->flags = message.Flags;
            msg->type = message.Type;
        }

        public BLIPMessage CreateMessage()
        {
            blip_message_t* msg = this;
            var retVal = new BLIPMessage
            {
                Body = new byte[msg->body_size],
                Checksum = msg->checksum,
                Flags = msg->flags,
                MessageNumber = msg->msg_no,
                Type = msg->type
            };

            if (msg->properties != null) {
                retVal.Properties = Marshal.PtrToStringUTF8((IntPtr) msg->properties);
            }

            Marshal.Copy((IntPtr)msg->body, retVal.Body, 0, (int)msg->body_size);
            return retVal;
        }

        public static implicit operator blip_message_t*(BLIPMessageContainer container)
        {
            return (blip_message_t*) container.NativeHandle.ToPointer();
        }

        #endregion

        #region Overrides

        protected override void FreeHandle(IntPtr nativeHandle)
        {
            var msg = (blip_message_t *)nativeHandle;
            if (_hasExtra) {
                Marshal.FreeHGlobal((IntPtr)msg->body);
                Marshal.FreeCoTaskMem((IntPtr)msg->properties);
            }

            Native.blip_message_free(msg);
        }

        #endregion
    }
}