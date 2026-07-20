using Holo.Models;

namespace Holo.DSP;

/// <summary>
/// Full port of HoloCore.TapClassifier (Swift → C#).
/// Trains a regularized linear zone model with nearest-example checks,
/// novelty detection, and negative-example rejection.
/// </summary>
public static class TapClassifierDefaults
{
    public const double MinimumConfidence = 0.36;
    public const double MinimumRelativeSeparation = 0.035;
    public const double MinimumLinearScoreMargin = 0.075;
}

public static class TapClassifier
{
    /// <summary>
    /// Train a classifier from labeled calibration taps.
    /// </summary>
    public static TrainedTapClassifier Train(
        List<LabeledTap> positiveExamples,
        List<LabeledTap> negativeExamples,
        double minimumConfidence = TapClassifierDefaults.MinimumConfidence)
    {
        var positives = positiveExamples.Where(p => p.Zone != null).ToList();
        var grouped = positives.GroupBy(p => p.Zone!.Value).ToDictionary(g => g.Key, g => g.ToList());
        if (grouped.Count < 2)
            throw new InvalidOperationException("Examples from at least two zones are required.");
        if (grouped.Values.Any(g => g.Count < 2))
            throw new InvalidOperationException("At least two examples per represented zone are required.");

        var first = positives[0];
        var names = first.Feature!.Names;
        var strategy = first.Feature.Strategy;
        var allExamples = positives.Concat(negativeExamples).ToList();
        if (names.Count == 0)
            throw new InvalidOperationException("Feature names must not be empty.");
        if (allExamples.Any(e =>
            e.Feature!.Names.Count != names.Count ||
            !e.Feature.Names.SequenceEqual(names) ||
            e.Feature.Values.Count != names.Count ||
            e.Feature.Strategy != strategy))
            throw new InvalidOperationException("Inconsistent features across examples.");

        int dimensions = names.Count;
        var center = new double[dimensions];
        var scales = new double[dimensions];

        for (int d = 0; d < dimensions; d++)
        {
            var values = positives.Select(p => p.Feature!.Values[d]).ToArray();
            center[d] = Median(values);
            var deviations = values.Select(v => Math.Abs(v - center[d])).ToArray();
            double robustScale = Median(deviations) * 1.4826;
            double mean = values.Average();
            double stdDev = Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / Math.Max(values.Length - 1, 1));
            scales[d] = Math.Max(Math.Max(robustScale, stdDev * 0.35), 1e-6);
        }

        var normalized = positives.Select(p => Normalize(p.Feature!.Values.ToArray(), center, scales)).ToArray();
        var weights = new double[dimensions];

        for (int d = 0; d < dimensions; d++)
        {
            double overall = normalized.Average(v => v[d]);
            double between = 0, within = 0;
            foreach (var zone in grouped.Keys)
            {
                var indices = positives.Select((p, i) => (p, i)).Where(x => x.p.Zone == zone).Select(x => x.i).ToList();
                var zoneValues = indices.Select(i => normalized[i][d]).ToArray();
                double zoneMean = zoneValues.Average();
                between += zoneValues.Length * Math.Pow(zoneMean - overall, 2);
                within += zoneValues.Sum(v => Math.Pow(v - zoneMean, 2));
            }
            double ratio = between / Math.Max(within, 1e-6);
            weights[d] = Math.Clamp(Math.Sqrt(ratio + 0.02), 0.12, 4.0);
        }
        double weightMean = weights.Average();
        weights = weights.Select(w => w / Math.Max(weightMean, 1e-9)).ToArray();

        // Compute outlier thresholds
        var sameZoneDistances = new List<double>();
        var sameZoneNoveltyDistances = new List<double>();
        for (int i = 0; i < positives.Count; i++)
        {
            var candidates = positives
                .Select((p, idx) => (p, idx))
                .Where(x => x.idx != i && x.p.Zone == positives[i].Zone)
                .ToList();

            if (candidates.Count > 0)
            {
                double nearest = candidates.Min(c =>
                    Distance(normalized[i], normalized[c.idx], weights));
                sameZoneDistances.Add(nearest);

                double nearestNovelty = candidates.Min(c =>
                    UnweightedDistance(normalized[i], normalized[c.idx]));
                sameZoneNoveltyDistances.Add(nearestNovelty);
            }
        }

