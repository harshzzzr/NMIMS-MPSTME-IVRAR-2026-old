using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace C067.Coordination
{
    [System.Serializable]
    public class DescriptiveStatistics
    {
        public string variableName = "";
        public int n = 0;
        public double mean = 0.0;
        public double median = 0.0;
        public double variance = 0.0;
        public double standardDeviation = 0.0;
        public double standardError = 0.0;
        public double min = 0.0;
        public double max = 0.0;
        public double p25 = 0.0;
        public double p75 = 0.0;
        public double iqr = 0.0;
        public double skewness = 0.0;
        public double kurtosis = 0.0;

        public override string ToString()
        {
            return $"{variableName}: n={n}, Mean={mean:F3} (SD={standardDeviation:F3}, SEM={standardError:F3}), Median={median:F3} (IQR={iqr:F3}), Range=[{min:F3}, {max:F3}]";
        }
    }

    [System.Serializable]
    public class PairedTTestResult
    {
        public string conditionA_Name = "Condition_A";
        public string conditionB_Name = "Condition_B";
        public int pairedObservations = 0;
        public double meanDifference = 0.0;
        public double standardDeviationDifference = 0.0;
        public double standardErrorDifference = 0.0;
        public double tStatistic = 0.0;
        public double degreesOfFreedom = 0.0;
        public double pValueTwoTailed = 1.0;
        public double cohensDz = 0.0;
        public double confidenceInterval95_Lower = 0.0;
        public double confidenceInterval95_Upper = 0.0;
        public bool isSignificant_Alpha05 => pValueTwoTailed < 0.05;
        public bool isSignificant_Alpha01 => pValueTwoTailed < 0.01;

        public override string ToString()
        {
            return $"Paired t-test ({conditionA_Name} vs {conditionB_Name}): t({degreesOfFreedom:F0}) = {tStatistic:F3}, p = {pValueTwoTailed:G4}, Mean Diff = {meanDifference:F3} (95% CI [{confidenceInterval95_Lower:F3}, {confidenceInterval95_Upper:F3}]), Cohen's dz = {cohensDz:F3}";
        }
    }

    [System.Serializable]
    public class TwoSampleTTestResult
    {
        public string groupA_Name = "Group_A";
        public string groupB_Name = "Group_B";
        public int nA = 0;
        public int nB = 0;
        public double meanA = 0.0;
        public double meanB = 0.0;
        public double meanDifference = 0.0;
        public double tStatistic = 0.0;
        public double degreesOfFreedom = 0.0;
        public double pValueTwoTailed = 1.0;
        public double cohensD = 0.0;
        public bool isSignificant_Alpha05 => pValueTwoTailed < 0.05;

        public override string ToString()
        {
            return $"Welch's Two-Sample t-test ({groupA_Name} vs {groupB_Name}): t({degreesOfFreedom:F1}) = {tStatistic:F3}, p = {pValueTwoTailed:G4}, Diff = {meanDifference:F3}, Cohen's d = {cohensD:F3}";
        }
    }

    [System.Serializable]
    public class WilcoxonSignedRankResult
    {
        public string conditionA_Name = "Condition_A";
        public string conditionB_Name = "Condition_B";
        public int nonZeroPairs = 0;
        public double sumPositiveRanks = 0.0;
        public double sumNegativeRanks = 0.0;
        public double testStatisticW = 0.0;
        public double zScore = 0.0;
        public double pValueTwoTailed = 1.0;
        public bool isSignificant_Alpha05 => pValueTwoTailed < 0.05;

        public override string ToString()
        {
            return $"Wilcoxon Signed-Rank Test ({conditionA_Name} vs {conditionB_Name}): W = {testStatisticW:F0}, z = {zScore:F3}, p = {pValueTwoTailed:G4} (n = {nonZeroPairs} non-zero pairs)";
        }
    }

    /// <summary>
    /// Rigorous, reproducible statistical analysis module for human factors evaluation data:
    /// Paired t-tests, Welch's independent two-sample t-tests, Wilcoxon signed-rank tests,
    /// descriptive statistics, and exact p-value calculations via regularized incomplete beta functions.
    /// </summary>
    public static class HumanFactorsStatistics
    {
        /// <summary>
        /// Computes complete descriptive statistics for a numeric sample.
        /// </summary>
        public static DescriptiveStatistics CalculateDescriptive(IEnumerable<double> values, string variableName = "Variable")
        {
            var res = new DescriptiveStatistics { variableName = variableName };
            if (values == null) return res;

            var list = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToList();
            res.n = list.Count;
            if (res.n == 0) return res;

            list.Sort();
            res.min = list[0];
            res.max = list[list.Count - 1];

            res.mean = list.Average();
            res.median = GetQuantile(list, 0.50);
            res.p25 = GetQuantile(list, 0.25);
            res.p75 = GetQuantile(list, 0.75);
            res.iqr = res.p75 - res.p25;

            if (res.n > 1)
            {
                double sumSq = list.Sum(v => (v - res.mean) * (v - res.mean));
                res.variance = sumSq / (res.n - 1);
                res.standardDeviation = Math.Sqrt(res.variance);
                res.standardError = res.standardDeviation / Math.Sqrt(res.n);

                if (res.n >= 3 && res.standardDeviation > 1e-9)
                {
                    double m3 = list.Sum(v => Math.Pow(v - res.mean, 3)) / res.n;
                    double s3 = Math.Pow(res.standardDeviation, 3);
                    res.skewness = (res.n * Math.Sqrt(res.n - 1) / (res.n - 2)) * (m3 / s3);
                }
            }

            return res;
        }

        /// <summary>
        /// Executes a paired Student's t-test for within-subject matched pairs:
        /// d_i = X_i,A - X_i,B
        /// t = mean(d) / (SD(d) / sqrt(n))
        /// </summary>
        public static PairedTTestResult ComputePairedTTest(
            IList<double> conditionA, IList<double> conditionB,
            string labelA = "Condition_A", string labelB = "Condition_B")
        {
            var result = new PairedTTestResult
            {
                conditionA_Name = labelA,
                conditionB_Name = labelB
            };

            if (conditionA == null || conditionB == null) return result;

            int n = Math.Min(conditionA.Count, conditionB.Count);
            var differences = new List<double>();

            for (int i = 0; i < n; i++)
            {
                double a = conditionA[i];
                double b = conditionB[i];

                if (!double.IsNaN(a) && !double.IsInfinity(a) && !double.IsNaN(b) && !double.IsInfinity(b) && a >= 0.0 && b >= 0.0)
                {
                    differences.Add(a - b);
                }
            }

            result.pairedObservations = differences.Count;
            if (result.pairedObservations < 2) return result;

            int validN = result.pairedObservations;
            result.degreesOfFreedom = validN - 1;

            result.meanDifference = differences.Average();

            double sumSqDiff = differences.Sum(d => (d - result.meanDifference) * (d - result.meanDifference));
            double varDiff = sumSqDiff / (validN - 1);
            result.standardDeviationDifference = Math.Sqrt(varDiff);
            result.standardErrorDifference = result.standardDeviationDifference / Math.Sqrt(validN);

            if (result.standardErrorDifference > 1e-12)
            {
                result.tStatistic = result.meanDifference / result.standardErrorDifference;
                result.pValueTwoTailed = ComputeStudentT_PValue(result.tStatistic, result.degreesOfFreedom);
                result.cohensDz = result.meanDifference / result.standardDeviationDifference;

                double criticalT = ApproximateTInverse0975(result.degreesOfFreedom);
                result.confidenceInterval95_Lower = result.meanDifference - criticalT * result.standardErrorDifference;
                result.confidenceInterval95_Upper = result.meanDifference + criticalT * result.standardErrorDifference;
            }
            else
            {
                result.tStatistic = 0.0;
                result.pValueTwoTailed = (Math.Abs(result.meanDifference) < 1e-9) ? 1.0 : 0.0;
                result.confidenceInterval95_Lower = result.meanDifference;
                result.confidenceInterval95_Upper = result.meanDifference;
            }

            return result;
        }

        /// <summary>
        /// Executes Welch's two-sample independent t-test (handles unequal variances and sample sizes).
        /// </summary>
        public static TwoSampleTTestResult ComputeWelchTwoSampleTTest(
            IList<double> groupA, IList<double> groupB,
            string labelA = "Group_A", string labelB = "Group_B")
        {
            return ComputeTwoSampleTTest(groupA, groupB, labelA, labelB);
        }

        public static TwoSampleTTestResult ComputeTwoSampleTTest(
            IList<double> groupA, IList<double> groupB,
            string labelA = "Group_A", string labelB = "Group_B")
        {
            var result = new TwoSampleTTestResult
            {
                groupA_Name = labelA,
                groupB_Name = labelB
            };

            var validA = groupA?.Where(v => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0.0).ToList() ?? new List<double>();
            var validB = groupB?.Where(v => !double.IsNaN(v) && !double.IsInfinity(v) && v >= 0.0).ToList() ?? new List<double>();

            result.nA = validA.Count;
            result.nB = validB.Count;
            if (result.nA < 2 || result.nB < 2) return result;

            result.meanA = validA.Average();
            result.meanB = validB.Average();
            result.meanDifference = result.meanA - result.meanB;

            double varA = validA.Sum(v => (v - result.meanA) * (v - result.meanA)) / (result.nA - 1);
            double varB = validB.Sum(v => (v - result.meanB) * (v - result.meanB)) / (result.nB - 1);

            double semA_sq = varA / result.nA;
            double semB_sq = varB / result.nB;
            double denom = Math.Sqrt(semA_sq + semB_sq);

            if (denom > 1e-12)
            {
                result.tStatistic = result.meanDifference / denom;

                // Welch-Satterthwaite degrees of freedom
                double numDf = Math.Pow(semA_sq + semB_sq, 2);
                double denomDf = (Math.Pow(semA_sq, 2) / (result.nA - 1)) + (Math.Pow(semB_sq, 2) / (result.nB - 1));
                result.degreesOfFreedom = (denomDf > 1e-12) ? numDf / denomDf : 1.0;

                result.pValueTwoTailed = ComputeStudentT_PValue(result.tStatistic, result.degreesOfFreedom);

                double pooledSd = Math.Sqrt(((result.nA - 1) * varA + (result.nB - 1) * varB) / (result.nA + result.nB - 2));
                result.cohensD = (pooledSd > 1e-9) ? result.meanDifference / pooledSd : 0.0;
            }

            return result;
        }

        /// <summary>
        /// Executes the Wilcoxon signed-rank test for paired non-normally distributed data
        /// (e.g. pre-evacuation delay, NASA-TLX ordinal ratings, SSQ scores).
        /// </summary>
        public static WilcoxonSignedRankResult ComputeWilcoxonSignedRankTest(
            IList<double> conditionA, IList<double> conditionB,
            string labelA = "Condition_A", string labelB = "Condition_B")
        {
            var res = new WilcoxonSignedRankResult
            {
                conditionA_Name = labelA,
                conditionB_Name = labelB
            };

            if (conditionA == null || conditionB == null) return res;

            int n = Math.Min(conditionA.Count, conditionB.Count);
            var diffs = new List<(double absDiff, int sign)>();

            for (int i = 0; i < n; i++)
            {
                double a = conditionA[i];
                double b = conditionB[i];
                if (!double.IsNaN(a) && !double.IsInfinity(a) && !double.IsNaN(b) && !double.IsInfinity(b))
                {
                    double d = a - b;
                    if (Math.Abs(d) > 1e-9)
                    {
                        diffs.Add((Math.Abs(d), d > 0 ? 1 : -1));
                    }
                }
            }

            res.nonZeroPairs = diffs.Count;
            if (res.nonZeroPairs < 3) return res;

            // Sort by absolute difference
            diffs.Sort((x, y) => x.absDiff.CompareTo(y.absDiff));

            // Assign ranks with average tie breaking
            double[] ranks = new double[diffs.Count];
            int iIdx = 0;
            while (iIdx < diffs.Count)
            {
                int jIdx = iIdx;
                while (jIdx < diffs.Count && Math.Abs(diffs[jIdx].absDiff - diffs[iIdx].absDiff) < 1e-9)
                {
                    jIdx++;
                }

                double avgRank = (iIdx + 1 + jIdx) / 2.0;
                for (int k = iIdx; k < jIdx; k++)
                {
                    ranks[k] = avgRank;
                }
                iIdx = jIdx;
            }

            double sumPos = 0.0;
            double sumNeg = 0.0;
            for (int k = 0; k < diffs.Count; k++)
            {
                if (diffs[k].sign > 0) sumPos += ranks[k];
                else sumNeg += ranks[k];
            }

            res.sumPositiveRanks = sumPos;
            res.sumNegativeRanks = sumNeg;
            res.testStatisticW = Math.Min(sumPos, sumNeg);

            // Normal approximation for p-value (with continuity correction)
            int nPairs = res.nonZeroPairs;
            double meanW = nPairs * (nPairs + 1) / 4.0;
            double varW = nPairs * (nPairs + 1) * (2 * nPairs + 1) / 24.0;
            double sdW = Math.Sqrt(varW);

            if (sdW > 1e-9)
            {
                double continuity = (res.testStatisticW < meanW) ? 0.5 : -0.5;
                res.zScore = (res.testStatisticW - meanW + continuity) / sdW;
                res.pValueTwoTailed = 2.0 * NormalCDF(-Math.Abs(res.zScore));
                res.pValueTwoTailed = Math.Clamp(res.pValueTwoTailed, 0.0, 1.0);
            }

            return res;
        }

        // =========================================================================
        // MATHEMATICAL KERNEL: STUDENT'S T AND REGULARIZED INCOMPLETE BETA
        // =========================================================================

        /// <summary>
        /// Computes exact two-tailed p-value for Student's t distribution with df degrees of freedom.
        /// p = I_{df / (df + t^2)}(df / 2, 1 / 2)
        /// </summary>
        public static double ComputeStudentT_PValue(double t, double df)
        {
            if (df <= 0.0 || double.IsNaN(t)) return 1.0;
            if (double.IsInfinity(t)) return 0.0;

            double x = df / (df + t * t);
            double p = RegularizedIncompleteBeta(x, df * 0.5, 0.5);
            return Math.Clamp(p, 0.0, 1.0);
        }

        /// <summary>
        /// High-precision regularized incomplete beta function I_x(a, b) via continued fractions.
        /// Abramowitz & Stegun 26.5.8.
        /// </summary>
        public static double RegularizedIncompleteBeta(double x, double a, double b)
        {
            if (x <= 0.0) return 0.0;
            if (x >= 1.0) return 1.0;

            // Symmetry relation to ensure rapid convergence
            if (x > (a + 1.0) / (a + b + 2.0))
            {
                return 1.0 - RegularizedIncompleteBeta(1.0 - x, b, a);
            }

            double lnBeta = LogGamma(a) + LogGamma(b) - LogGamma(a + b);
            double front = Math.Exp(Math.Log(x) * a + Math.Log(1.0 - x) * b - lnBeta) / a;

            // Continued fraction (Modified Lentz's method)
            double f = 1.0;
            double c = 1.0;
            double d = 0.0;

            for (int m = 1; m <= 200; m++)
            {
                int m2 = 2 * m;
                // Even step
                double dEven = -m * (b - m) * x / ((a + m2 - 1.0) * (a + m2));
                d = 1.0 + dEven * d;
                if (Math.Abs(d) < 1e-30) d = 1e-30;
                c = 1.0 + dEven / c;
                if (Math.Abs(c) < 1e-30) c = 1e-30;
                d = 1.0 / d;
                f *= c * d;

                // Odd step
                double dOdd = (a + m) * (a + b + m) * x / ((a + m2) * (a + m2 + 1.0));
                d = 1.0 + dOdd * d;
                if (Math.Abs(d) < 1e-30) d = 1e-30;
                c = 1.0 + dOdd / c;
                if (Math.Abs(c) < 1e-30) c = 1e-30;
                d = 1.0 / d;
                double delta = c * d;
                f *= delta;

                if (Math.Abs(delta - 1.0) < 1e-12) break;
            }

            return front * f;
        }

        /// <summary>
        /// Lanczos approximation for log-gamma function (precision ~1e-14).
        /// </summary>
        public static double LogGamma(double x)
        {
            double[] c = {
                57.1562356658629235, -59.5979603554754912,
                14.1360979747417471, -0.491913816097620199,
                0.339946499848118887e-4, 0.465236289270485756e-4,
                -0.983744753048795646e-4, 0.158088703224378388e-3,
                -0.210264441724104783e-3, 0.217439618115212643e-3,
                -0.164318106536763890e-3, 0.844182239838527433e-4,
                -0.261908384015814087e-4, 0.368991826595316234e-5
            };

            if (x < 0.5)
            {
                return Math.Log(Math.PI / Math.Sin(Math.PI * x)) - LogGamma(1.0 - x);
            }

            double z = x - 1.0;
            double baseVal = z + 5.2421875;
            double sum = 0.999999999999997092;
            for (int i = 0; i < c.Length; i++)
            {
                sum += c[i] / (z + i + 1.0);
            }

            return 0.5 * Math.Log(2.0 * Math.PI) + (z + 0.5) * Math.Log(baseVal) - baseVal + Math.Log(sum);
        }

        private static double NormalCDF(double z)
        {
            return 0.5 * (1.0 + Erf(z / Math.Sqrt(2.0)));
        }

        private static double Erf(double x)
        {
            // Abramowitz and Stegun 7.1.26
            double a1 = 0.254829592;
            double a2 = -0.284496736;
            double a3 = 1.421413741;
            double a4 = -1.453152027;
            double a5 = 1.061405429;
            double p = 0.3275911;

            int sign = (x < 0) ? -1 : 1;
            double absX = Math.Abs(x);
            double t = 1.0 / (1.0 + p * absX);
            double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-absX * absX);

            return sign * y;
        }

        private static double ApproximateTInverse0975(double df)
        {
            // High-quality Cornish-Fisher approximation for 95% critical value (two-tailed alpha=0.05)
            if (df <= 1.0) return 12.706;
            if (df == 2.0) return 4.303;
            if (df == 3.0) return 3.182;
            if (df == 4.0) return 2.776;
            if (df == 5.0) return 2.571;
            if (df >= 120.0) return 1.960;

            double z = 1.959964;
            double c1 = (z * z * z + z) / (4.0 * df);
            double c2 = (5.0 * Math.Pow(z, 5) + 16.0 * Math.Pow(z, 3) + 3.0 * z) / (96.0 * df * df);
            return z + c1 + c2;
        }

        private static double GetQuantile(List<double> sorted, double q)
        {
            if (sorted == null || sorted.Count == 0) return 0.0;
            if (sorted.Count == 1) return sorted[0];

            double rank = q * (sorted.Count - 1);
            int low = (int)Math.Floor(rank);
            int high = (int)Math.Ceiling(rank);
            double frac = rank - low;

            return sorted[low] + frac * (sorted[high] - sorted[low]);
        }
    }
}
