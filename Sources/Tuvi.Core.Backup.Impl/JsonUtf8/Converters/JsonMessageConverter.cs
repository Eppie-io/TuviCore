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
using System.Text.Json;
using System.Text.Json.Serialization;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Backup.Impl.JsonUtf8.Converters
{
    /// <summary>
    /// Custom JSON converter for Message class to handle read-only collection properties
    /// that use lazy initialization (From, To, Cc, Bcc, ReplyTo, Attachments, Protection).
    /// </summary>
    internal class JsonMessageConverter : JsonConverter<Message>
    {
        /// <summary>
        /// Reads and converts the JSON to a Message object.
        /// </summary>
        /// <param name="reader">The reader to read from.</param>
        /// <param name="typeToConvert">The type to convert.</param>
        /// <param name="options">Serializer options.</param>
        /// <returns>The converted Message object.</returns>
        /// <exception cref="JsonException">Thrown when the JSON structure is invalid or required fields are missing.</exception>
        public override Message Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected StartObject token");
            }

            var message = new Message();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return message;
                }

                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException("Expected PropertyName token");
                }

                string propertyName = reader.GetString();
                reader.Read();

                switch (propertyName)
                {
                    case nameof(Message.Id):
                        message.Id = reader.GetUInt32();
                        break;
                    case nameof(Message.Subject):
                        message.Subject = reader.GetString();
                        break;
                    case nameof(Message.Date):
                        message.Date = reader.GetDateTimeOffset();
                        break;
                    case nameof(Message.TextBody):
                        message.TextBody = reader.GetString();
                        break;
                    case nameof(Message.HtmlBody):
                        message.HtmlBody = reader.GetString();
                        break;
                    case nameof(Message.PreviewText):
                        message.PreviewText = reader.GetString();
                        break;
                    case nameof(Message.IsMarkedAsRead):
                        message.IsMarkedAsRead = reader.GetBoolean();
                        break;
                    case nameof(Message.IsFlagged):
                        message.IsFlagged = reader.GetBoolean();
                        break;
                    case nameof(Message.IsDecentralized):
                        message.IsDecentralized = reader.GetBoolean();
                        break;
                    case nameof(Message.From):
                        DeserializeEmailCollection(ref reader, message.From, options);
                        break;
                    case nameof(Message.To):
                        DeserializeEmailCollection(ref reader, message.To, options);
                        break;
                    case nameof(Message.Cc):
                        DeserializeEmailCollection(ref reader, message.Cc, options);
                        break;
                    case nameof(Message.Bcc):
                        DeserializeEmailCollection(ref reader, message.Bcc, options);
                        break;
                    case nameof(Message.ReplyTo):
                        DeserializeEmailCollection(ref reader, message.ReplyTo, options);
                        break;
                    case nameof(Message.Attachments):
                        DeserializeAttachmentCollection(ref reader, message.Attachments, options);
                        break;
                    case nameof(Message.Protection):
                        message.Protection = JsonSerializer.Deserialize<ProtectionInfo>(ref reader, options);
                        break;
                    default:
                        // Safely skip unknown property value even when reader works on partial buffers
                        _ = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
                        break;
                }
            }

            throw new JsonException("Unexpected end of JSON");
        }

        /// <summary>
        /// Writes a Message object as JSON.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <param name="value">The Message object to serialize.</param>
        /// <param name="options">Serializer options.</param>
        public override void Write(Utf8JsonWriter writer, Message value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();

            writer.WriteNumber(nameof(Message.Id), value.Id);
            writer.WriteString(nameof(Message.Subject), value.Subject);
            writer.WriteString(nameof(Message.Date), value.Date);
            writer.WriteString(nameof(Message.TextBody), value.TextBody);
            writer.WriteString(nameof(Message.HtmlBody), value.HtmlBody);
            writer.WriteString(nameof(Message.PreviewText), value.PreviewText);

            writer.WriteBoolean(nameof(Message.IsMarkedAsRead), value.IsMarkedAsRead);
            writer.WriteBoolean(nameof(Message.IsFlagged), value.IsFlagged);
            writer.WriteBoolean(nameof(Message.IsDecentralized), value.IsDecentralized);

            WriteEmailCollection(writer, nameof(Message.From), value.From, options);
            WriteEmailCollection(writer, nameof(Message.To), value.To, options);
            WriteEmailCollection(writer, nameof(Message.Cc), value.Cc, options);
            WriteEmailCollection(writer, nameof(Message.Bcc), value.Bcc, options);
            WriteEmailCollection(writer, nameof(Message.ReplyTo), value.ReplyTo, options);
            WriteAttachmentCollection(writer, nameof(Message.Attachments), value.Attachments, options);

            if (value.Protection != null)
            {
                writer.WritePropertyName(nameof(Message.Protection));
                JsonSerializer.Serialize(writer, value.Protection, options);
            }

            writer.WriteEndObject();
        }

        private static void DeserializeEmailCollection(ref Utf8JsonReader reader, IList<EmailAddress> collection, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return;
            }

            var emails = JsonSerializer.Deserialize<List<EmailAddress>>(ref reader, options);
            if (emails != null)
            {
                foreach (var email in emails)
                {
                    collection.Add(email);
                }
            }
        }

        private static void DeserializeAttachmentCollection(ref Utf8JsonReader reader, IList<Attachment> collection, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return;
            }

            var attachments = JsonSerializer.Deserialize<List<Attachment>>(ref reader, options);
            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    collection.Add(attachment);
                }
            }
        }

        private static void WriteEmailCollection(Utf8JsonWriter writer, string propertyName, IList<EmailAddress> collection, JsonSerializerOptions options)
        {
            writer.WritePropertyName(propertyName);
            JsonSerializer.Serialize(writer, collection, options);
        }

        private static void WriteAttachmentCollection(Utf8JsonWriter writer, string propertyName, IList<Attachment> collection, JsonSerializerOptions options)
        {
            writer.WritePropertyName(propertyName);
            JsonSerializer.Serialize(writer, collection, options);
        }
    }
}
