// 
//  Configuration.cs
// 
//  Copyright (c) 2018 Couchbase, Inc All rights reserved.
// 
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
// 
//  http://www.apache.org/licenses/LICENSE-2.0
// 
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// 

#nullable enable

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;

using Newtonsoft.Json;
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace BadNetworkPlugin
{
    [UsedImplicitly]
    public sealed class Configuration
    {
        #region Properties

        public NumericDistribution? Latency { get; [UsedImplicitly] set; }

        public NumericDistribution? ReadBandwidth { get; [UsedImplicitly] set; }

        public NumericDistribution? WriteBandwidth { get; [UsedImplicitly] set; }

        #endregion
    }

    public enum DistributionType
    {
        ContinuousUniform,
        Normal,
        LogNormal,
        Cauchy,
        Chi,
        ChiSquared,
        Erlang,
        Exponential,
        FisherSnedecor,
        Gamma,
        InverseGamma,
        Laplace,
        Pareto,
        Rayleigh,
        Stable,
        StudentT,
        Weibull,
        Triangular,
        DiscreteUniform,
        Binomial,
        NegativeBinomial,
        Geometric,
        Hypergeometric,
        Poisson,
        ConwayMaxwellPoisson,
        Zipf
    }

    public enum RandomSourceType
    {
        CryptoRandomSource,
        Mcg31m1,
        Mcg59,
        MersenneTwister,
        Mrg32k3a,
        Palf,
        SystemRandomSource,
        WH1982,
        WH2006,
        Xorshift
    }

    public sealed class NumericDistribution
    {
        private IDistribution? _distribution;

        #region Properties

        [DefaultValue(DistributionType.Normal)]
        [UsedImplicitly]
        public DistributionType Distribution { get; set; } = DistributionType.Normal;

        [DefaultValue(RandomSourceType.SystemRandomSource)]
        [UsedImplicitly]
        public RandomSourceType RandomSourceType { get; set; } = RandomSourceType.SystemRandomSource;

        [Required]
        [UsedImplicitly]
        public object[]? DistributionParameters { get; set; }

        public IDistribution CreateDistribution()
        {
            if (_distribution != null) {
                return _distribution;
            }

            var randomSourceType = Type.GetType($"MathNet.Numerics.Random.{RandomSourceType},MathNet.Numerics") ?? throw new ApplicationException($"Unable to load MathNet.Numerics.Random.{RandomSourceType}");
            var randomSource = Activator.CreateInstance(randomSourceType) as RandomSource;
            
            var distributionType = Type.GetType($"MathNet.Numerics.Distributions.{Distribution},MathNet.Numerics") ?? throw new ApplicationException($"Unable to load MathNet.Numerics.Distributions.{Distribution}");
            if (distributionType.GetConstructors().Any(ConstructorMatches)) {
                _distribution = Activator.CreateInstance(distributionType, DistributionParameters) as IDistribution ?? throw new ApplicationException($"Unable to instantiate {distributionType.FullName}");
                _distribution.RandomSource = randomSource;
                return _distribution;
            }

            var passedParameters = JsonConvert.SerializeObject(DistributionParameters);
            throw new InvalidOperationException($"No constructor found for {distributionType.Name} that has parameters {passedParameters}!");
        }

        private bool ConstructorMatches(ConstructorInfo info)
        {
            var parameters = info.GetParameters();
            if (parameters.Length != DistributionParameters?.Length) {
                return false;
            }

            for (var i = 0; i < parameters.Length; i++) {
                var cType = parameters[i].ParameterType;
                var providedType = DistributionParameters[i].GetType();
                if (!cType.IsAssignableFrom(providedType)) {
                    return false;
                }
            }

            return true;
        }

        #endregion
    }
}