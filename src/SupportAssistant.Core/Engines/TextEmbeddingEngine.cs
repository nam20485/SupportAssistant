using System;
using System.Collections.Generic;
using System.Linq;
using InferenceEngine.Core;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.Tokenizers;

namespace SupportAssistant.Core.Engines
{
    /// <summary>
    /// One-shot text embedding engine backed by an all-MiniLM-L6-v2 (or compatible BERT) ONNX model.
    /// Tokenizes input with a <see cref="Tokenizer"/> (BERT WordPiece vocab), runs a single forward
    /// pass through <see cref="BaseInferenceEngine{TInput,TOutput}"/>, then mean-pools the per-token
    /// hidden states (masked) and L2-normalizes the result. Dimension is discovered from the model's
    /// output metadata, replacing the legacy hardcoded 384.
    /// </summary>
    public class TextEmbeddingEngine : BaseInferenceEngine<string, float[]>
    {
        private readonly Tokenizer? _tokenizer;

        /// <summary>Creates the engine. <paramref name="tokenizer"/> may be null when the vocab file
        /// is absent; in that case prediction throws and callers fall back.</summary>
        public TextEmbeddingEngine(InferenceEngineOptions options, Tokenizer? tokenizer, ILogger? logger = null)
            : base(options, logger)
        {
            _tokenizer = tokenizer;
        }

        /// <summary>True when a tokenizer was supplied and tokenization is possible.</summary>
        public bool IsTokenizerAvailable => _tokenizer != null;

        /// <inheritdoc />
        protected override string ModelName => "all-MiniLM-L6-v2.onnx";

        /// <summary>
        /// No automatic network download (local/bundled models only), keeping first-run and CI
        /// deterministic. First-run model download UX is deferred (see plan §9).
        /// </summary>
        protected override string[]? ModelDownloadUrls => null;

        /// <inheritdoc />
        protected override (List<NamedOnnxValue> InputTensors, object? Context) PreProcessWithContext(string input)
        {
            if (_tokenizer == null)
            {
                throw new InvalidOperationException(
                    "Embedding tokenizer is not available; cannot tokenize input.");
            }

            var ids = _tokenizer.EncodeToIds(input ?? string.Empty);
            if (ids.Count == 0)
            {
                // Degenerate input — feed the model an unk/pad token so it still produces a vector.
                ids = new List<int> { 0 };
            }

            var tensors = BuildInputTensors(ids, InputMetadata);
            return (tensors, ids.Count);
        }

        /// <inheritdoc />
        protected override float[] PostProcessWithContext(
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> output, object? context)
        {
            var seqLen = (context as int?) ?? 1;
            var embeddingTensor = SelectEmbeddingOutput(output);
            var tensor = embeddingTensor.AsTensor<float>();
            var dims = tensor.Dimensions;

            // 3D [batch, seq, dim]: masked mean-pool over the sequence axis.
            if (dims.Length >= 3)
            {
                var seqDim = (int)dims[1];
                var dim = (int)dims[dims.Length - 1];
                var validSeq = Math.Max(1, Math.Min(seqLen, seqDim));
                return MeanPoolAndNormalize(tensor, seqDim, dim, validSeq);
            }

            // 2D [batch, dim] (already pooled): take the row and normalize.
            if (dims.Length == 2)
            {
                var dim = (int)dims[1];
                var vector = new float[dim];
                for (var i = 0; i < dim; i++)
                {
                    vector[i] = tensor[0, i];
                }

                return L2Normalize(vector);
            }

            // 1D fallback.
            {
                var dim = (int)tensor.Length;
                var vector = new float[dim];
                for (var i = 0; i < dim; i++)
                {
                    vector[i] = tensor[i];
                }

                return L2Normalize(vector);
            }
        }

        /// <inheritdoc />
        protected override List<NamedOnnxValue> GetWarmupInput()
        {
            // Build correctly-typed dummy tensors (int64/int32 token ids) so warmup does not fail on
            // the default float-zero tensors the base builds for arbitrary inputs.
            var dummyIds = new List<int> { 0 };
            return BuildInputTensors(dummyIds, InputMetadata);
        }

        private static DisposableNamedOnnxValue SelectEmbeddingOutput(
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> output)
        {
            // Prefer a pooled embedding output name; otherwise the first output (typically last_hidden_state).
            var preferred = output.FirstOrDefault(o =>
                o.Name.Contains("sentence_embedding", StringComparison.OrdinalIgnoreCase) ||
                o.Name.Contains("pooler", StringComparison.OrdinalIgnoreCase) ||
                o.Name.Contains("pooled", StringComparison.OrdinalIgnoreCase));
            return preferred ?? output.First();
        }

