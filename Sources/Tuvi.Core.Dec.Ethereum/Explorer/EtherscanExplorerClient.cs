// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2025 Eppie (https://eppie.io)                                    //
//                                                                              //
//   Licensed under the Apache License, Version 2.0 (the "License"),            //
//   you may not use this file except in compliance with the License.           //
//   You may obtain a copy of the License at                                    //
//                                                                              //
//       http://www.apache.org/licenses/LICENSE-2.0                             //
//                                                                              //
//   Unless required by applicable law or agreed to in writing, software        //
//   distributed under the License is distributed on an "AS IS" BASIS,          //
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.   //
//   See the License for the specific language governing permissions and        //
//   limitations under the License.                                             //
//                                                                              //
// ---------------------------------------------------------------------------- //

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Util;

namespace Tuvi.Core.Dec.Ethereum.Explorer
{
    /// <summary>
    /// Etherscan-compatible explorer client. Provides limited paging to collect outgoing transactions
    /// and utilities to compute signing-hash and extract signature fields (without raw reconstruction).
    /// Failures are swallowed and represented as empty results.
    /// </summary>
    internal sealed class EtherscanExplorerClient : IEthereumExplorerClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBase;
        private readonly string _apiKey;

        private const int PageSize = 100;   // Reasonable page size.
        private const int MaxPages = 5;     // Safety cap (at most 500 tx inspected).

        // General constants
        private const int FirstPage = 1;

        // Etherscan API constants
        private const string AccountModule = "account";
        private const string ProxyModule = "proxy";
        private const string ActionTxList = "txlist";
        private const string ActionEthGetTransactionByHash = "eth_getTransactionByHash";
        private const string SortDescending = "desc";
        private const string StartBlock = "0";
        private const string EndBlockDefault = "99999999";

        // Retry / backoff constants
        private const int MaxAttempts = 3;
        private const int InitialBackoffMs = 500;
        private const int MaxBackoffMs = 4000;
        private const int HttpStatusTooManyRequests = 429;
        private const string RateLimitKeyword = "rate limit";

        // Transaction/signature related constants
        private const byte TxType01Prefix = 0x01; // EIP-2930
        private const byte TxType02Prefix = 0x02; // EIP-1559
        private const string TxTypeHex01 = "0x1";
        private const string TxTypeHex02 = "0x2";
        private const string TxTypeHexLegacy = "0x0";
        private const int TypePrefixLength = 1;

        private const byte VParityBase = 27;
        private const byte VEip155Threshold = 35;
        private const byte ZeroByte = 0x00;

        private const int Bytes32 = 32;
        private const int AddressBytes = 20;

