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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

using MathNet.Numerics.Distributions;
using MathNet.Numerics.Random;

using Newtonsoft.Json;

namespace BadNetworkPlugin
{
    public sealed class Configuration
    {
        #region Properties

        public NumericDistribution Latency { get; set; }

        public NumericDistribution ReadBandwidth { get; set; }

        public NumericDistribution WriteBandwidth { get; set; }

        #endregion
    }

    public enum DistributionType
    {
        ContinuousUniform,
        Normal,
        LogNormal,
        Beta,
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
        Bernoulli,
        Binomial,
        NegativeBinomial,
        Geometric,
        Hypergeometric,
        Poisson,
        Categorical,
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
        private IDistribution _distribution;

        #region Properties

        [DefaultValue(DistributionType.Normal)]
        public DistributionType Distribution { get; set; } = DistributionType.Normal;

        [DefaultValue(RandomSourceType.SystemRandomSource)]
        public RandomSourceType RandomSourceType { get; set; } = RandomSourceType.SystemRandomSource;

        [Required]
        public object[] DistributionParameters { get; set; }

        public IDistribution CreateDistribution()
        {
            if (_distribution != null) {
                return _distribution;
            }

            var randomSourceType = Type.GetType($"MathNet.Numerics.Random.{RandomSourceType},MathNet.Numerics");
            var randomSource = Activator.CreateInstance(randomSourceType) as RandomSource;
            
            var distributionType = Type.GetType($"MathNet.Numerics.Distributions.{Distribution},MathNet.Numerics");
            foreach (var c in distributionType.GetConstructors()) {
                if (ConstructorMatches(c)) {
                    _distribution = Activator.CreateInstance(distributionType, DistributionParameters) as IDistribution;
                    _distribution.RandomSource = randomSource;
                    return _distribution;
                }
            }

            var passedParameters = JsonConvert.SerializeObject(DistributionParameters);
            throw new InvalidOperationException($"No constructor found for {distributionType.Name} that has parameters {passedParameters}!");
        }

        private bool ConstructorMatches(ConstructorInfo info)
        {
            var parameters = info.GetParameters();
            if (parameters.Length != DistributionParameters.Length) {
                return false;
            }

            for (int i = 0; i < parameters.Length; i++) {
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