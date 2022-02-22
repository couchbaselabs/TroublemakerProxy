// 
// ITroublemakerPlugin.cs
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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Serilog;

[assembly: InternalsVisibleTo("TroublemakerProxy")]

namespace TroublemakerInterfaces
{
    /// <summary>
    /// The interface that a plugin must implement (via <see cref="TroublemakerPluginBase"/>
    /// or <see cref="TroublemakerPluginBase{T}"/>) in order to be able to be used in the troublemaker proxy.
    /// </summary>
    public interface ITroublemakerPlugin
    {
        #region Properties

        /// <summary>
        /// Gets the style in which this plugin tampers with
        /// the connection between Couchbase Lite and the
        /// other side
        /// </summary>
        TamperStyle Style { get; }

        #endregion

        #region Public Methods

        /// <summary>
        /// If a configuration file was provided, then this method receives the
        /// contents of the configuration file as a stream (configuration file
        /// must be valid JSON)
        /// </summary>
        /// <param name="configData">The stream of JSON config data</param>
        /// <returns><c>true</c> if configuration succeeded, <c>false</c> otherwise</returns>
        bool Configure(Stream configData);

        /// <summary>
        /// If the plugin has the <see cref="TamperStyle.Bytes"/> flag then this
        /// method is used to send the bytes for tampering
        /// </summary>
        /// <param name="bytes">The object containing the bytes</param>
        /// <param name="count">How many bytes were used inside the array</param>
        /// <param name="fromClient"><c>true</c> if this is from local Couchbase Lite, otherwise <c>false</c></param>
        /// <returns>An awaitable task</returns>
        Task HandleBytesStage(ArraySegment<byte> bytes, int count, bool fromClient);

        /// <summary>
        /// If the plugin has the <see cref="TamperStyle.Message"/> flag then this
        /// method is used to tamper with the BLIP message
        /// </summary>
        /// <param name="message">The current BLIP message</param>
        /// <param name="fromClient"><c>true</c> if this is from local Couchbase Lite, otherwise <c>false</c></param>
        /// <returns>An awaitable task</returns>
        Task HandleMessageStage(ref BLIPMessage message, bool fromClient);

        /// <summary>
        /// If the plugin has the <see cref="TamperStyle.Network"/> flag then this
        /// method is used to tamper with the network connection
        /// </summary>
        /// <param name="stage">The stage of network that is currently being tampered with</param>
        /// <param name="size">The number of bytes being sent (-1 in the case of <see cref="NetworkStage.Initial"/>)</param>
        /// <returns>An awaitable task that returns an action to take regarding the network</returns>
        Task<NetworkAction> HandleNetworkStage(NetworkStage stage, int size);

        /// <summary>
        /// If the plugin has the <see cref="TamperStyle.Response"/> flag then this
        /// method is used to create a false response to a message
        /// </summary>
        /// <param name="message">The message being sent (always REQ)</param>
        /// <param name="fromClient">Whether or not the request is from the local side</param>
        /// <returns>An awaitable task holding the response to be sent.  If <c>null</c> is returned, then
        /// the interception is cancelled and the REQ is sent normally</returns>
        Task<BLIPMessage?> HandleResponseStage(BLIPMessage message, bool fromClient);

        #endregion
    }

    /// <summary>
    /// Abstract base class for a troublemaker proxy plugin that does not require
    /// any configuration
    /// </summary>
    public abstract class TroublemakerPluginBase : ITroublemakerPlugin
    {
        #region Properties

        /// <inheritdoc />
        public abstract TamperStyle Style { get; }

        /// <summary>
        /// The log object to write logs for this plugin
        /// </summary>
        protected internal ILogger Log { get; }

        #endregion

        #region Constructors

        protected TroublemakerPluginBase(ILogger log)
        {
            Log = log;
        }

        #endregion

        #region ITroublemakerPlugin

        /// <inheritdoc />
        public virtual bool Configure(Stream configData) => true;

        /// <inheritdoc />
        public virtual Task HandleBytesStage(ArraySegment<byte> bytes, int count, bool fromClient) =>
            Task.CompletedTask;

        /// <inheritdoc />
        public virtual Task HandleMessageStage(ref BLIPMessage message, bool fromClient) => Task.CompletedTask;

        /// <inheritdoc />
        public virtual Task<NetworkAction> HandleNetworkStage(NetworkStage stage, int size) => Task.FromResult(NetworkAction.Continue);
        
        /// <inheritdoc />
        public virtual Task<BLIPMessage?> HandleResponseStage(BLIPMessage message, bool fromClient) =>
            Task.FromResult<BLIPMessage?>(null);

        #endregion
    }

    /// <summary>
    /// A specialized form of troublemaker proxy plugin that automatically parses
    /// its configuration file (JSON) and makes the result available.
    /// </summary>
    /// <typeparam name="T">The type to deserialize the contents of the configuration file as</typeparam>
    public abstract class TroublemakerPluginBase<T> : TroublemakerPluginBase
    {
        #region Properties

        protected T? ParsedConfig { get; private set; }

        #endregion

        #region Constructors

        protected TroublemakerPluginBase(ILogger log) : base(log)
        {
        }

        #endregion

        #region Protected Methods

        /// <summary>
        /// Override to perform any needed setup after the parsing of
        /// the configuration is done
        /// </summary>
        /// <returns><c>true</c> on success, <c>false</c> on failure</returns>
        protected virtual bool Init() => true;

        #endregion

        #region Overrides

        /// <inheritdoc />
        public sealed override bool Configure(Stream configData)
        {
            using var reader = new StreamReader(configData);
            using var jsonReader = new JsonTextReader(reader);
            ParsedConfig = JsonSerializer.CreateDefault().Deserialize<T>(jsonReader);
            return Init();
        }

        #endregion
    }
}