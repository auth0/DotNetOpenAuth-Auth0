namespace $rootnamespace$
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Text;
    using DotNetOpenAuth.AspNet.Clients;
    using DotNetOpenAuth.Messaging;
    using Newtonsoft.Json;

    public class Auth0Client : OAuth2Client
    {
        private const string AuthorizationEndpoint = @"https://{0}.auth0.com/authorize";
        private const string TokenEndpoint = @"https://{0}.auth0.com/oauth/token";
        private const string UserInfo = @"https://{0}.auth0.com/userinfo?access_token={1}";

        private readonly string appId;
        private readonly string appSecret;
        private readonly string tenant;
        private readonly string connection;

        public Auth0Client(string appId, string appSecret, string tenant, string connection)
            : this("Auth0", appId, appSecret, tenant, connection)
        {
        }

        public Auth0Client(string name, string appId, string appSecret, string tenant, string connection)
            : base(name)
        {
            this.appId = appId;
            this.appSecret = appSecret;
            this.tenant = tenant;
            this.connection = connection;
        }

        protected override Uri GetServiceLoginUrl(Uri returnUrl)
        {
            var builder = new UriBuilder(string.Format(AuthorizationEndpoint, this.tenant));
            builder.AppendQueryArgument("client_id", this.appId);
            builder.AppendQueryArgument("response_type", "code");
            builder.AppendQueryArgument("redirect_uri", returnUrl.AbsoluteUri);
            builder.AppendQueryArgument("connection", this.connection);

            return builder.Uri;
        }

        protected override IDictionary<string, string> GetUserData(string accessToken)
        {
            var request = WebRequest.Create(string.Format(UserInfo, this.tenant, accessToken));

            using (var response = request.GetResponse())
            {
                using (var responseStream = response.GetResponseStream())
                {
                    using (var streamReader = new StreamReader(responseStream))
                    {
                        Dictionary<string, string> values
                            = JsonConvert.DeserializeObject<Dictionary<string, string>>(streamReader.ReadToEnd());

                        // map user_id to id as DNOA needs it
                        values.Add("id", values["user_id"]);

                        return values;
                    }
                }
            }
        }

        protected override string QueryAccessToken(Uri returnUrl, string authorizationCode)
        {
            var entity = new StringBuilder()
                                .Append(string.Format("client_id={0}&", this.appId))
                                .Append(string.Format("redirect_uri={0}&", returnUrl.AbsoluteUri))
                                .Append(string.Format("client_secret={0}&", this.appSecret))
                                .Append(string.Format("code={0}&", authorizationCode))
                                .Append("grant_type=authorization_code")
                                .ToString();

            WebRequest tokenRequest = WebRequest.Create(string.Format(TokenEndpoint, this.tenant));
            tokenRequest.ContentType = "application/x-www-form-urlencoded";
            tokenRequest.ContentLength = entity.Length;
            tokenRequest.Method = "POST";

            using (Stream requestStream = tokenRequest.GetRequestStream())
            using (var writer = new StreamWriter(requestStream))
            {
                writer.Write(entity);
                writer.Flush();
            }

            HttpWebResponse tokenResponse = (HttpWebResponse)tokenRequest.GetResponse();

            if (tokenResponse.StatusCode == HttpStatusCode.OK)
            {
                using (Stream responseStream = tokenResponse.GetResponseStream())
                using (StreamReader response = new StreamReader(responseStream))
                {
                    var tokenData = JsonConvert.DeserializeObject<OAuth2AccessTokenData>(response.ReadToEnd());
                    if (tokenData != null)
                    {
                        return tokenData.AccessToken;
                    }
                }
            }

            return null;
        }
    }
}