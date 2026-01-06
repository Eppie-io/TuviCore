// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2026 Eppie (https://eppie.io)                                    //
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
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using KeyDerivation.Keys;
using KeyDerivationLib;
using Microsoft.Extensions.Logging;
using NBitcoin;
using Tuvi.Base32EConverterLib;
using Tuvi.Core.Logging;

[assembly: InternalsVisibleTo("Tuvi.Core.Dec.Bitcoin.Tests")]

namespace Tuvi.Core.Dec.Bitcoin
{
    /// <summary>
    /// Configuration for Bitcoin network settings.
    /// </summary>
    internal class BitcoinNetworkConfig
    {
        public Network Network { get; }
        public string NetworkApiPrefix { get; }
        public ScriptPubKeyType SupportedAddressType { get; }

        public BitcoinNetworkConfig(Network network, string networkApiPrefix, ScriptPubKeyType supportedAddressType)
        {
            Network = network ?? throw new ArgumentNullException(nameof(network));
            NetworkApiPrefix = networkApiPrefix ?? throw new ArgumentNullException(nameof(networkApiPrefix));
            SupportedAddressType = supportedAddressType;
        }

        public static readonly BitcoinNetworkConfig TestNet4 = new BitcoinNetworkConfig(NBitcoin.Network.TestNet4, "testnet4/", ScriptPubKeyType.Legacy);
        public static readonly BitcoinNetworkConfig MainNet = new BitcoinNetworkConfig(NBitcoin.Network.Main, "", ScriptPubKeyType.Legacy);
    }

    internal static class Constants
    {
        public const int BitcoinCoinType = 0; // BIP44: coin type 0 for Bitcoin
        public const int ExternalChain = 0; // BIP44: 0 for external chain (receiving addresses)
        public const int DefaultMaxPages = 100; // Maximum number of pages to fetch for transactions
    }

    /// <summary>
    /// Internal class for logging context in BitcoinTools.
    /// </summary>
    internal class BitcoinToolsLogger { }

    /// <summary>
    /// Provides utility methods for Bitcoin-related operations, such as address and key derivation,
    /// and public key retrieval from the blockchain.
    /// </summary>
    internal static class BitcoinToolsImpl
    {
        private static readonly ILogger Logger = LoggingExtension.Log<BitcoinToolsLogger>();

