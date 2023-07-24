using Tuvi.Core.Entities;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tuvi.Core.Backup.Impl.JsonUtf8.Converters
{
    class JsonAuthenticationDataConverter : JsonConverter<IAuthenticationData>
    {
        public override IAuthenticationData Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if(reader.TokenType == JsonTokenType.Null) return null;

            var doc = JsonDocument.ParseValue(ref reader);
            if (doc != null && doc.RootElement.TryGetProperty(nameof(IAuthenticationData.Type), out var property) && property.ValueKind == JsonValueKind.String)
            {
                IAuthenticationData result = null;

                switch (property.GetString())
                {
                    case nameof(AuthenticationType.Basic):
                        result = new BasicAuthData()
                        {
                            Password = GetString(doc.RootElement, nameof(BasicAuthData.Password))
                        };
                        break;
                    case nameof(AuthenticationType.OAuth2): 
                        result = new OAuth2Data()
                        {
                            RefreshToken = GetString(doc.RootElement, nameof(OAuth2Data.RefreshToken)),
                            AuthAssistantId = GetString(doc.RootElement, nameof(OAuth2Data.AuthAssistantId))
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
                    break;
                case OAuth2Data oauth2Data:
                    writer.WriteString(nameof(OAuth2Data.RefreshToken), oauth2Data.RefreshToken);
                    writer.WriteString(nameof(OAuth2Data.AuthAssistantId), oauth2Data.AuthAssistantId);
                    break;
            }

            writer.WriteEndObject();
        }

        private static string GetString(JsonElement element, string propName)
        {
            if(element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propName, out var prop))
            {
                return prop.GetString();
            }

            return default;
        }
    }
}
