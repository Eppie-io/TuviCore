﻿using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Tuvi.Core.Entities;

namespace Tuvi.Core.Backup.Impl.JsonUtf8.Converters
{
    class JsonAuthenticationDataConverter : JsonConverter<IAuthenticationData>
    {
        public override IAuthenticationData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;

            var doc = JsonDocument.ParseValue(ref reader);
            if (doc != null && doc.RootElement.TryGetProperty(nameof(IAuthenticationData.Type), out var property) && property.ValueKind == JsonValueKind.String)
            {
                IAuthenticationData result = null;

                switch (property.GetString())
                {
                    case nameof(AuthenticationType.Basic):
                        result = new BasicAuthData()
                        {
                            Password = GetString(doc.RootElement, nameof(BasicAuthData.Password)),
                            IncomingLogin = GetString(doc.RootElement, nameof(BasicAuthData.IncomingLogin)),
                            IncomingPassword = GetString(doc.RootElement, nameof(BasicAuthData.IncomingPassword)),
                            OutgoingLogin = GetString(doc.RootElement, nameof(BasicAuthData.OutgoingLogin)),
                            OutgoingPassword = GetString(doc.RootElement, nameof(BasicAuthData.OutgoingPassword))
                        };
                        break;
                    case nameof(AuthenticationType.OAuth2):
                        result = new OAuth2Data()
                        {
                            RefreshToken = GetString(doc.RootElement, nameof(OAuth2Data.RefreshToken)),
                            AuthAssistantId = GetString(doc.RootElement, nameof(OAuth2Data.AuthAssistantId))
                        };
                        break;
                    case nameof(AuthenticationType.Proton):
                        result = new ProtonAuthData()
                        {
                            UserId = GetString(doc.RootElement, nameof(ProtonAuthData.UserId)),
                            RefreshToken = GetString(doc.RootElement, nameof(ProtonAuthData.RefreshToken)),
                            SaltedPassword = GetString(doc.RootElement, nameof(ProtonAuthData.SaltedPassword))
                        };
                        break;
                };

                return result;
            }

            return null;
        }

        public override void Write(Utf8JsonWriter writer, IAuthenticationData value, JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();
            writer.WriteString(nameof(IAuthenticationData.Type), value.Type.ToString());

            switch (value)
            {
                case BasicAuthData basicData:
                    writer.WriteString(nameof(BasicAuthData.Password), basicData.Password);
                    writer.WriteString(nameof(BasicAuthData.IncomingLogin), basicData.IncomingLogin);
                    writer.WriteString(nameof(BasicAuthData.IncomingPassword), basicData.IncomingPassword);
                    writer.WriteString(nameof(BasicAuthData.OutgoingLogin), basicData.OutgoingLogin);
                    writer.WriteString(nameof(BasicAuthData.OutgoingPassword), basicData.OutgoingPassword);
                    break;
                case OAuth2Data oauth2Data:
                    writer.WriteString(nameof(OAuth2Data.RefreshToken), oauth2Data.RefreshToken);
                    writer.WriteString(nameof(OAuth2Data.AuthAssistantId), oauth2Data.AuthAssistantId);
                    break;
                case ProtonAuthData protonData:
                    writer.WriteString(nameof(ProtonAuthData.UserId), protonData.UserId);
                    writer.WriteString(nameof(ProtonAuthData.RefreshToken), protonData.RefreshToken);
                    writer.WriteString(nameof(ProtonAuthData.SaltedPassword), protonData.SaltedPassword);
                    break;
            }

            writer.WriteEndObject();
        }

        private static string GetString(JsonElement element, string propName)
        {
            if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propName, out var prop))
            {
                return prop.GetString();
            }

            return default;
        }
    }
}
