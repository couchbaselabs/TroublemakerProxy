// 
// BadNetworkPlugin.cs
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
using System.Threading.Tasks;
using JetBrains.Annotations;
using MathNet.Numerics.Distributions;
using Serilog;
using TroublemakerInterfaces;

namespace BadNetworkPlugin
{
    /// <summary>
    /// A plugin that simulates poor network conditions
    /// </summary>
    [UsedImplicitly]
    public sealed class BadNetworkPlugin : TroublemakerPluginBase<Configuration>
    {
        #region Variables

        private IDistribution _latencyDistribution;
        private IDistribution _readDistribution;
        private IDistribution _writeDistribution;

        #endregion

        #region Constructors

        /// <inheritdoc />
        public BadNetworkPlugin(ILogger log) : base(log)
        {
        }

        #endregion

        #region Properties

        /// <inheritdoc />
        public override TamperStyle Style => TamperStyle.Network;

        #endregion

        #region Private Methods

        private async Task Delay(IDistribution distribution, int bytesReceived)
        {
            var nextSpeed = NextLatency(distribution);
            if (nextSpeed > 0.0) {
                Log.Verbose("{0} Kbps ({1} KBps)", nextSpeed, nextSpeed / 8);
                await Task.Delay(SpeedOfTransfer(bytesReceived, nextSpeed));
            }
        }

        private async Task InsertLatency(IDistribution distribution)
        {
            var nextMS = NextLatency(distribution);
            await Task.Delay(TimeSpan.FromMilliseconds(nextMS));
        }

        private double NextLatency(IDistribution distribution)
        {
            var retVal = 0.0;
            switch (distribution) {
                case null:
                    break;
                case IContinuousDistribution d:
                    retVal = d.Sample();
                    break;
                case IDiscreteDistribution d:
                    retVal = d.Sample();
                    break;
            }

            return retVal;
        }

        private TimeSpan SpeedOfTransfer(int bytes, double kbps)
        {
            return TimeSpan.FromSeconds(bytes / kbps);
        }

        #endregion

        #region Overrides

        /// <inheritdoc />
        public override async Task<NetworkAction> HandleNetworkStage(NetworkStage stage, int size)
        {
            switch (stage) {
                case NetworkStage.Initial:
                    await InsertLatency(_latencyDistribution).ConfigureAwait(false);
                    break;
                case NetworkStage.Read:
                    await Delay(_readDistribution, size).ConfigureAwait(false);
                    break;
                case NetworkStage.Write:
                    await Delay(_writeDistribution, size).ConfigureAwait(false);
                    break;
            }

            return NetworkAction.Continue;
        }

        /// <inheritdoc />
        protected override bool Init()
        {
            _latencyDistribution = ParsedConfig?.Latency?.CreateDistribution();
            if (_latencyDistribution != null) {
                Log.Information("Latency parameters: {0} milliseconds with random source {1}",
                    _latencyDistribution, ParsedConfig!.Latency!.RandomSourceType);
            }

            _writeDistribution = ParsedConfig?.WriteBandwidth?.CreateDistribution();
            if (_writeDistribution != null) {
                Log.Information("Write bandwidth parameters {0} kilobits/sec with random source {1}",
                    _writeDistribution, ParsedConfig!.WriteBandwidth!.RandomSourceType);
            }

            _readDistribution = ParsedConfig?.ReadBandwidth?.CreateDistribution();
            if (_readDistribution != null) {
                Log.Information("Read bandwidth parameters {0} kilobits/sec with random source {1}",
                    _readDistribution, ParsedConfig!.ReadBandwidth!.RandomSourceType);
            }

            return true;
        }

        #endregion
    }
}