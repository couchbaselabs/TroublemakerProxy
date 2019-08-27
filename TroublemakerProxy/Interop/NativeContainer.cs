// 
// NativeContainer.cs
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
using System.Threading;

namespace TroublemakerProxy.Interop
{
    internal abstract class NativeContainer : IDisposable
    {
        #region Variables

        private IntPtr _nativeHandle;

        #endregion

        #region Properties

        protected IntPtr NativeHandle => _nativeHandle;

        #endregion

        #region Constructors

        protected NativeContainer(IntPtr nativeHandle)
        {
            _nativeHandle = nativeHandle;
        }

        ~NativeContainer()
        {
            ReleaseUnmanagedResources();
        }

        #endregion

        #region Protected Methods

        protected abstract void FreeHandle(IntPtr nativeHandle);

        protected virtual void FreeManaged()
        {

        }

        #endregion

        #region Private Methods

        private void ReleaseUnmanagedResources()
        {
            var old = Interlocked.Exchange(ref _nativeHandle, IntPtr.Zero);
            if (old != IntPtr.Zero) {
                FreeHandle(old);
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            FreeManaged();
            ReleaseUnmanagedResources();
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}