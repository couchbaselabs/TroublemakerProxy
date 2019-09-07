# BadNetworkPlugin Config Options

## Distribution

A type of probability distribution to use when calculating random number patterns.  Each of them has one or more distribution parameters which are passed via the `DistributionParameters` key.  Choose from the following string keys (* = default value if the `Distribution` key is omitted).

| Type | Parameter 1 | Parameter 2 | Parameter 3 | Parameter 4 |
| ---- | ----------- | ------------| ----------- | ----------- |
| [ContinuousUniform](https://en.wikipedia.org/wiki/Uniform_distribution_%28continuous%29) | a (minimum) | b ≥ minimum (maximum) |
| *[Normal](https://en.wikipedia.org/wiki/Normal_distribution) | μ (mean) | σ ≥ 0 (standard deviation)
| [LogNormal](https://en.wikipedia.org/wiki/Log-normal_distribution) | μ (mean) | σ ≥ 0 (standard deviation)
| [Cauchy](https://en.wikipedia.org/wiki/Cauchy_distribution) | x0 (Location) | γ > 0 (scale)
| [Chi](https://en.wikipedia.org/wiki/Chi_distribution) | k > 0 (degrees of freedom)
| [ChiSquared](https://en.wikipedia.org/wiki/Chi-squared_distribution) | k > 0 (degrees of freedom)
| [Erlang](https://en.wikipedia.org/wiki/Erlang_distribution) | k ≥ 0 (shape) | λ > 0 (rate)
| [Exponential](https://en.wikipedia.org/wiki/Exponential_distribution) | λ ≥ 0 (rate)
| [FisherSnedecor](http://en.wikipedia.org/wiki/F-distribution) | d1 > 0 (first degree of freedom) | d2 > 0 (second DOF)
| [Gamma](http://en.wikipedia.org/wiki/Gamma_distribution) | α ≥ 0 (shape) | β ≥ 0 (rate)
| [InverseGamma](http://en.wikipedia.org/wiki/inverse-gamma_distribution) | α > 0 (shape) | β > 0 (rate)
| [Laplace](http://en.wikipedia.org/wiki/Laplace_distribution) | μ (location) | b > 0 (scale)
| [Pareto](http://en.wikipedia.org/wiki/Pareto_distribution) | xm > 0 (scale) | α > 0 (shape)
| [Rayleigh](http://en.wikipedia.org/wiki/Rayleigh_distribution) | σ > 0 (scale)
| [Stable](http://en.wikipedia.org/wiki/Stable_distribution) | 2 ≥ α > 0 (stability) | 1 ≥ β ≥ -1 (skewness) | c > 0 (scale) | μ (location)
| [StudentT](http://en.wikipedia.org/wiki/Student%27s_t-distribution) | μ (location) | σ > 0 (scale) | ν > 0 (degrees of freedom)
| [Weibull](http://en.wikipedia.org/wiki/Weibull_distribution) | k > 0 (shape) | λ > 0 (scale)
| [Triangular](https://en.wikipedia.org/wiki/Triangular_distribution) | (lower) ≤ mode ≤ upper | lower ≤ mode ≤ (upper) | lower ≤ (mode) ≤ upper
| [DiscreteUniform](http://en.wikipedia.org/wiki/Uniform_distribution_%28discrete%29) | (lower) ≤ upper | lower ≤ (upper)
| [Binomial](http://en.wikipedia.org/wiki/Binomial_distribution) | 0 ≤ p ≤ 1 (success probability) | n ≥ 0 (number of trials) |
| [NegativeBinomial](https://en.wikipedia.org/wiki/Negative_binomial_distribution) | r ≥ 0 (successes required to stop) | 0 ≤ p ≤ 1 (probability of success)
| [Geometric](http://en.wikipedia.org/wiki/geometric_distribution) | 0 ≤ p ≤ 1 (probability of generating one)
| [Hypergeometric](https://en.wikipedia.org/wiki/Hypergeometric_distribution) | N (size of population) | M (Number of successes in population) | n (Number of draws without replacement)
| [Poisson](https://en.wikipedia.org/wiki/Poisson_distribution) | λ > 0 (lambda)
| [ConwayMaxwellPoisson](http://en.wikipedia.org/wiki/Conway%E2%80%93Maxwell%E2%80%93Poisson_distribution) | λ > 0 (lambda) | ν ≥ 0 (rate of decay)
| [Zipf](https://en.wikipedia.org/wiki/Zipf%27s_law) | s (exponent) | n (number of elements)

Some of the above have default arguments which are listed below and will be used if the distribution parameters portion is empty or missing

| Type | Default 1 | Default 2 | Default 3 |
| ---- | --------- | --------- | --------- |
| ContinuousUniform | 0.0 | 1.0 |
| Normal | 0.0 | 1.0 |
| Cauchy | 0 | 1 |
| Laplace | 0.0 | 1.0 |
| StudentT | 0.0 | 1.0 | 1.0 |

## Source of Entropy

The following values can be passed in, as strings, in the `RandomSource` key (* = default):

| Key | Description |
| --- | ----------- |
| CryptoRandomSource | Wraps the .NET [RNGCryptoServiceProvider](https://docs.microsoft.com/en-us/dotnet/api/system.security.cryptography.rngcryptoserviceprovider?view=netframework-4.8) |
| Mcg31m1 | [Multiplicative congruential generator](https://en.wikipedia.org/wiki/Linear_congruential_generator) using a modulus of 2^31-1 and a multiplier of 1132489760
| Mcg59 | [Multiplicative congruential generator](https://en.wikipedia.org/wiki/Linear_congruential_generator) using a modulus of 2^59 and a multiplier of 13^13 |
| MersenneTwister | [Mersenne Twister 19937](https://en.wikipedia.org/wiki/Mersenne_Twister) generator |
| Mrg32k3a | 32-bit combined [multiple recursive generator](https://en.wikipedia.org/wiki/Combined_linear_congruential_generator) with 2 components of order 3
| Palf | Parallel Additive [Lagged Fibonacci generator](https://en.wikipedia.org/wiki/Lagged_Fibonacci_generator) |
| *SystemRandomSource | Wraps the .NET [Random](https://docs.microsoft.com/en-us/dotnet/api/system.random?view=netframework-4.8) to provide thread-safety |
| WH1982 | Wichmann-Hill's 1982 combined [multiplicative congruential generator](https://en.wikipedia.org/wiki/Linear_congruential_generator) |
| WH2006 | Wichmann-Hill's 2006 combined [multiplicative congruential generator](https://en.wikipedia.org/wiki/Linear_congruential_generator)
| Xorshift | Multiply-with-carry XOR-shift generator |