        public EtherscanExplorerClient(HttpClient httpClient, Uri apiBase, string apiKey = "")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            if (apiBase is null) throw new ArgumentNullException(nameof(apiBase));
            _apiBase = apiBase.AbsoluteUri.TrimEnd('/');
            _apiKey = apiKey ?? string.Empty;
        }

        public Task<AddressInfo> GetAddressInfoAsync(string address, CancellationToken ct)
        {
            return GetAddressInfoPagedAsync(address, ct);
        }

        private async Task<AddressInfo> GetAddressInfoPagedAsync(string address, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return new AddressInfo(address ?? string.Empty, new List<string>());
            }

            var outgoing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int page = FirstPage; page <= MaxPages; page++)
            {
                ct.ThrowIfCancellationRequested();
                var pageTx = await FetchPageAsync(address, page, PageSize, ct).ConfigureAwait(false);
                if (pageTx.Count == 0)
                {
                    break;
                }

                foreach (var tx in pageTx)
                {
                    if (tx is null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(tx.From) && tx.From.Equals(address, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrEmpty(tx.Hash))
                        {
                            outgoing.Add(tx.Hash);
                        }
                    }
                }

                if (outgoing.Count > 0)
                {
                    break; // Stop early if we already found at least one outgoing tx.
                }
            }

            return new AddressInfo(address, outgoing.ToList());
        }

        private async Task<List<TxItem>> FetchPageAsync(string address, int page, int offset, CancellationToken ct)
        {
            var url = _apiBase + $"?module={AccountModule}&action={ActionTxList}&address={address}&startblock={StartBlock}&endblock={EndBlockDefault}&page={page}&offset={offset}&sort={SortDescending}{AppendKey()}";
            int delayMs = InitialBackoffMs;

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    using (var resp = await _httpClient.GetAsync(url, ct).ConfigureAwait(false))
                    {
                        // If not success and not a rate limit we swallow and return empty
                        if (!resp.IsSuccessStatusCode)
                        {
                            // Retry on 429 TooManyRequests
                            if ((int)resp.StatusCode == HttpStatusTooManyRequests && attempt < MaxAttempts)
                            {
                                await Task.Delay(delayMs, ct).ConfigureAwait(false);
                                delayMs = Math.Min(delayMs * 2, MaxBackoffMs);

                                continue;
                            }

                            return new List<TxItem>();
                        }

                        var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                        // Detect rate limit messages reported in plain text for tx list and retry after a short delay
                        if (ContainsRateLimit(json) && attempt < MaxAttempts)
                        {
                            await Task.Delay(delayMs, ct).ConfigureAwait(false);
                            delayMs = Math.Min(delayMs * 2, MaxBackoffMs);

                            continue;
                        }

                        TxListResponse dto;
                        try
                        {
                            dto = JsonSerializer.Deserialize<TxListResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new TxListResponse();
                        }
                        catch (JsonException)
                        {
                            // Bad JSON -> treat as empty page
                            return new List<TxItem>();
                        }

                        if (dto.Result is null)
                        {
                            return new List<TxItem>();
                        }

                        if (ContainsRateLimit(dto.Message) && attempt < MaxAttempts)
                        {
                            await Task.Delay(delayMs, ct).ConfigureAwait(false);
                            delayMs = Math.Min(delayMs * 2, MaxBackoffMs);

                            continue;
                        }

                        return dto.Result;
                    }
                }
                catch (HttpRequestException)
                {
                    // swallow and return empty unless more retries remain
                    if (attempt < MaxAttempts)
                    {
                        await Task.Delay(delayMs, ct).ConfigureAwait(false);
                        delayMs = Math.Min(delayMs * 2, MaxBackoffMs);
                        continue;
                    }
                    return new List<TxItem>();
                }
                catch (TaskCanceledException)
                {
                    // Cancellation or timeout -> return empty
                    return new List<TxItem>();
                }
            }

            return new List<TxItem>();
        }

        private static bool ContainsRateLimit(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return text.IndexOf(RateLimitKeyword, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string AppendKey()
        {
            return string.IsNullOrEmpty(_apiKey) ? string.Empty : $"&apikey={_apiKey}";
        }

        // Provides signature and message hash for standard EOA-signed transactions (legacy, 0x01, 0x02).
        internal async Task<SignatureInfo> TryGetSignatureInfoAsync(string txHash, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(txHash))
            {
                return null;
            }

            var proxyUrl = $"{_apiBase}?module={ProxyModule}&action={ActionEthGetTransactionByHash}&txhash={txHash}{AppendKey()}";
            try
            {
                using (var resp = await _httpClient.GetAsync(proxyUrl, ct).ConfigureAwait(false))
                {
                    if (!resp.IsSuccessStatusCode)
                    {
                        return null;
                    }

                    var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (ContainsRateLimit(json))
                    {
                        throw new ApiRateLimitExceededException("Etherscan API rate limit exceeded while fetching transaction by hash.");
                    }

                    // Deserialize to a typed model to avoid manual JsonElement property probing
                    RpcResponse<TransactionRpcDto> rpc;
                    try
                    {
                        rpc = JsonSerializer.Deserialize<RpcResponse<TransactionRpcDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch (JsonException)
                    {
                        return null;
                    }

                    if (rpc is null || rpc.Result is null)
                    {
                        return null;
                    }

                    // Determine type flags from typed model
                    var t = rpc.Result.Type;
                    bool is1559 = string.Equals(t, TxTypeHex02, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(rpc.Result.MaxFeePerGas) && !string.IsNullOrEmpty(rpc.Result.MaxPriorityFeePerGas));
                    bool is2930 = string.Equals(t, TxTypeHex01, StringComparison.OrdinalIgnoreCase);
                    bool isLegacy = string.IsNullOrEmpty(t) || string.Equals(t, TxTypeHexLegacy, StringComparison.OrdinalIgnoreCase);

                    byte[] msg = null;
                    try
                    {
                        if (is1559) msg = ComputeType2SigningHash(rpc.Result);
                        else if (is2930) msg = ComputeType1SigningHash(rpc.Result);
                        else if (isLegacy) msg = ComputeLegacySigningHash(rpc.Result);
                        else return null; // unknown future type not supported here
                    }
                    catch (JsonException)
                    {
                        return null;
                    }

                    if (msg is null || msg.Length == 0) return null;

                    // r and s must be present
                    var rMin = HexToMinimal(rpc.Result.R);
                    var sMin = HexToMinimal(rpc.Result.S);
                    if (rMin.Length == 0 || sMin.Length == 0)
                    {
                        return null;
                    }

                    var r = LeftPad32(rMin);
                    var s = LeftPad32(sMin);

                    // Build V using Signer-friendly normalization
                    byte vByte;
                    if (!string.IsNullOrEmpty(rpc.Result.YParity))
                    {
                        var vp = HexToMinimal(rpc.Result.YParity);
                        var parity = (byte)(vp.Length == 0 ? ZeroByte : vp[vp.Length - 1]);
                        vByte = (byte)(VParityBase + (parity & 0x01));
                    }
                    else
                    {
                        var vb = HexToMinimal(rpc.Result.V);
                        byte rawV = vb.Length == 0 ? ZeroByte : vb[vb.Length - 1];
                        if (rawV >= VEip155Threshold) vByte = (byte)(VParityBase + ((rawV - VEip155Threshold) % 2));
                        else if (rawV >= VParityBase) vByte = rawV;
                        else vByte = (byte)(VParityBase + (rawV % 2));
                    }

                    // Use Nethereum.Signer to carry V and extract recovery id consistently
                    var sig = EthECDSASignatureFactory.FromComponents(r, s, new[] { vByte });
                    var recId = (byte)(((sig.V[0] >= VParityBase ? (sig.V[0] - VParityBase) : sig.V[0]) & 0x01));

                    return new SignatureInfo(msg, r, s, recId);
                }
            }
            catch (HttpRequestException) { }
            catch (TaskCanceledException) { }
            catch (JsonException) { }

            return null;
        }

        #region Signing hash helpers (RLP build)

        private static byte[] EncodeAtom(byte[] data)
        {
            if (data is null || (data.Length == 1 && data[0] == ZeroByte))
            {
                return RLP.EncodeElement(Array.Empty<byte>());
            }

            return RLP.EncodeElement(data);
        }

        private static byte[] EncodeBytes(byte[] data)
        {
            return RLP.EncodeElement(data ?? Array.Empty<byte>());
        }

        // DTO-based overloads to avoid JsonElement round-trip
        private static byte[] ComputeType2SigningHash(TransactionRpcDto tx)
        {
            byte[] chainId = HexToMinimal(tx.ChainId);
            byte[] nonce = HexToMinimal(tx.Nonce);
            byte[] maxPriorityFee = HexToMinimal(tx.MaxPriorityFeePerGas);
            byte[] maxFee = HexToMinimal(tx.MaxFeePerGas);
            byte[] gasLimit = HexToMinimal(tx.Gas);

            var toStr = tx.To;
            byte[] to = string.IsNullOrEmpty(toStr) ? Array.Empty<byte>() : HexToAddress20(toStr);

            byte[] value = HexToMinimal(tx.Value);
            var inputHex = tx.Input;
            byte[] data = string.IsNullOrEmpty(inputHex) ? Array.Empty<byte>() : inputHex.HexToByteArray();
            byte[] accessList = BuildAccessListRlpEncoded(tx);

            var list = RLP.EncodeList(
                EncodeAtom(chainId),
                EncodeAtom(nonce),
                EncodeAtom(maxPriorityFee),
                EncodeAtom(maxFee),
                EncodeAtom(gasLimit),
                EncodeBytes(to),
                EncodeAtom(value),
                EncodeBytes(data),
                accessList
            );

            var prefixed = new byte[TypePrefixLength + list.Length];
            prefixed[0] = TxType02Prefix;
            Buffer.BlockCopy(list, 0, prefixed, 1, list.Length);
            return Sha3Keccack.Current.CalculateHash(prefixed);
        }

        private static byte[] ComputeType1SigningHash(TransactionRpcDto tx)
        {
            byte[] chainId = HexToMinimal(tx.ChainId);
            byte[] nonce = HexToMinimal(tx.Nonce);
            byte[] gasPrice = HexToMinimal(tx.GasPrice);
            byte[] gasLimit = HexToMinimal(tx.Gas);
            var toStr = tx.To;
            byte[] to = string.IsNullOrEmpty(toStr) ? Array.Empty<byte>() : HexToAddress20(toStr);
            byte[] value = HexToMinimal(tx.Value);
            var inputHex = tx.Input;
            byte[] data = string.IsNullOrEmpty(inputHex) ? Array.Empty<byte>() : inputHex.HexToByteArray();
            byte[] accessList = BuildAccessListRlpEncoded(tx);

            var list = RLP.EncodeList(
                EncodeAtom(chainId),
                EncodeAtom(nonce),
                EncodeAtom(gasPrice),
                EncodeAtom(gasLimit),
                EncodeBytes(to),
                EncodeAtom(value),
                EncodeBytes(data),
                accessList
            );

            var prefixed = new byte[TypePrefixLength + list.Length];
            prefixed[0] = TxType01Prefix;
            Buffer.BlockCopy(list, 0, prefixed, 1, list.Length);
            return Sha3Keccack.Current.CalculateHash(prefixed);
        }

        private static byte[] ComputeLegacySigningHash(TransactionRpcDto tx)
        {
            byte[] nonce = HexToMinimal(tx.Nonce);
            byte[] gasPrice = HexToMinimal(tx.GasPrice);
            byte[] gasLimit = HexToMinimal(tx.Gas);
            var toStr = tx.To;
            byte[] to = string.IsNullOrEmpty(toStr) ? Array.Empty<byte>() : HexToAddress20(toStr);
            byte[] value = HexToMinimal(tx.Value);
            var inputHex = tx.Input;
            byte[] data = string.IsNullOrEmpty(inputHex) ? Array.Empty<byte>() : inputHex.HexToByteArray();

            // Determine chainId via field or from v (EIP-155)
            byte[] chainIdBytes = HexToMinimal(tx.ChainId);
            if (chainIdBytes.Length == 0)
            {
                var vBytes = HexToMinimal(tx.V);
                if (vBytes.Length > 0)
                {
                    var v = vBytes[vBytes.Length - 1];
                    if (v >= VEip155Threshold)
                    {
                        int chainId = (v - VEip155Threshold) / 2;
                        if (chainId > 0)
                        {
                            chainIdBytes = IntToMinimal(chainId);
                        }
                    }
                }
            }

            var elements = new List<byte[]>
            {
                EncodeAtom(nonce),
                EncodeAtom(gasPrice),
                EncodeAtom(gasLimit),
                EncodeBytes(to),
                EncodeAtom(value),
                EncodeBytes(data)
            };

            if (chainIdBytes.Length > 0)
            {
                elements.Add(EncodeAtom(chainIdBytes));
                elements.Add(EncodeAtom(Array.Empty<byte>()));
                elements.Add(EncodeAtom(Array.Empty<byte>()));
            }

            var list = RLP.EncodeList(elements.ToArray());
            return Sha3Keccack.Current.CalculateHash(list);
        }

        private static byte[] IntToMinimal(int value)
        {
            if (value == 0)
            {
                return Array.Empty<byte>();
            }

            var bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            int idx = 0;
            while (idx < bytes.Length - 1 && bytes[idx] == 0)
            {
                idx++;
            }

            var res = new byte[bytes.Length - idx];
            Buffer.BlockCopy(bytes, idx, res, 0, res.Length);
            return res;
        }

        private static byte[] HexToMinimal(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Array.Empty<byte>();
            var bytes = hex.HexToByteArray();
            int idx = 0;
            while (idx < bytes.Length - 1 && bytes[idx] == 0)
            {
                idx++;
            }

            if (idx == 0)
            {
                return bytes;
            }

            var trimmed = new byte[bytes.Length - idx];
            Buffer.BlockCopy(bytes, idx, trimmed, 0, trimmed.Length);
            return trimmed;
        }

        private static byte[] LeftPad32(byte[] data)
        {
            if (data is null)
            {
                return new byte[Bytes32];
            }

            if (data.Length == Bytes32)
            {
                return data;
            }

            if (data.Length > Bytes32)
            {
                var cut = new byte[Bytes32];
                Buffer.BlockCopy(data, data.Length - Bytes32, cut, 0, Bytes32);
                return cut;
            }

            var res = new byte[Bytes32];
            Buffer.BlockCopy(data, 0, res, Bytes32 - data.Length, data.Length);
            return res;
        }

        private static byte[] HexToAddress20(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return Array.Empty<byte>();
            }

            var b = hex.HexToByteArray();
            if (b.Length == 0)
            {
                return Array.Empty<byte>();
            }

            if (b.Length == AddressBytes)
            {
                return b;
            }

            if (b.Length > AddressBytes)
            {
                var cut = new byte[AddressBytes];
                Buffer.BlockCopy(b, b.Length - AddressBytes, cut, 0, AddressBytes);
                return cut;
            }

            var padded = new byte[AddressBytes];
            Buffer.BlockCopy(b, 0, padded, AddressBytes - b.Length, b.Length);
            return padded;
        }

        // DTO-based overload for access list
        private static byte[] BuildAccessListRlpEncoded(TransactionRpcDto tx)
        {
            if (tx.AccessList is null || tx.AccessList.Count == 0)
            {
                return RLP.EncodeList();
            }

            var entryEnc = new List<byte[]>();
            foreach (var entry in tx.AccessList)
            {
                var addr = HexToAddress20(entry?.Address);

                byte[] storageKeysList;
                if (entry?.StorageKeys is null || entry.StorageKeys.Count == 0)
                {
                    storageKeysList = RLP.EncodeList();
                }
                else
                {
                    var keyItems = new List<byte[]>();
                    foreach (var sk in entry.StorageKeys)
                    {
                        var keyBytes = string.IsNullOrEmpty(sk) ? Array.Empty<byte>() : sk.HexToByteArray();
                        keyItems.Add(EncodeBytes(keyBytes));
                    }
                    storageKeysList = RLP.EncodeList(keyItems.ToArray());
                }

                var pair = RLP.EncodeList(
                    EncodeBytes(addr),
                    storageKeysList
                );
                entryEnc.Add(pair);
            }

            return RLP.EncodeList(entryEnc.ToArray());
        }

        #endregion

        #region DTOs

        private sealed class TxListResponse
        {
            public string Status { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public List<TxItem> Result { get; set; } = new List<TxItem>();
        }

        private sealed class TxItem
        {
            public string Hash { get; set; } = string.Empty;
            public string From { get; set; } = string.Empty;
        }

        // Minimal JSON-RPC wrapper for proxy responses
        private sealed class RpcResponse<T>
        {
            public string Jsonrpc { get; set; }
            public int? Id { get; set; }
            public T Result { get; set; }
            public RpcError Error { get; set; }
        }

        private sealed class RpcError
        {
            public int Code { get; set; }
            public string Message { get; set; }
        }

        // Minimal transaction DTO for eth_getTransactionByHash
        private sealed class TransactionRpcDto
        {
            public string Type { get; set; }
            public string ChainId { get; set; }
            public string Nonce { get; set; }
            public string GasPrice { get; set; }
            public string Gas { get; set; }
            public string MaxFeePerGas { get; set; }
            public string MaxPriorityFeePerGas { get; set; }
            public string To { get; set; }
            public string Value { get; set; }
            public string Input { get; set; }
            public List<AccessListEntry> AccessList { get; set; }
            public string V { get; set; }
            public string R { get; set; }
            public string S { get; set; }
            public string YParity { get; set; }
        }

        private sealed class AccessListEntry
        {
            public string Address { get; set; }
            public List<string> StorageKeys { get; set; }
        }

        internal sealed class SignatureInfo
        {
            public byte[] MessageHash { get; }
            public byte[] R { get; }
            public byte[] S { get; }
            public byte RecoveryId { get; }
            public SignatureInfo(byte[] msg, byte[] r, byte[] s, byte recId)
            {
                MessageHash = msg ?? Array.Empty<byte>();
                R = r ?? Array.Empty<byte>();
                S = s ?? Array.Empty<byte>();
                RecoveryId = recId;
            }
        }

        #endregion
    }
}
