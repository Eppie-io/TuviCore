using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Tuvi.Auth.Proton.Exceptions;
using Tuvi.Auth.Proton.Messages.Payloads;
using Tuvi.Core.Entities;
using Tuvi.Proton.Client;
using Tuvi.Proton.Client.Exceptions;
using Tuvi.Proton.Primitive.Exceptions;
using Tuvi.Proton.Primitive.Headers;
using Tuvi.Proton.Primitive.Messages.Errors;
using Tuvi.Proton.Primitive.Messages.Payloads;
using static Tuvi.Proton.Primitive.Messages.Payloads.CommonResponse;

namespace Tuvi.Proton.Impl
{
    #region Enums

    // TODO: failed to deserialize, flag values do not coincide with Proton's flag values
    //[Flags]
    //internal enum MessageFlag : int
    //{
    //    Received = 1 << 0,
    //    Sent = 1 << 1,
    //    Internal = 1 << 2,
    //    E2E = 1 << 3,
    //    Auto = 1 << 4,
    //    Replied = 1 << 5,
    //    RepliedAll = 1 << 6,
    //    Forwarded = 1 << 7,
    //    AutoReplied = 1 << 8,
    //    Imported = 1 << 9,
    //    Opened = 1 << 10,
    //    ReceiptSent = 1 << 11,
    //    Notified = 1 << 12,
    //    Touched = 1 << 13,
    //    Receipt = 1 << 14,
    //    ReceiptRequest = 1 << 16,
    //    PublicKey = 1 << 17,
    //    Sign = 1 << 18,
    //    Unsubscribed = 1 << 19,
    //    ScheduledSend = 1 << 20,
    //    Alias = 1 << 21,
    //    DMARCPass = 1 << 23,
    //    SPFFail = 1 << 24,
    //    DKIMFail = 1 << 25,
    //    DMARCFail = 1 << 26,
    //    HamManual = 1 << 27,
    //    SpamAuto = 1 << 28,
    //    SpamManual = 1 << 29,
    //    PhishingAuto = 1 << 30,
    //    PhishingManual = 1 << 31,
    //}

    internal enum RecipientType : int
    {
        Unknown = 0,
        Internal = 1,
        External = 2
    }

    internal enum LabelType
    {
        Label = 1,
        ContactGroup = 2,
        Folder = 3,
        System = 4
    }

    internal enum CreateDraftAction : int
    {
        ReplyAction,
        ReplyAllAction,
        ForwardAction,
        AutoResponseAction,
        ReadReceiptAction
    }

    [Flags]
    internal enum EncryptionScheme : int
    {
        InternalScheme = 1 << 0,
        EncryptedOutsideScheme = 1 << 1,
        ClearScheme = 1 << 2,
        PGPInlineScheme = 1 << 3,
        PGPMIMEScheme = 1 << 4,
        ClearMIMEScheme = 1 << 5,
    }

    internal enum SignatureType : int
    {
        NoSignature = 0,
        DetachedSignature = 1,
        AttachedSignature = 2
    }

    [Flags]
    internal enum KeyState : int
    {
        Trusted = 1 << 0,
        Active = 1 << 1,
    }

    internal enum Code
    {
        Success = 1000,
        Multi = 1001,
        InvalidValue = 2001,
        AppVersionMissing = 5001,
        AppVersionBad = 5003,
        UsernameInvalid = 6003, // Deprecated, but still used.
        PasswordWrong = 8002,
        HumanVerificationRequired = 9001,
        PaidPlanRequired = 10004,
        AuthRefreshTokenInvalid = 10013
    }

    #endregion

    #region Data

    internal struct APIError
    {
        public int Status { get; set; }
        public Code Code { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Message { get; set; }
    }

    internal struct UndoToken
    {
        public string Token { get; set; }
        public long ValidUntil { get; set; }
    }

    internal struct Label
    {
        public string ID { get; set; }
        public string ParentID { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }
        // public IList<string> Path { get; set; } // TODO: failed to deserialize, it is a string with slashes
        public string Color { get; set; }
        public LabelType Type { get; set; }
    }

