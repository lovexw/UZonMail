﻿using Microsoft.AspNetCore.Authorization;
using UZonMail.Utils.Http.Request;
using UZonMail.Utils.Web.Exceptions;
using UZonMail.Utils.Web.Service;

namespace UZonMail.Core.Controllers.Emails.Requests
{
    /// <summary>
    /// 参考: https://learn.microsoft.com/en-us/graph/sdks/choose-authentication-providers?tabs=csharp#client-credentials-provider
    /// 个人邮箱限制
    /// 不支持应用程序权限（Application Permissions）
    /// 不支持客户端凭据流（Client Credentials Flow）
    /// 只支持委托权限（Delegated Permissions
    /// </summary>
    public class OutlookAuthorizationRequest : FluentHttpRequest, ITransientService
    {
        public static readonly List<string> SendScopes = [
                // 可以获取刷新令牌    
                "openid",
                "offline_access",
                // 发送邮箱需要
                "Mail.Send", //  替代 SMTP 发件权限
                //"Mail.Send.Shared",
                //"Mail.Read"  // 替代 IMAP 收件权限
            ];

        public OutlookAuthorizationRequest(HttpClient httpClient, IConfiguration configuration)
        {
            var baseUrl = configuration.GetValue<string>("BaseUrl");
            if (string.IsNullOrEmpty(baseUrl))
            {
                throw new KnownException("BaseUrl 配置不能为空，请检查配置文件。");
            }

            WithHttpClient(httpClient);
            WithUrl("https://login.microsoftonline.com/common/oauth2/v2.0/authorize");
            AddQuery("redirect_uri", $"{baseUrl.Trim('/')}/api/v1/outlook-authorization/code");
            AddQuery("response_type", "code");
            AddQuery("response_mode", "query");
            AddQuery("scope", string.Join(" ", SendScopes));
            AddQuery("prompt", "login");
        }

        public OutlookAuthorizationRequest WithClientId(string clientId)
        {
            AddQuery("client_id", clientId);
            return this;
        }

        /// <summary>
        /// 通过 state 进行回调
        /// </summary>
        /// <param name="outboxObjectId"></param>
        /// <returns></returns>
        public OutlookAuthorizationRequest WithState(string outboxObjectId)
        {
            AddQuery("state", outboxObjectId);
            return this;
        }

        public OutlookAuthorizationRequest WithEmail(string email)
        {
            AddQuery("login_hint", email);
            return this;
        }
    }
}
