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
using System.Text.Json;
using System.Text.Json.Serialization;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Backup.Impl.JsonUtf8.Converters
{
    /// <summary>
    /// Custom JSON converter for ProtectionInfo class to handle read-only SignaturesInfo collection.
    /// </summary>
    internal class JsonProtectionInfoConverter : JsonConverter<ProtectionInfo>
    {
        /// <summary>
        /// Reads and converts the JSON to a ProtectionInfo object.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">Serializer options.</param>
        /// <returns>The converted ProtectionInfo object.</returns>
        /// <exception cref="JsonException">Thrown when the JSON structure is invalid or required fields are missing.</exception>
        public override ProtectionInfo Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token");
            }

            var protectionInfo = new ProtectionInfo();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return protectionInfo;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected PropertyName token");
                }

                string propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case nameof(ProtectionInfo.Type):
                        protectionInfo.Type = (MessageProtectionType)reader.GetInt32();
                        break;
                    case nameof(ProtectionInfo.SignaturesInfo):
                        DeserializeSignatureCollection(ref reader, protectionInfo.SignaturesInfo, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            throw new JsonException("Unexpected end of JSON");
        }

        /// <summary>
        /// Writes a ProtectionInfo object as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The ProtectionInfo object to serialize.</param>
        /// <param name="options">Serializer options.</param>
        public override void Write(Utf8JsonWriter writer, ProtectionInfo value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();

            writer.WriteNumber(nameof(ProtectionInfo.Type), (int)value.Type);

            writer.WritePropertyName(nameof(ProtectionInfo.SignaturesInfo));
            JsonSerializer.Serialize(writer, value.SignaturesInfo, options);

            writer.WriteEndObject();
        }

        private static void DeserializeSignatureCollection(ref Utf8JsonReader reader, IList<SignatureInfo> collection, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return;
            }

            var signatures = JsonSerializer.Deserialize<List<SignatureInfo>>(ref reader, options);
            if (signatures != null)
            {
                foreach (var signature in signatures)
                {
                    collection.Add(signature);
                }
            }
        }
    }
}