    internal class MessageMetadata
    {
        public string ID { get; set; }
        public string AddressID { get; set; }
        public IReadOnlyList<string> LabelIDs { get; set; }
        public string ExternalID { get; set; }
        public string Subject { get; set; }
        public string SenderAddress { get; set; }
        public string SenderName { get; set; }
        public IReadOnlyList<EmailAddress> ToList { get; set; }
        public IReadOnlyList<EmailAddress> CCList { get; set; }
        public IReadOnlyList<EmailAddress> BCCList { get; set; }
        public IReadOnlyList<EmailAddress> ReplyTos { get; set; }
        //public MessageFlag Flags { get; set; }// TODO: failed to deserialize, flag values do not coincide with Proton's flag values
        public long Time { get; set; }
        public int Size { get; set; }
        public int Unread { get; set; }
        public int IsReplied { get; set; }
        public int IsRepliedAll { get; set; }
        public int IsForwarded { get; set; }
        public int NumAttachments { get; set; }
    }

    internal class Message : MessageMetadata
    {
        public string Header { get; set; }
        //public IDictionary<string, IList<string>> ParsedHeaders { get; set; }
        public string Body { get; set; }
        public string MIMEType { get; set; }
        public IList<Attachment> Attachments { get; set; }
    }

    internal class Attachment
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public Int64 Size { get; set; }
        public string MIMEType { get; set; }
        public string Disposition { get; set; }
        public IDictionary<string, string> Headers { get; set; }
        public string KeyPackets { get; set; }
        public string Signature { get; set; }
    }

    internal static class MIMETypes
    {
        public const string TextHtml = "text/html";
        public const string TextPlain = "text/plain";
        public const string MultipartMixed = "multipart/mixed";
        public const string MultipartRelated = "multipart/related";
        public const string MessageRFC822 = "message/rfc822";
        public const string AppOctetStream = "application/octet-stream";
        public const string AppJson = "application/json";
    }

    internal static class Dispositions
    {
        public const string Inline = "inline";
        public const string Attachment = "attachment";
    }

    internal static class LabelID
    {
        public const string InboxLabel = "0";
        public const string AllDraftsLabel = "1";
        public const string AllSentLabel = "2";
        public const string TrashLabel = "3";
        public const string SpamLabel = "4";
        public const string AllMailLabel = "5";
        public const string ArchiveLabel = "6";
        public const string SentLabel = "7";
        public const string DraftsLabel = "8";
        public const string OutboxLabel = "9";
        public const string StarredLabel = "10";
        public const string AllScheduledLabel = "12";
    }

    internal struct Key
    {
        public string ID { get; set; }
        public string PrivateKey { get; set; }
        public string Token { get; set; }
        public string Signature { get; set; }
        public int Primary { get; set; }
        public int Active { get; set; }
        public int Flags { get; set; }
    }