        internal static string DeriveBitcoinAddress(BitcoinNetworkConfig config, MasterKey masterKey, int account, int index)
        {
            ValidateConfigAndDerivationInputs(config, masterKey, account, index);

            try
            {
                using (var key = DerivePrivateKey(config, masterKey, account, index))
                {
                    PubKey pubKey = key.PubKey;
                    BitcoinAddress address = pubKey.GetAddress(config.SupportedAddressType, config.Network);

                    return address.ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to derive Bitcoin address from master key.");
                throw new InvalidOperationException("Failed to derive Bitcoin address from master key.", ex);
            }
        }

        internal static string DeriveBitcoinSecretKeyWif(BitcoinNetworkConfig config, MasterKey masterKey, int account, int index)
        {
            ValidateConfigAndDerivationInputs(config, masterKey, account, index);

            try
            {
                using (var key = DerivePrivateKey(config, masterKey, account, index))
                {
                    return key.GetWif(config.Network).ToString();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to derive Bitcoin secret key (WIF) from master key.");
                throw new InvalidOperationException("Failed to derive Bitcoin secret key (WIF) from master key.", ex);
            }
        }

        private static Key DerivePrivateKey(BitcoinNetworkConfig config, MasterKey masterKey, int account, int index)
        {
            using (var derivedKey = DerivationKeyFactory.CreatePrivateDerivationKeyBip44(masterKey, Constants.BitcoinCoinType, account, Constants.ExternalChain, index))
            {
                if (derivedKey.Scalar.Length != 32)
                {
                    Logger.LogError("Derived scalar key must be exactly 32 bytes.");
                    throw new InvalidOperationException("Derived scalar key must be exactly 32 bytes.");
                }

                return new Key(derivedKey.Scalar.ToArray());
            }
        }

        internal static async Task<string> RetrievePublicKeyAsync(BitcoinNetworkConfig config, string address, HttpClient httpClient, CancellationToken cancellationToken = default)
        {
            if (config is null)
            {
                Logger.LogError("Configuration is null.");
                throw new ArgumentNullException(nameof(config));
            }

            if (string.IsNullOrEmpty(address))
            {
                Logger.LogError("Address is null or empty.");
                throw new ArgumentNullException(nameof(address), "Address cannot be null or empty.");
            }

            if (httpClient is null)
            {
                Logger.LogError("HttpClient is null.");
                throw new ArgumentNullException(nameof(httpClient));
            }

            BitcoinAddress bitcoinAddress;
            try
            {
                bitcoinAddress = BitcoinAddress.Create(address, config.Network);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Invalid Bitcoin address for the specified network: {Address}.", address);
                throw new ArgumentException("Invalid Bitcoin address for the specified network.", nameof(address), ex);
            }

            Transaction transaction = await FetchSpentTransactionAsync(config, address, httpClient, cancellationToken).ConfigureAwait(false);
            if (transaction is null)
            {
                return null;
            }

            return ExtractPublicKeyFromTransaction(transaction, bitcoinAddress, config);
        }

        private static string ExtractPublicKeyFromTransaction(Transaction transaction, BitcoinAddress bitcoinAddress, BitcoinNetworkConfig config)
        {
            // P2PKH
            foreach (TxIn input in transaction.Inputs)
            {
                Script scriptSig = input.ScriptSig;
                if (Script.IsNullOrEmpty(scriptSig))
                {
                    continue;
                }

                IEnumerable<PubKey> pubKeys = scriptSig.GetAllPubKeys();
                foreach (PubKey pubKey in pubKeys)
                {
                    BitcoinAddress derivedAddress = pubKey.GetAddress(config.SupportedAddressType, config.Network);
                    if (derivedAddress == bitcoinAddress)
                    {
                        return Base32EConverter.ToEmailBase32(pubKey.Compress().ToBytes());
                    }
                }
            }

            // P2PK (legacy transactions paying directly to a public key)
            foreach (var output in transaction.Outputs)
            {
                var spk = output.ScriptPubKey;
                var pk = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(spk);
                if (pk != null)
                {
                    var addr = pk.GetAddress(ScriptPubKeyType.Legacy, config.Network);
                    if (addr == bitcoinAddress)
                    {
                        return Base32EConverter.ToEmailBase32(pk.Compress().ToBytes());
                    }
                }
            }

            return null;
        }

        private static void ValidateConfigAndDerivationInputs(BitcoinNetworkConfig config, MasterKey masterKey, int account, int index)
        {
            if (config is null)
            {
                Logger.LogError("Configuration is null.");
                throw new ArgumentNullException(nameof(config), "Configuration cannot be null.");
            }

            if (masterKey is null)
            {
                Logger.LogError("Master key is null.");
                throw new ArgumentNullException(nameof(masterKey), "Master key cannot be null.");
            }

            if (account < 0)
            {
                Logger.LogError("Account index is negative: {Account}.", account);
                throw new ArgumentOutOfRangeException(nameof(account), "Account index must be non-negative integer.");
            }

            if (index < 0)
            {
                Logger.LogError("Address index is negative: {Index}.", index);
                throw new ArgumentOutOfRangeException(nameof(index), "Address index must be non-negative integer.");
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        // TODO: Add RPC node support for fetching transactions by default
        internal static async Task<Transaction> FetchSpentTransactionAsync(
            BitcoinNetworkConfig config,
            string address,
            HttpClient httpClient,
            CancellationToken cancellation)
        {
            if (IsWellKnownAddress(address))
            {
                return await FetchWellKnownTransactionAsync(address, config, httpClient, cancellation).ConfigureAwait(false);
            }

            const int MaxPages = Constants.DefaultMaxPages;
            string afterTxId = null;
            string previousLastTxId = null;

            for (int page = 0; page < MaxPages; page++)
            {
                cancellation.ThrowIfCancellationRequested();

                string url = $"https://mempool.space/{config.NetworkApiPrefix}api/address/{address}/txs/chain";
                if (!string.IsNullOrEmpty(afterTxId))
                {
                    url += "?after_txid=" + afterTxId;
                }

                string txsResponse;
                try
                {
                    txsResponse = await httpClient.GetStringAsync(url).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    Logger.LogError(ex, "Failed to fetch transactions from {Url}.", url);
                    return null;
                }

                Dto.TransactionSummary[] transactions;
                try
                {
                    transactions = JsonSerializer.Deserialize<Dto.TransactionSummary[]>(txsResponse, JsonOptions);
                }
                catch (JsonException ex)
                {
                    Logger.LogWarning(ex, "JSON parse error for {Url}.", url);
                    return null;
                }

                if (transactions is null || transactions.Length == 0)
                {
                    return null;
                }

                foreach (var tx in transactions)
                {
                    cancellation.ThrowIfCancellationRequested();

                    if (string.IsNullOrWhiteSpace(tx?.TxId) || tx.Inputs is null)
                    {
                        Logger.LogWarning("Skipping malformed transaction entry.");
                        continue;
                    }

                    foreach (var input in tx.Inputs)
                    {
                        cancellation.ThrowIfCancellationRequested();

                        var prevAddr = input?.PreviousOutput?.ScriptPubKeyAddress;
                        if (!string.IsNullOrEmpty(prevAddr) && prevAddr == address)
                        {
                            string hexUrl = $"https://mempool.space/{config.NetworkApiPrefix}api/tx/{tx.TxId}/hex";
                            string hex;
                            try
                            {
                                hex = await httpClient.GetStringAsync(hexUrl).ConfigureAwait(false);
                            }
                            catch (HttpRequestException ex)
                            {
                                Logger.LogError(ex, "Failed to fetch transaction hex from {HexUrl}.", hexUrl);
                                return null;
                            }

                            try
                            {
                                return Transaction.Parse(hex, config.Network);
                            }
#pragma warning disable CA1031 // Do not catch general exceptions, but handle specific ones
                            catch (Exception ex)
                            {
                                Logger.LogError(ex, "Failed to parse transaction {TxId}.", tx.TxId);
                                return null;
                            }
#pragma warning restore CA1031
                        }
                    }
                }

                var last = transactions[transactions.Length - 1];
                if (last is null || string.IsNullOrWhiteSpace(last.TxId))
                {
                    return null;
                }

                previousLastTxId = afterTxId;
                afterTxId = last.TxId;

                if (afterTxId == previousLastTxId)
                {
                    Logger.LogWarning("Pagination stalled (after_txid did not advance) for address: {Address}.", address);
                    return null;
                }
            }

            Logger.LogWarning("Reached max pages ({MaxPages}) without finding a spent transaction for address: {Address}.",
                MaxPages, address);

            return null;
        }

        private const string HalFinneyAddress = "1Q2TWHE3GMdB6BZKafqwxXtWAWgFt5Jvm3";
        private const string SatoshiNakamotoAddress = "12cbQLTFMXRnSzktFkuoG3eHoMeFtpTu3S";

        private static async Task<Transaction> FetchWellKnownTransactionAsync(string address, BitcoinNetworkConfig config, HttpClient httpClient, CancellationToken cancellation)
        {
            string trId;
            switch (address)
            {
                case HalFinneyAddress:
                case SatoshiNakamotoAddress:
                    trId = "f4184fc596403b9d638783cf57adfe4c75c605f6356fbc91338530e9831e9e16";
                    break;
                default:
                    throw new ArgumentException($"Address {address} is not recognized as a well-known address.", address);
            }

            string hexUrl = $"https://mempool.space/{config.NetworkApiPrefix}api/tx/{trId}/hex";
            string hex;
            try
            {
                hex = await httpClient.GetStringAsync(hexUrl).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "Failed to fetch transaction hex from {HexUrl}.", hexUrl);
                return null;
            }

            try
            {
                return Transaction.Parse(hex, config.Network);
            }
#pragma warning disable CA1031 // Do not catch general exceptions, but handle specific ones
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Logger.LogError(ex, "Failed to parse transaction {TxId}.", trId);
                return null;
            }
        }

        private static bool IsWellKnownAddress(string address)
        {
            return address == HalFinneyAddress
                || address == SatoshiNakamotoAddress;
        }

        internal static async Task ActivateBitcoinAddressAsync(
            BitcoinNetworkConfig config,
            MasterKey masterKey,
            int account,
            int index,
            HttpClient httpClient,
            CancellationToken cancellation = default)
        {
            ValidateConfigAndDerivationInputs(config, masterKey, account, index);

            if (httpClient is null)
            {
                Logger.LogError("HttpClient is null.");
                throw new ArgumentNullException(nameof(httpClient));
            }

            string address = DeriveBitcoinAddress(config, masterKey, account, index);
            string wif = DeriveBitcoinSecretKeyWif(config, masterKey, account, index);

            string txHex = await BuildAndSignSpendAllToSameAddressTransactionAsync(config, address, wif, httpClient, cancellation: cancellation).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(txHex))
            {
                throw new InvalidOperationException("Failed to build and sign activation transaction.");
            }

            string txid = await BroadcastTransactionAsync(config, txHex, httpClient, cancellation).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(txid))
            {
                throw new InvalidOperationException("Failed to broadcast activation transaction.");
            }

            Logger.LogInformation("Activation transaction broadcasted for address {Address}. TxId: {TxId}.", address, txid);
        }