        /// <summary>Assembles int input tensors for every declared input, inferring values by name:
        /// input_ids → tokens; attention_mask → ones; token_type_ids → zeros; position_ids → 0..n-1;
        /// anything else → zeros. Matches the model's int32/int64 element type from metadata.</summary>
        internal static List<NamedOnnxValue> BuildInputTensors(
            IReadOnlyList<int> ids, IReadOnlyDictionary<string, NodeMetadata>? inputMetadata)
        {
            var tensors = new List<NamedOnnxValue>();
            var seq = ids.Count;
            var names = ResolveInputNames(inputMetadata);

            if (names.InputIds != null)
            {
                tensors.Add(CreateIntTensor(names.InputIds, ids, inputMetadata?[names.InputIds]?.ElementType));
            }

            if (names.AttentionMask != null)
            {
                var ones = Enumerable.Repeat(1, seq).ToArray();
                tensors.Add(CreateIntTensor(names.AttentionMask, ones, inputMetadata?[names.AttentionMask]?.ElementType));
            }

            if (names.TokenTypeIds != null)
            {
                var zeros = new int[seq];
                tensors.Add(CreateIntTensor(names.TokenTypeIds, zeros, inputMetadata?[names.TokenTypeIds]?.ElementType));
            }

            if (names.PositionIds != null)
            {
                var positions = Enumerable.Range(0, seq).ToArray();
                tensors.Add(CreateIntTensor(names.PositionIds, positions, inputMetadata?[names.PositionIds]?.ElementType));
            }

            return tensors;
        }

        private static (string? InputIds, string? AttentionMask, string? TokenTypeIds, string? PositionIds)
            ResolveInputNames(IReadOnlyDictionary<string, NodeMetadata>? inputMetadata)
        {
            if (inputMetadata == null || inputMetadata.Count == 0)
            {
                return ("input_ids", "attention_mask", null, null);
            }

            string? Pick(params string[] keywords)
            {
                foreach (var n in inputMetadata.Keys)
                {
                    foreach (var k in keywords)
                    {
                        if (n.Equals(k, StringComparison.OrdinalIgnoreCase))
                        {
                            return n;
                        }
                    }
                }

                foreach (var n in inputMetadata.Keys)
                {
                    foreach (var k in keywords)
                    {
                        if (n.Contains(k, StringComparison.OrdinalIgnoreCase))
                        {
                            return n;
                        }
                    }
                }

                return null;
            }

            return (Pick("input_ids", "ids"), Pick("attention_mask"), Pick("token_type_ids"), Pick("position_ids"));
        }

        private static NamedOnnxValue CreateIntTensor(string name, IReadOnlyList<int> values, Type? elementType)
        {
            // ONNX token-id tensors are int64 by convention; some exports use int32.
            if (elementType == typeof(int))
            {
                var denseInt = new DenseTensor<int>(new[] { 1, values.Count });
                for (var i = 0; i < values.Count; i++)
                {
                    denseInt[0, i] = values[i];
                }

                return NamedOnnxValue.CreateFromTensor(name, denseInt);
            }

            var dense = new DenseTensor<long>(new[] { 1, values.Count });
            for (var i = 0; i < values.Count; i++)
            {
                dense[0, i] = values[i];
            }

            return NamedOnnxValue.CreateFromTensor(name, dense);
        }

        /// <summary>Masked mean-pool over the sequence axis (axis 1) of a [1, seq, dim] tensor,
        /// followed by L2 normalization. Exposed for unit testing the math.</summary>
        internal static float[] MeanPoolAndNormalize(Tensor<float> tensor, int seqDim, int dim, int validSeq)
        {
            var pooled = new float[dim];
            for (var s = 0; s < validSeq; s++)
            {
                for (var d = 0; d < dim; d++)
                {
                    pooled[d] += tensor[0, s, d];
                }
            }

            var denom = (float)validSeq;
            if (denom > 0)
            {
                for (var d = 0; d < dim; d++)
                {
                    pooled[d] /= denom;
                }
            }

            return L2Normalize(pooled);
        }

        /// <summary>L2-normalizes a vector in place-safe fashion (returns a new vector).</summary>
        internal static float[] L2Normalize(float[] vector)
        {
            var sum = 0f;
            for (var i = 0; i < vector.Length; i++)
            {
                sum += vector[i] * vector[i];
            }

            var mag = MathF.Sqrt(sum);
            if (mag == 0f)
            {
                return (float[])vector.Clone();
            }

            var result = new float[vector.Length];
            for (var i = 0; i < vector.Length; i++)
            {
                result[i] = vector[i] / mag;
            }

            return result;
        }
    }
}