    internal struct User
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public IList<Key> Keys { get; set; }
    }

    internal struct Salt
    {
        public string ID { get; set; }
        public string KeySalt { get; set; }
    }

    internal struct Address
    {
        public string ID { get; set; }
        public string Email { get; set; }
        public int Send { get; set; }
        public int Receive { get; set; }
        public int Status { get; set; }
        public int Type { get; set; }
        public int Order { get; set; }
        public string DisplayName { get; set; }
        public IList<Key> Keys { get; set; }
    }

    internal class MessageFilter
    {
        public MessageFilter()
        {

        }
        public MessageFilter(MessageFilter other)
        {
            ID = other.ID;
            Subject = other.Subject;
            AddressID = other.AddressID;
            ExternalID = other.ExternalID;
            LabelID = other.LabelID;
        }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IList<string> ID { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Subject { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string AddressID { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string ExternalID { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string LabelID { get; set; }
    }

    internal class PagedMessageFilter : MessageFilter
    {
        public PagedMessageFilter(MessageFilter filter) : base(filter) { }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public string Sort { get; set; }
        public bool Desc { get; set; }
    }

    internal class CountMessageFilter : MessageFilter
    {
        public CountMessageFilter(MessageFilter filter) : base(filter) { }
        [JsonConverter(typeof(Int32JsonConverter))]
        public int Limit { get; set; }
    }

    internal class DraftTemplate
    {
        public string Subject { get; set; }
        public EmailAddress Sender { get; set; }
        public List<EmailAddress> ToList { get; set; } = new List<EmailAddress>();
        public List<EmailAddress> CCList { get; set; } = new List<EmailAddress>();
        public List<EmailAddress> BCCList { get; set; } = new List<EmailAddress>();
        public string Body { get; set; }
        public string MIMEType { get; set; }
        public bool Unread { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string ExternalID { get; set; }
    }

    internal class MessageRecipient
    {
        public EncryptionScheme Type { get; set; }
        public SignatureType Signature { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string BodyKeyPacket { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IDictionary<string, string> AttachmentKeyPackets { get; set; }
    }

    internal struct SessionKey
    {
        public string Key { get; set; }
        public string Algorithm { get; set; }
    }

    internal class MessagePackage
    {
        public IDictionary<string, MessageRecipient> Addresses { get; set; }
        public string MIMEType { get; set; }
        public EncryptionScheme Type { get; set; }
        public string Body { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public SessionKey BodyKey { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IDictionary<string, SessionKey> AttachmentKeys { get; set; }
    }

    internal struct PublicKey
    {
        public KeyState Flags { get; set; }
        [JsonPropertyName("PublicKey")]
        public string PublicKeyProp { get; set; }
    }

    #endregion

    #region Requests

    internal class MessageActionReq
    {
        public List<string> IDs { get; set; } = new List<string>();
    }

    internal class CreateDraftReq
    {
        public DraftTemplate Message { get; set; }
        public List<string> AttachmentKeyPackets { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string ParentID { get; set; }
        public CreateDraftAction Action { get; set; }
    }

    internal class UpdateDraftReq
    {
        public DraftTemplate Message { get; set; }
        public IList<string> AttachmentKeyPackets { get; set; }
    }

    internal class SendDraftReq
    {
        public IList<MessagePackage> Packages { get; set; }
    }

    internal class LabelMessagesReq
    {
        public string LabelID { get; set; }
        public IList<string> IDs { get; set; }
    }

    internal class CreateAttachmentReq
    {
        public string MessageID { get; set; }
        public string Filename { get; set; }
        public string MIMEType { get; set; }
        public string Disposition { get; set; }
        public string ContentID { get; set; }
        public byte[] EncKey { get; set; }
        public byte[] EncBody { get; set; }
        public byte[] Signature { get; set; }

    }

    #endregion

    #region Responses

    internal class LabelsResponse : CommonResponse
    {
        [JsonInclude]
        public IList<Label> Labels { get; private set; }

    }

    internal class FilterContent : CommonResponse
    {
        [JsonInclude]
        public long Total { get; private set; }

        [JsonInclude]
        public IList<MessageMetadata> Messages { get; private set; }
    }

    internal class StaleFilterContent : CommonResponse
    {
        public int Stale { get; set; }

        public IList<MessageMetadata> Messages { get; set; }
    }

    internal class IDsResponse : CommonResponse
    {
        public IList<string> IDs { get; set; }
    }

    internal class MessageResponse : CommonResponse
    {
        public Message Message { get; set; }
    }

    internal class UserResponse : CommonResponse
    {
        public User User { get; set; }
    }

    internal class SaltsResponse : CommonResponse
    {
        public IList<Salt> KeySalts { get; set; }
    }

    internal class AddressesResponse : CommonResponse
    {
        public IList<Address> Addresses { get; set; }
    }

    internal class KeysResponse : CommonResponse
    {
        public IList<PublicKey> Keys { get; set; }
        public RecipientType RecipientType { get; set; }
    }

    internal class LabelMessagesRes : CommonResponse
    {
        public IList<LabelMessageRes> Responses { get; set; }
        public UndoToken UndoToken { get; set; }
    }

    internal class LabelMessageRes
    {
        public string ID { get; set; }
        public APIError Response { get; set; }
    }

    internal class AttachmentRes : CommonResponse
    {
        public Attachment Attachment { get; set; }
    }

    #endregion

    #region Helpers
    public class Int32JsonConverter : JsonConverter<Int32>
    {
        public override Int32 Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options) =>
                Int32.Parse(reader.GetString(), CultureInfo.InvariantCulture);

        public override void Write(
            Utf8JsonWriter writer,
            Int32 value,
            JsonSerializerOptions options) =>
                writer?.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }

    internal struct MultipartFieldData
    {
        public string Name { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public byte[] Data { get; set; }
    }

    internal class Request
    {
        private readonly Session _session;
        private readonly HttpClient _httpClient;
        private Dictionary<string, string> _headers = new Dictionary<string, string>();
        private NameValueCollection _queryParams = System.Web.HttpUtility.ParseQueryString(string.Empty);
        private HttpContent _content;
        private object _body;
        private readonly List<MultipartFieldData> _multiparts = new List<MultipartFieldData>();

        public Request(Session session, HttpClient client)
        {
            _session = session;
            _httpClient = client;
        }

        public void Reset()
        {
            _body = null;
            _headers.Clear();
            _queryParams.Clear();
        }

        public Request SetContent(HttpContent content)
        {
            _content = content;
            return this;
        }

        public Request SetBody<TRequest>(TRequest body)
        {
            _body = body;
            return this;
        }

        public Request SetHeader(string header, string value)
        {
            _headers[header] = value;
            return this;
        }

        public Request SetMultipartFormField(string name, string filename, string contentType, byte[] data)
        {
            _multiparts.Add(new MultipartFieldData()
            {
                Name = name,
                FileName = filename,
                ContentType = contentType,
                Data = data
            });
            return this;
        }

        public Request SetMultipartFormField(string name, string data)
        {
            SetMultipartFormField(name, "", "", Encoding.UTF8.GetBytes(data));
            return this;
        }

        public Request AddQueryParam(string key, string value)
        {
            _queryParams[key] = value;
            return this;
        }

        public Task<TResponse> PostAsync<TResponse, TBody>(string uri) where TResponse : new()
        {
            return ExecuteAsync<TResponse, TBody>(uri, HttpMethod.Post);
        }
        public Task<TResponse> PostAsync<TResponse>(string uri) where TResponse : new()
        {
            return ExecuteAsync2<TResponse>(uri, HttpMethod.Post);
        }

        public Task<TResponse> GetAsync<TResponse>(string uri) where TResponse : new()
        {
            return ExecuteAsync<TResponse>(uri, HttpMethod.Get);
        }

        public Task PutAsync<TRequest>(string uri)
        {
            return ExecuteAsync<RestClient.EmptyResponse, TRequest>(uri, HttpMethod.Put);
        }

        public Task<TResponse> PutAsync<TResponse, TBody>(string uri) where TResponse : new()
        {
            return ExecuteAsync<TResponse, TBody>(uri, HttpMethod.Put);
        }

        public Task<TResponse> GetAsync<TResponse, TBody>(string uri) where TResponse : new()
        {
            return ExecuteAsync<TResponse, TBody>(uri, HttpMethod.Get);
        }

        public Task<Stream> GetAsync(string uri)
        {
            return ExecuteAsync2(uri, HttpMethod.Get);
        }

        private Task<TResponse> ExecuteAsync<TResponse, TBody>(string uriStr, HttpMethod method) where TResponse : new()
        {
            var uri = new Uri(uriStr + (_queryParams.Count > 0 ? ("?" + _queryParams.ToString()) : ""), UriKind.Relative);
            return _session.RequestAsync<TResponse, TBody>(
                endpoint: uri,
                method: method,
                payload: (TBody)_body,
                headers: new RestClient.HeaderCollection(_headers));
        }

        private Task<TResponse> ExecuteAsync<TResponse>(string uriStr, HttpMethod method) where TResponse : new()
        {
            var uri = new Uri(uriStr + (_queryParams.Count > 0 ? ("?" + _queryParams.ToString()) : ""), UriKind.Relative);
            return _session.RequestAsync<TResponse>(
                endpoint: uri,
                method: method,
                headers: new RestClient.HeaderCollection(_headers));
        }

        private async Task<TResponse> ExecuteAsync2<TResponse>(string uriStr, HttpMethod method)
        {
            var res = await SendProtonAsync(uriStr, method).ConfigureAwait(false);
            if (res.Content.Headers.ContentType.MediaType != MIMETypes.AppJson)
            {
                throw new CoreException("Proton: unexpected content type");
            }
            return await res.Content.ReadFromJsonAsync<TResponse>().ConfigureAwait(false);
        }

        private async Task<Stream> ExecuteAsync2(string uriStr, HttpMethod method)
        {
            var res = await SendProtonAsync(uriStr, method).ConfigureAwait(false);
            if (res.Content.Headers.ContentType.MediaType != MIMETypes.AppOctetStream)
            {
                throw new CoreException("Proton: unexpected content type");
            }
            return await res.Content.ReadAsStreamAsync().ConfigureAwait(false);
        }

        private async Task<HttpResponseMessage> SendProtonAsync(string uriStr, HttpMethod method)
        {
            var uri2 = new Uri(Client.ProtonHost, uriStr + (_queryParams.Count > 0 ? ("?" + _queryParams.ToString()) : string.Empty));
            using (var request = new HttpRequestMessage(method, uri2))
            {
                var sessionData = _session.GetSessionData();
                if (!string.IsNullOrWhiteSpace(sessionData.Uid))
                {
                    request.Headers.Add(ProtonHeader.UidHeaderName, sessionData.Uid);
                }
                if (!string.IsNullOrWhiteSpace(sessionData.AccessToken))
                {
                    request.Headers.Add(ProtonHeader.AuthorizationHeaderName, $"{sessionData.TokenType} {sessionData.AccessToken}");
                }
                request.Headers.Add(ProtonHeader.AppVersionHeaderName, "Other");

                foreach (var h in _headers)
                {
                    request.Headers.Add(h.Key, h.Value);
                }

                if (_content != null)
                {
                    request.Content = _content;
                }
                else if (_multiparts.Count > 0)
                {
                    request.Content = GetMultipartFormDataContent();
                }

                return await _httpClient.SendAsync(request).ConfigureAwait(false);
            }
        }

        private HttpContent GetMultipartFormDataContent()
        {
            var content = new MultipartFormDataContent();
            foreach (var field in _multiparts)
            {
#pragma warning disable CA2000 // Dispose objects before losing scope
                if (String.IsNullOrEmpty(field.FileName))
                {
                    content.Add(GetHttpContent(field), field.Name);
                }
                else
                {
                    content.Add(GetHttpContent(field), field.Name, field.FileName);
                }
#pragma warning restore CA2000 // Dispose objects before losing scope
            }
            return content;
        }

        private static HttpContent GetHttpContent(MultipartFieldData field)
        {
            if (String.IsNullOrEmpty(field.ContentType))
            {
                return new StringContent(Encoding.UTF8.GetString(field.Data)); ;
            }
            else if (field.ContentType == MIMETypes.AppOctetStream)
            {
                var s = new StreamContent(new MemoryStream(field.Data));
                s.Headers.Add("Content-Type", field.ContentType);

                return s;
            }
            throw new CoreException("REST: unexpected part type");
        }
    }

    #endregion

    internal class Client : IDisposable
    {
        public static readonly Uri ProtonHost = new Uri("https://mail-api.proton.me");
        private const int MaxPageSize = 150;
        private readonly HttpClient _httpClient;
        private readonly Session _session;
        private readonly Func<Session, CancellationToken, Task> _refreshCallback;
        private readonly SemaphoreSlim _refreshSemaphore = new SemaphoreSlim(1);

        public static async Task<Client> CreateWithLoginAsync(Func<HttpClient> httpClientCreator,
                                                              string userName,
                                                              string password,
                                                              TwoFactorCodeProvider twoFactorProvider,
                                                              HumanVerifier humanVerifier,
                                                              Func<Session, CancellationToken, Task> refreshCallback,
                                                              CancellationToken cancellationToken)
        {
            try
            {
                var client = new Client(httpClientCreator, refreshCallback);
                await client.LoginAsync(userName, password, twoFactorProvider, humanVerifier, cancellationToken).ConfigureAwait(false);

                return client;
            }
            catch (AuthProtonException ex)
            {
                throw MakeException((int)ResponseCode.WrongPassword, "login", ex.Message, ex);
            }
        }

        private async Task LoginAsync(string userName,
                                      string password,
                                      TwoFactorCodeProvider twoFactorProvider,
                                      HumanVerifier humanVerifier,
                                      CancellationToken cancellationToken)
        {

            await VerifyCredentialsAsync().ConfigureAwait(false);

            if (_session.IsTwoFactor && _session.IsTOTP)
            {
                await ProvideTwoFactorCodeAsync().ConfigureAwait(false);
            }

            async Task VerifyCredentialsAsync(bool firstAttempt = true)
            {
                try
                {
                    await _session.LoginAsync(userName, password, cancellationToken).ConfigureAwait(false);

                    // ToDo: check if we need to reset human verification after successful login
                    // _session.ResetHumanVerification();
                }
                catch (AuthUnsuccessProtonException ex) when (ex.Response.IsHumanVerificationRequired() && humanVerifier != null)
                {
                    HumanVerificationDetails details = ex.Response.ReadDetails<HumanVerificationDetails>();

                    if (string.IsNullOrEmpty(details?.HumanVerificationToken))
                    {
                        throw;
                    }

                    await VerifyHumanAsync(details, firstAttempt ? null : ex).ConfigureAwait(false);
                }
            }

            async Task VerifyHumanAsync(HumanVerificationDetails details, Exception previousAttemptException)
            {
                (bool completed, string type, string token) = await humanVerifier(new Uri(ProtonHost, details.HumanVerificationApiUri), previousAttemptException, cancellationToken).ConfigureAwait(false);
                if (completed)
                {
                    _session.SetHumanVerification(type, token);
                    await VerifyCredentialsAsync(false).ConfigureAwait(false);
                }
                else
                {
                    throw new OperationCanceledException();
                }
            }

            async Task ProvideTwoFactorCodeAsync(Exception previousAttemptException = null)
            {
                try
                {
                    (bool completed, string code) = await twoFactorProvider(previousAttemptException, cancellationToken).ConfigureAwait(false);

                    if (completed)
                    {
                        await _session.ProvideTwoFactorCodeAsync(code, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        throw new OperationCanceledException();
                    }
                }
                catch (ProtonException ex) when (ex is AuthProtonArgumentException || ex is ProtonSessionRequestException)
                {
                    await ProvideTwoFactorCodeAsync(ex).ConfigureAwait(false);
                }
            }
        }

        public static async Task<Client> CreateFromRefreshAsync(Func<HttpClient> httpClientCreator, string uid, string refresh, Func<Session, CancellationToken, Task> refreshCallback, CancellationToken cancellationToken)
        {
            try
            {
                var client = new Client(httpClientCreator, refreshCallback);
                await client.RestoreSessionAsync(uid, refresh, cancellationToken).ConfigureAwait(false);
                return client;
            }
            catch (ProtonSessionRequestException ex)
            {
                throw MakeException(ex.ErrorInfo.Code, "refresh token", ex.ErrorInfo.Error, ex);
            }
        }

        private Client(Func<HttpClient> httpClientCreator, Func<Session, CancellationToken, Task> refreshCallback)
        {
            _httpClient = httpClientCreator();
            _refreshCallback = refreshCallback;
            _session = new Session(
                httpClient: _httpClient,
                host: ProtonHost)
            {
                AppVersion = "Other",
                RedirectUri = new Uri("https://protonmail.ch")
            };
        }

        public string RefreshToken => _session.RefreshToken;
        public string UserId => _session.UserId;
        public bool IsTwoPasswordMode => (PasswordMode)_session.PasswordMode == PasswordMode.TwoPasswordMode;

        #region Client API methods

        public Task<long> CountMessagesAsync(CancellationToken cancellationToken)
        {
            return CountMessagesImplAsync(new MessageFilter(), cancellationToken);
        }

        public async Task<IList<MessageMetadata>> GetMessageMetadataAsync(MessageFilter filter, CancellationToken cancellationToken)
        {
            var count = await CountMessagesImplAsync(filter, cancellationToken).ConfigureAwait(false);
            var tasks = new List<Task<IList<MessageMetadata>>>();
            for (long i = 0, page = 0; i < count; page++, i += MaxPageSize)
            {
                tasks.Add(GetMessageMetadataPagedAsync((int)page, MaxPageSize, filter, cancellationToken));
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return tasks.SelectMany(x => x.Result).ToList();
        }

        public async Task<IList<MessageMetadata>> GetMessageMetadataPagedAsync(int page, int pageSize, MessageFilter filter, CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            request.SetHeader("X-HTTP-Method-Override", "GET")
                   .SetBody(new PagedMessageFilter(filter)
                   {
                       Page = page,
                       PageSize = pageSize,
                       Desc = false,
                       Sort = "ID"
                   });
            while (true)
            {
                var response = await request.PostAsync<StaleFilterContent, PagedMessageFilter>("/mail/v4/messages").ConfigureAwait(false);
                CheckResponse(response, "fetch messages metadata");
                if (response.Stale == 0)
                {
                    return response.Messages;
                }
            }
        }

        public async Task<IList<string>> GetMessageIDsAsync(string afterId, CancellationToken cancellationToken)
        {
            // returns messages from older to newer
            var messageIds = new List<string>();
            for (; ; afterId = messageIds[messageIds.Count - 1])
            {
                var page = await GetMessageIDsImplAsync(afterId, cancellationToken).ConfigureAwait(false);
                if (page.Count == 0)
                {
                    return messageIds;
                }
                messageIds.AddRange(page);
            }
        }

        public async Task<Message> GetMessageAsync(string messageId, CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var response = await request.GetAsync<MessageResponse>("/mail/v4/messages/" + messageId)
                                        .ConfigureAwait(false);
            CheckResponse(response, "get message by ID");
            return response.Message;
        }

        public async Task<Stream> GetAttachmentAsync(string attachmentId, CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var response = await request.GetAsync("/mail/v4/attachments/" + attachmentId)
                                        .ConfigureAwait(false);
            return response;
        }

        public async Task<Attachment> UploadAttachmentAsync(CreateAttachmentReq req, CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var response = await request.SetMultipartFormField("MessageID", req.MessageID)
                                        .SetMultipartFormField("Filename", req.Filename)
                                        .SetMultipartFormField("MIMEType", req.MIMEType)
                                        .SetMultipartFormField("Disposition", req.Disposition)
                                        .SetMultipartFormField("ContentID", req.ContentID)
                                        .SetMultipartFormField("KeyPackets", "blob", MIMETypes.AppOctetStream, req.EncKey)
                                        .SetMultipartFormField("DataPacket", "blob", MIMETypes.AppOctetStream, req.EncBody)
                                        .SetMultipartFormField("Signature", "blob", MIMETypes.AppOctetStream, req.Signature)
                                        .PostAsync<AttachmentRes>("/mail/v4/attachments")
                                        .ConfigureAwait(false);
            CheckResponse(response, "upload attachment");
            return response.Attachment;
        }

        public async Task<Message> CreateDraftAsync(CreateDraftReq req, CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var response = await request.SetBody(req)
                                        .PostAsync<MessageResponse, CreateDraftReq>("/mail/v4/messages")
                                        .ConfigureAwait(false);
            CheckResponse(response, "create draft");
            return response.Message;
        }

        public async Task<Message> UpdateDraftAsync(string draftId, UpdateDraftReq req, CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var response = await request.SetBody(req)
                                        .PutAsync<MessageResponse, UpdateDraftReq>("/mail/v4/messages/" + draftId)
                                        .ConfigureAwait(false);
            CheckResponse(response, "update draft");
            return response.Message;
        }

        public async Task<Message> SendDraftAsync(string draftId, SendDraftReq req, CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var response = await request.SetBody(req)
                                        .PostAsync<MessageResponse, SendDraftReq>("/mail/v4/messages/" + draftId)
                                        .ConfigureAwait(false);
            CheckResponse(response, "send draft");
            return response.Message;
        }

        public async Task<IList<Label>> GetLabelsAsync(IEnumerable<LabelType> labelTypes, CancellationToken cancellationToken)
        {
            var labels = new List<Label>();
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            foreach (var label in labelTypes)
            {
                var response = await request.AddQueryParam("Type", ((int)label).ToString(CultureInfo.InvariantCulture))
                                            .GetAsync<LabelsResponse>("/core/v4/labels").ConfigureAwait(false);
                if (response.Success)
                {
                    labels.AddRange(response.Labels);
                }
            }
            return labels;
        }

        public async Task<(IList<PublicKey>, RecipientType)> GetPublicKeysAsync(string address, CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var response = await request.AddQueryParam("Email", address)
                                        .GetAsync<KeysResponse>("/core/v4/keys")
                                        .ConfigureAwait(false);
            CheckResponse(response, "get address pubic keys");
            return (response.Keys, response.RecipientType);
        }

        public async Task<IEnumerable<Salt>> GetSaltsAsync(CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var response = await request.GetAsync<SaltsResponse>("/core/v4/keys/salts")
                                        .ConfigureAwait(false);
            CheckResponse(response, "get salts");
            return response.KeySalts;
        }

        public async Task<User> GetUserAsync(CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var response = await request.GetAsync<UserResponse>("/core/v4/users")
                                        .ConfigureAwait(false);
            CheckResponse(response, "get user");
            return response.User;
        }

        public Task MarkMessagesAsReadAsync(IEnumerable<string> messageIDs, CancellationToken cancellationToken)
        {
            return ProcessMessagesAsync(messageIDs, "/mail/v4/messages/read");
        }

        public Task MarkMessagesAsUnreadAsync(IEnumerable<string> messageIDs, CancellationToken cancellationToken)
        {
            return ProcessMessagesAsync(messageIDs, "/mail/v4/messages/unread");
        }

        public Task DeleteMessagesAsync(IEnumerable<string> messageIDs, CancellationToken cancellationToken)
        {
            return ProcessMessagesAsync(messageIDs, "/mail/v4/messages/delete");
        }

        public async Task<IEnumerable<Address>> GetAddressesAsync(CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var response = await request.GetAsync<AddressesResponse>("/core/v4/addresses")
                                        .ConfigureAwait(false);
            CheckResponse(response, "get addresses");
            return response.Addresses.OrderBy(x => x.Order).ToList();
        }

        public async Task LabelMessagesAsync(IEnumerable<string> messagesIDs, string labelId, CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var req = new LabelMessagesReq()
            {
                IDs = messagesIDs.ToList(),
                LabelID = labelId
            };
            var response = await request.SetBody(req)
                                        .PutAsync<LabelMessagesRes, LabelMessagesReq>("/mail/v4/messages/label")
                                        .ConfigureAwait(false);
            CheckResponse(response, "label messages");
            return;
        }

        public async Task UnlabelMessagesAsync(IEnumerable<string> messagesIDs, string labelId, CancellationToken cancellationToken)
        {
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var req = new LabelMessagesReq()
            {
                IDs = messagesIDs.ToList(),
                LabelID = labelId
            };
            var response = await request.SetBody(req)
                                        .PutAsync<LabelMessagesRes, LabelMessagesReq>("/mail/v4/messages/unlabel")
                                        .ConfigureAwait(false);
            CheckResponse(response, "unlabel messages");
            return;
        }

        #endregion

        #region Helpers

        private static void CheckResponse(CommonResponse response, string context)
        {
            if (!response.Success)
            {
                throw MakeException(response.Code, context, response.Error, null);
            }
        }

        private static Exception MakeException(int code, string context, string message, Exception innerException)
        {
            string errorMessage = $"Proton: failed to {context}. Reason: {message}";
            if (ResponseCode.Unauthorized.SameAs(code) ||
                ResponseCode.RefreshTokenInvalid.SameAs(code) ||
                ResponseCode.WrongPassword.SameAs(code))
            {
                // there is no mistake here 401 has name "unauthorized", but means "not authenticated"
                throw new AuthenticationException(errorMessage, innerException);
            }
            if (ResponseCode.Unlock.SameAs(code))
            {
                throw new AuthorizationException(errorMessage, innerException);
            }
            throw new CoreException(errorMessage, innerException);
        }

        private async Task<IList<string>> GetMessageIDsImplAsync(string afterId, CancellationToken cancellationToken)
        {
            const string MaxMessageIDs = "1000";
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            if (!String.IsNullOrEmpty(afterId))
            {
                request.AddQueryParam("AfterID", afterId);
            }
            var response = await request.AddQueryParam("Limit", MaxMessageIDs)
                                        .GetAsync<IDsResponse>("/mail/v4/messages/ids")
                                        .ConfigureAwait(false);
            CheckResponse(response, "get message IDs");
            return response.IDs;
        }

        private async Task<long> CountMessagesImplAsync(MessageFilter filter, CancellationToken cancellationToken)
        {
            // TODO: should we do the same way as proton api does? This is ineffective way to get message count 
            var request = await CreateHttpRequestAsync(cancellationToken).ConfigureAwait(false);
            var countFilter = new CountMessageFilter(filter)
            {
                Limit = 0
            };
            var response = await request.SetHeader("X-HTTP-Method-Override", "GET")
                                        .SetBody(countFilter)
                                        .PostAsync<FilterContent, CountMessageFilter>("/mail/v4/messages").ConfigureAwait(false);
            CheckResponse(response, "count messages");
            return response.Total;
        }

        private async Task ProcessMessagesAsync(IEnumerable<string> messageIDs, string endpoint)
        {
            var req = new MessageActionReq();
            req.IDs.AddRange(messageIDs);
            var request = await CreateHttpRequestAsync(CancellationToken.None).ConfigureAwait(false);
            await request.SetBody(req)
                         .PutAsync<MessageActionReq>(endpoint)
                         .ConfigureAwait(false);
        }

        private async Task<Request> CreateHttpRequestAsync(CancellationToken cancellationToken)
        {
            await TryRefreshSessionAsync(cancellationToken).ConfigureAwait(false);

            return new Request(_session, _httpClient);
        }

        private async Task<bool> TryRefreshSessionAsync(CancellationToken cancellationToken)
        {
            const int reserveSeconds = 60;
            if (!_session.IsExpired(reserveSeconds))
            {
                return false;
            }

            await _refreshSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!_session.IsExpired(reserveSeconds))
                {
                    return false;
                }

                await _session.RefreshAsync(cancellationToken).ConfigureAwait(false);

                if (_refreshCallback != null)
                {
                    await _refreshCallback(_session, cancellationToken).ConfigureAwait(false);
                }

                return true;
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        private async Task RestoreSessionAsync(string uid, string refreshToken, CancellationToken cancellationToken)
        {
            await _refreshSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _session.RestoreAsync(uid, refreshToken, cancellationToken).ConfigureAwait(false);

                if (_refreshCallback != null)
                {
                    await _refreshCallback(_session, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _refreshSemaphore.Release();
            }
        }

        public void Dispose()
        {
            _httpClient.Dispose();
            _refreshSemaphore.Dispose();
        }
        #endregion
    }
}