        internal static async Task<string> BuildAndSignSpendAllToSameAddressTransactionAsync(
            BitcoinNetworkConfig config,
            string address,
            string wif,
            HttpClient httpClient,
            int feeRateSatsPerVByte = 1,
            CancellationToken cancellation = default)
        {
            if (config is null)
            {
                Logger.LogError("Configuration is null.");
                throw new ArgumentNullException(nameof(config));
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                Logger.LogError("Address is null or empty.");
                throw new ArgumentNullException(nameof(address));
            }

            if (string.IsNullOrWhiteSpace(wif))
            {
                Logger.LogError("WIF is null or empty.");
                throw new ArgumentNullException(nameof(wif));
            }

            if (httpClient is null)
            {
                Logger.LogError("HttpClient is null.");
                throw new ArgumentNullException(nameof(httpClient));
            }

            BitcoinAddress bitcoinAddress;
            try
            {
                bitcoinAddress = BitcoinAddress.Create(address, config.Network);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Invalid Bitcoin address for the specified network: {Address}.", address);
                throw new ArgumentException("Invalid Bitcoin address for the specified network.", nameof(address), ex);
            }

            string utxoUrl = $"https://mempool.space/{config.NetworkApiPrefix}api/address/{address}/utxo";
            string json = await FetchUtxoJsonAsync(utxoUrl, httpClient, address, cancellation).ConfigureAwait(false);
            if (json is null)
            {
                return null;
            }

            var utxoList = ParseUtxoList(json, address);
            if (utxoList is null || utxoList.Count == 0)
            {
                Logger.LogInformation("No UTXOs found or failed to parse UTXOs for address {Address}.", address);
                return null;
            }

            long totalSatoshis = 0;
            foreach (var t in utxoList)
            {
                totalSatoshis += t.Item3;
            }

            if (totalSatoshis <= 0)
            {
                Logger.LogInformation("Address {Address} has zero balance.", address);
                return null;
            }

            var secret = ParseSecret(wif, config.Network, address);
            if (secret is null)
            {
                return null;
            }

            var coins = CreateCoinsFromUtxos(utxoList, bitcoinAddress, address);
            if (coins.Count == 0)
            {
                return null;
            }

            long feeSatoshis = EstimateFeeSatoshis(utxoList.Count, 1, feeRateSatsPerVByte);
            if (totalSatoshis <= feeSatoshis)
            {
                Logger.LogWarning("Insufficient funds for address {Address}. Total: {Total}, Fee: {Fee}.", address, totalSatoshis, feeSatoshis);
                return null;
            }

            var hex = BuildAndSignTransactionHex(coins, secret, bitcoinAddress, feeSatoshis, config.Network, address);
            if (hex != null)
            {
                Logger.LogInformation("Built and signed transaction for address {Address}. Total sats: {Total}, Fee sats: {Fee}.", address, totalSatoshis, feeSatoshis);
            }

            return hex;
        }

