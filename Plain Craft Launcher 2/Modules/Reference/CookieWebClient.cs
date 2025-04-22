using System;
using System.Collections.Generic;
using System.Net;

namespace PCL
{

    public class CookieWebClient : WebClient
    {

        public CookieWebClient(CookieContainer container, Dictionary<string, string> Headers) : this(container)
        {
            foreach (var keyVal in Headers)
                this.Headers[keyVal.Key] = keyVal.Value;
        }

        public CookieWebClient() : this(new CookieContainer())
        {
        }

        public CookieWebClient(CookieContainer container)
        {
            this.container = container;
        }

        private new readonly CookieContainer container = new CookieContainer();

        /// <summary>
    /// 以毫秒为单位的超时。
    /// </summary>
        public int Timeout = 600000;

        protected override WebRequest GetWebRequest(Uri address)
        {
            var r = base.GetWebRequest(address);
            HttpWebRequest request = r as HttpWebRequest;

            if (request is not null)
            {
                request.CookieContainer = container;
                request.Timeout = Timeout;
            }

            return r;
        }

        protected override WebResponse GetWebResponse(WebRequest request, IAsyncResult result)
        {
            var response = base.GetWebResponse(request, result);
            ReadCookies(response);
            return response;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            var response = base.GetWebResponse(request);
            ReadCookies(response);
            return response;
        }

        private void ReadCookies(WebResponse r)
        {
            HttpWebResponse response = r as HttpWebResponse;
            if (response is not null)
            {
                var cookies = response.Cookies;
                container.Add(cookies);
            }
        }
    }
}