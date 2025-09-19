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
using System.Net.Http;
using System.Runtime.CompilerServices;
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
#pragma warning restore CA1812
        }
    }
}