        internal static async Task<string> BroadcastTransactionAsync(
            BitcoinNetworkConfig config,
            string txHex,
            HttpClient httpClient,
            CancellationToken cancellation = default)
        {
            if (config is null)
            {
                Logger.LogError("Configuration is null.");
                throw new ArgumentNullException(nameof(config));
            }

            if (string.IsNullOrWhiteSpace(txHex))
            {
                Logger.LogError("Transaction hex is null or empty.");
                throw new ArgumentNullException(nameof(txHex));
            }

            if (httpClient is null)
            {
                Logger.LogError("HttpClient is null.");
                throw new ArgumentNullException(nameof(httpClient));
            }

            var pushUrl = $"https://mempool.space/{config.NetworkApiPrefix}api/tx";

            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Post, pushUrl))
                {
                    req.Content = new StringContent(txHex, Encoding.UTF8, "text/plain");

                    using (HttpResponseMessage resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead, cancellation).ConfigureAwait(false))
                    {
                        resp.EnsureSuccessStatusCode();
                        var txid = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        Logger.LogInformation("Broadcasted transaction. Returned txid: {TxId}.", txid);
                        return txid?.Trim();
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "Failed to broadcast transaction to {Url}.", pushUrl);
                return null;
            }
            catch (OperationCanceledException ex) when (cancellation.IsCancellationRequested)
            {
                Logger.LogInformation(ex, "Broadcast cancelled.");
                return null;
            }
        }

        internal static async Task<string> FetchUtxoJsonAsync(string utxoUrl, HttpClient httpClient, string address, CancellationToken cancellationToken)
        {
            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Get, utxoUrl))
                using (HttpResponseMessage resp = await httpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false))
                {
                    resp.EnsureSuccessStatusCode();
                    return await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex, "Failed to fetch UTXOs from {Url}.", utxoUrl);
                return null;
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
            {
                Logger.LogInformation(ex, "UTXO fetch cancelled for address {Address}.", address);
                return null;
            }
        }

        internal static List<Tuple<string, int, long>> ParseUtxoList(string json, string address)
        {
            try
            {
                var entries = JsonSerializer.Deserialize<Dto.UtxoEntry[]>(json, JsonOptions);
                if (entries is null || entries.Length == 0)
                {
                    Logger.LogWarning("UTXO response deserialized to empty for address {Address}.", address);
                    return null;
                }

                var utxoList = new List<Tuple<string, int, long>>(entries.Length);
                foreach (var e in entries)
                {
                    if (e is null)
                    {
                        Logger.LogWarning("Encountered null UTXO entry for address {Address}.", address);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(e.TxId))
                    {
                        Logger.LogWarning("Empty txid in UTXO entry for address {Address}.", address);
                        continue;
                    }

                    if (e.Vout < 0)
                    {
                        Logger.LogWarning("Negative vout in UTXO entry for address {Address}: {Vout}.", address, e.Vout);
                        continue;
                    }

                    if (e.Value < 0)
                    {
                        Logger.LogWarning("Negative value in UTXO entry for address {Address}: {Value}.", address, e.Value);
                        continue;
                    }

                    utxoList.Add(Tuple.Create(e.TxId, e.Vout, e.Value));
                }

                return utxoList;
            }
            catch (JsonException ex)
            {
                Logger.LogWarning(ex, "Failed to deserialize UTXO JSON for address {Address}.", address);
                return null;
            }
            catch (FormatException ex)
            {
                Logger.LogWarning(ex, "Invalid data format in UTXO JSON for address {Address}.", address);
                return null;
            }
            catch (OverflowException ex)
            {
                Logger.LogWarning(ex, "Numeric overflow in UTXO JSON for address {Address}.", address);
                return null;
            }
        }

        internal static List<Coin> CreateCoinsFromUtxos(List<Tuple<string, int, long>> utxoList, BitcoinAddress bitcoinAddress, string address)
        {
            var coins = new List<Coin>();
            try
            {
                foreach (var t in utxoList)
                {
                    var outpoint = new OutPoint(uint256.Parse(t.Item1), t.Item2);
                    var txOut = new TxOut(Money.Satoshis(t.Item3), bitcoinAddress.ScriptPubKey);
                    coins.Add(new Coin(outpoint, txOut));
                }
            }
            catch (FormatException ex)
            {
                Logger.LogError(ex, "Invalid txid format in UTXO list for address {Address}.", address);
                return new List<Coin>();
            }
            catch (OverflowException ex)
            {
                Logger.LogError(ex, "Numeric overflow in UTXO list for address {Address}.", address);
                return new List<Coin>();
            }

            return coins;
        }

        private static BitcoinSecret ParseSecret(string wif, Network network, string address)
        {
            try
            {
                return new BitcoinSecret(wif, network);
            }
            catch (FormatException ex)
            {
                Logger.LogError(ex, "Invalid WIF format provided for address {Address}.", address);
                return null;
            }
            catch (ArgumentException ex)
            {
                Logger.LogError(ex, "Invalid WIF/network combination for address {Address}.", address);
                return null;
            }
        }

        /// <summary>
        /// Estimates the transaction fee in satoshis using a simplified heuristic.
        /// </summary>
        /// <param name="inputCount">Number of inputs (UTXOs) included in the transaction.</param>
        /// <param name="outputCount">Number of outputs in the transaction.</param>
        /// <param name="feeRateSatsPerVByte">Fee rate in satoshis per virtual byte (sats/vByte). A minimum of 1 is enforced.</param>
        /// <returns>
        /// Estimated fee in satoshis, calculated as estimated_vsize * feeRateSatsPerVByte.
        /// </returns>
        /// <remarks>
        /// The virtual size is approximated as: vsize ≈ 10 + 148 * inputs + 34 * outputs.
        /// This approximation fits legacy P2PKH transactions. For SegWit (P2WPKH), P2SH, Taproot or
        /// other non-legacy output/input types the estimate may be inaccurate. For precise fee
        /// calculation consider computing the actual transaction weight including witness data
        /// or using the real signed transaction size from a transaction builder.
        /// </remarks>
        private static long EstimateFeeSatoshis(int inputCount, int outputCount, int feeRateSatsPerVByte)
        {
            int estimatedVSize = 10 + inputCount * 148 + outputCount * 34;
            return (long)Math.Max(1, feeRateSatsPerVByte) * estimatedVSize;
        }

        private static string BuildAndSignTransactionHex(List<Coin> coins, BitcoinSecret secret, BitcoinAddress destAddress, long feeSatoshis, Network network, string address)
        {
            try
            {
                var builder = network.CreateTransactionBuilder();
                builder.AddCoins(coins);
                builder.AddKeys(secret);
                builder.SendAll(destAddress);
                builder.SendFees(Money.Satoshis(feeSatoshis));

                var tx = builder.BuildTransaction(sign: true);

                var verified = builder.Verify(tx);
                if (!verified)
                {
                    Logger.LogWarning("Built transaction failed verification for address {Address}.", address);
                    return null;
                }

                return tx.ToHex();
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogError(ex, "Failed to build/sign transaction for address {Address}: invalid operation.", address);
                return null;
            }
            catch (ArgumentException ex)
            {
                Logger.LogError(ex, "Failed to build/sign transaction for address {Address}: invalid argument.", address);
                return null;
            }
        }

        // DTO classes for JSON deserialization of transaction data
        private static class Dto
        {
            // Suppress CA1812: Class is used implicitly by JsonSerializer for deserialization
#pragma warning disable CA1812
            internal sealed class TransactionSummary
            {
                [JsonPropertyName("txid")]
                public string TxId { get; set; }

                [JsonPropertyName("vin")]
                public Input[] Inputs { get; set; }
            }

            internal sealed class Input
            {
                [JsonPropertyName("prevout")]
                public PreviousOutput PreviousOutput { get; set; }
            }

            internal sealed class PreviousOutput
            {
                [JsonPropertyName("scriptpubkey_address")]
                public string ScriptPubKeyAddress { get; set; }
            }

            internal sealed class UtxoEntry
            {
                [JsonPropertyName("txid")]
                public string TxId { get; set; }

                [JsonPropertyName("vout")]
                public int Vout { get; set; }

                [JsonPropertyName("value")]
                public long Value { get; set; }
            }
#pragma warning restore CA1812
        }
    }
}