        double threshold = Math.Max(Quantile(sameZoneDistances, 0.95) * 2.6, 0.85);
        double noveltyThreshold = Math.Max(Quantile(sameZoneNoveltyDistances, 0.95) * 2.6, 0.95);
        var linearModel = TrainLinearZoneModel(normalized, positives);

        return new TrainedTapClassifier
        {
            Strategy = strategy,
            FeatureNames = names,
            Center = center.ToList(),
            Scales = scales.ToList(),
            FeatureWeights = weights.ToList(),
            PositiveExamples = positives,
            NegativeExamples = negativeExamples,
            OutlierThreshold = threshold,
            PositiveNoveltyThreshold = noveltyThreshold,
            LinearZoneModel = linearModel
        };
    }

    /// <summary>
    /// Predict the zone for a feature vector using the trained classifier.
    /// </summary>
    public static ClassificationDecision Predict(TapFeatureVector feature, TrainedTapClassifier classifier)
    {
        if (feature.Strategy != classifier.Strategy ||
            feature.Names.Count != classifier.FeatureNames.Count ||
            !feature.Names.SequenceEqual(classifier.FeatureNames) ||
            feature.Values.Count != classifier.Center.Count)
        {
            return Rejected(feature, RejectionReason.SchemaMismatch);
        }

        if (feature.Quality!.ClippingFraction > 0.20)
            return Rejected(feature, RejectionReason.ClippedSignal);
        if (feature.Quality.PeakAmplitude < 0.003)
            return Rejected(feature, RejectionReason.WeakSignal);
        if (feature.Quality.SignalToNoiseDB < 6.0)
            return Rejected(feature, RejectionReason.LowSignalToNoise);

        double[] normalizedInput = Normalize(feature.Values.ToArray(),
            classifier.Center.ToArray(), classifier.Scales.ToArray());

        var distances = new double[4];
        for (int i = 0; i < 4; i++) distances[i] = double.PositiveInfinity;

        foreach (var zone in Enum.GetValues<DeskZone>())
        {
            var candidates = classifier.PositiveExamples
                .Where(p => p.Zone == zone)
                .Select(p => Distance(normalizedInput,
                    Normalize(p.Feature!.Values.ToArray(), classifier.Center.ToArray(), classifier.Scales.ToArray()),
                    classifier.FeatureWeights.ToArray()))
                .OrderBy(d => d)
                .ToList();

            if (candidates.Count > 0)
            {
                var nearest = candidates.Take(3).ToList();
                double[] rankWeights = [0.58, 0.28, 0.14];
                double denom = rankWeights.Take(nearest.Count).Sum();
                double weighted = 0;
                for (int j = 0; j < nearest.Count; j++)
                    weighted += nearest[j] * rankWeights[j];
                distances[(int)zone] = weighted / denom;
            }
        }

        var ranked = distances.Select((d, i) => (dist: d, zone: i))
            .Where(x => double.IsFinite(x.dist))
            .OrderBy(x => x.dist)
            .ToList();

        if (ranked.Count < 2)
            return Rejected(feature, RejectionReason.SchemaMismatch, distances.ToList());

        double best = ranked[0].dist;
        double second = ranked[1].dist;

        if (best > classifier.OutlierThreshold)
            return Rejected(feature, RejectionReason.OutOfDistribution, distances.ToList());

        var positiveNoveltyDistances = classifier.PositiveExamples
            .Select(p => UnweightedDistance(normalizedInput,
                Normalize(p.Feature!.Values.ToArray(), classifier.Center.ToArray(), classifier.Scales.ToArray())))
            .ToList();
        double nearestPositiveNovelty = positiveNoveltyDistances.Min();

        if (classifier.PositiveNoveltyThreshold.HasValue &&
            nearestPositiveNovelty > classifier.PositiveNoveltyThreshold.Value)
            return Rejected(feature, RejectionReason.OutOfDistribution, distances.ToList());

        if (classifier.NegativeExamples.Count > 0)
        {
            double nearestNegative = classifier.NegativeExamples
                .Min(p => UnweightedDistance(normalizedInput,
                    Normalize(p.Feature!.Values.ToArray(), classifier.Center.ToArray(), classifier.Scales.ToArray())));
            if (nearestNegative <= nearestPositiveNovelty * 1.10)
                return Rejected(feature, RejectionReason.ResemblesNegativeExample, distances.ToList());
        }

        // Try linear model decision first
        var linearResult = LinearDecision(normalizedInput, nearestPositiveNovelty,
            distances.ToList(), feature, classifier);
        if (linearResult != null)
            return linearResult;

        // Fall back to nearest-example decision
        var bestZone = (DeskZone)ranked[0].zone;
        double separation = (second - best) / Math.Max(second, 1e-9);
        double separationScore = Math.Clamp(separation / 0.38, 0, 1);
        double fitScore = Math.Clamp(1 - best / classifier.OutlierThreshold, 0, 1);
        double confidence = 0.72 * separationScore + 0.28 * fitScore;

        if (separation < TapClassifierDefaults.MinimumRelativeSeparation ||
            confidence < classifier.MinimumConfidence)
            return Rejected(feature, RejectionReason.AmbiguousZone, distances.ToList(), confidence);

        return new ClassificationDecision
        {
            Zone = bestZone,
            Confidence = confidence,
            SignalStrength = feature.Quality.Score,
            ZoneDistances = distances.ToList(),
            RejectionReason = null
        };
    }

    private static ClassificationDecision? LinearDecision(
        double[] normalizedInput, double nearestPositiveNovelty,
        List<double> distances, TapFeatureVector feature, TrainedTapClassifier classifier)
    {
        if (classifier.LinearZoneModel == null ||
            classifier.LinearZoneModel.Coefficients.Count != 4 ||
            classifier.LinearZoneModel.Coefficients.Any(c => c.Count != normalizedInput.Length + 1))
            return null;

        var representedZones = classifier.PositiveExamples
            .Select(p => p.Zone).Where(z => z.HasValue).Select(z => z!.Value).ToHashSet();

        var input = new double[] { 1.0 }.Concat(normalizedInput).ToArray();
        var scores = new List<(DeskZone zone, double score)>();

        foreach (var zone in Enum.GetValues<DeskZone>())
        {
            if (!representedZones.Contains(zone)) continue;
            double score = 0;
            for (int i = 0; i < classifier.LinearZoneModel.Coefficients[(int)zone].Count && i < input.Length; i++)
                score += classifier.LinearZoneModel.Coefficients[(int)zone][i] * input[i];
            if (double.IsFinite(score))
                scores.Add((zone, score));
        }

        scores = scores.OrderByDescending(s => s.score).ToList();
        if (scores.Count < 2) return null;

        double margin = scores[0].score - scores[1].score;
        double marginScore = Math.Clamp(margin / 0.30, 0, 1);
        double fitThreshold = classifier.PositiveNoveltyThreshold ?? Math.Max(classifier.OutlierThreshold, 1e-9);
        double fitScore = Math.Clamp(1 - nearestPositiveNovelty / fitThreshold, 0, 1);
        double confidence = 0.76 * marginScore + 0.24 * fitScore;

        if (margin < TapClassifierDefaults.MinimumLinearScoreMargin ||
            confidence < classifier.MinimumConfidence)
            return null;

        return new ClassificationDecision
        {
            Zone = scores[0].zone,
            Confidence = confidence,
            SignalStrength = feature.Quality!.Score,
            ZoneDistances = distances,
            RejectionReason = null
        };
    }

    private static ClassificationDecision Rejected(
        TapFeatureVector feature, RejectionReason reason,
        List<double>? distances = null, double confidence = 0)
    {
        return new ClassificationDecision
        {
            Zone = null,
            Confidence = confidence,
            SignalStrength = feature.Quality?.Score ?? 0,
            ZoneDistances = distances ?? [],
            RejectionReason = reason
        };
    }

    private static double[] Normalize(double[] values, double[] center, double[] scales)
    {
        var result = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
            result[i] = (values[i] - center[i]) / Math.Max(scales[i], 1e-9);
        return result;
    }

    private static double Distance(double[] lhs, double[] rhs, double[] weights)
    {
        double weighted = 0;
        for (int i = 0; i < lhs.Length && i < rhs.Length && i < weights.Length; i++)
        {
            double delta = lhs[i] - rhs[i];
            weighted += weights[i] * delta * delta;
        }
        return Math.Sqrt(weighted / Math.Max(weights.Sum(), 1e-9));
    }

    private static double UnweightedDistance(double[] lhs, double[] rhs)
    {
        double sq = 0;
        int count = Math.Min(lhs.Length, rhs.Length);
        for (int i = 0; i < count; i++)
            sq += Math.Pow(lhs[i] - rhs[i], 2);
        return Math.Sqrt(sq / Math.Max(count, 1));
    }

    private static RegularizedLinearZoneModel? TrainLinearZoneModel(
        double[][] normalized, List<LabeledTap> positives)
    {
        if (normalized.Length == 0 || positives.Count == 0) return null;
        int dimension = normalized[0].Length;
        if (dimension == 0 || normalized.Length != positives.Count) return null;

        int paramCount = dimension + 1;
        int zoneCount = 4;

        var normal = new double[paramCount][];
        var rhs = new double[paramCount][];
        for (int i = 0; i < paramCount; i++)
        {
            normal[i] = new double[paramCount];
            rhs[i] = new double[zoneCount];
        }

        for (int idx = 0; idx < normalized.Length; idx++)
        {
            if (positives[idx].Zone == null) continue;
            int zoneIdx = (int)positives[idx].Zone!.Value;
            var row = new double[] { 1.0 }.Concat(normalized[idx]).ToArray();

            for (int lhs = 0; lhs < paramCount; lhs++)
            {
                rhs[lhs][zoneIdx] += row[lhs];
                for (int r = 0; r < paramCount; r++)
                    normal[lhs][r] += row[lhs] * row[r];
            }
        }

        // Ridge regularization (no penalty on intercept)
        double regularization = Math.Max(3.0, positives.Count * 0.25);
        for (int i = 1; i < paramCount; i++)
            normal[i][i] += regularization;

        var solution = SolvePositiveDefinite(normal, rhs);
        if (solution == null) return null;

        var coefficients = new List<List<double>>();
        for (int z = 0; z < zoneCount; z++)
        {
            var col = new List<double>();
            for (int r = 0; r < paramCount; r++)
                col.Add(solution[r][z]);
            coefficients.Add(col);
        }

        if (coefficients.SelectMany(c => c).All(double.IsFinite) == false)
            return null;

        return new RegularizedLinearZoneModel { Coefficients = coefficients };
    }

    /// <summary>
    /// Cholesky decomposition solver: A·X = B
    /// Ridge regularization makes A positive definite.
    /// </summary>
    private static double[][]? SolvePositiveDefinite(double[][] matrix, double[][] rightHandSide)
    {
        int n = matrix.Length;
        if (n == 0 || matrix.Any(row => row.Length != n)) return null;
        int cols = rightHandSide[0].Length;
        if (rightHandSide.Length != n || rightHandSide.Any(row => row.Length != cols)) return null;

        // Cholesky decomposition: A = L·L^T
        var lower = new double[n][];
        for (int i = 0; i < n; i++) lower[i] = new double[n];

        for (int row = 0; row < n; row++)
        {
            for (int col = 0; col <= row; col++)
            {
                double value = matrix[row][col];
                for (int k = 0; k < col; k++)
                    value -= lower[row][k] * lower[col][k];

                if (row == col)
                {
                    if (!double.IsFinite(value) || value <= 1e-10) return null;
                    lower[row][col] = Math.Sqrt(value);
                }
                else
                {
                    lower[row][col] = value / lower[col][col];
                }
            }
        }

        // Forward substitution: L·Y = B
        var y = new double[n][];
        for (int i = 0; i < n; i++)
        {
            y[i] = new double[cols];
            for (int j = 0; j < cols; j++)
            {
                double value = rightHandSide[i][j];
                for (int k = 0; k < i; k++)
                    value -= lower[i][k] * y[k][j];
                y[i][j] = value / lower[i][i];
            }
        }

        // Back substitution: L^T·X = Y
        var x = new double[n][];
        for (int i = 0; i < n; i++) x[i] = new double[cols];

        for (int row = n - 1; row >= 0; row--)
        {
            for (int j = 0; j < cols; j++)
            {
                double value = y[row][j];
                for (int k = row + 1; k < n; k++)
                    value -= lower[k][row] * x[k][j];
                x[row][j] = value / lower[row][row];
            }
        }

        return x;
    }

    private static double Median(double[] values)
    {
        if (values.Length == 0) return 0;
        var sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length % 2 == 0)
            return (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
        return sorted[sorted.Length / 2];
    }

    private static double Quantile(List<double> values, double probability)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(v => v).ToArray();
        double position = Math.Clamp(probability, 0, 1) * (sorted.Length - 1);
        int lower = (int)Math.Floor(position);
        int upper = (int)Math.Ceiling(position);
        if (lower == upper) return sorted[lower];
        double fraction = position - lower;
        return sorted[lower] * (1 - fraction) + sorted[upper] * fraction;
    }
}
