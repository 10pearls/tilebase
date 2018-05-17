using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

namespace CustomRegionPOC.Common.Helper
{
    public class WebProxy
    {
        private static int exceptionConsecutiveCount = 0;

        private static int totalExceptionCount = 0;

        private bool useDirectConnection = false;

        private bool useProxy = false;

        public WebProxy(bool useDirectConnection, bool useProxy)
        {
            this.useDirectConnection = useDirectConnection;
            this.useProxy = useProxy;
        }

        public WebProxy()
        {
        }

        public static int ExceptionConsecutiveCount
        {
            get
            {
                return exceptionConsecutiveCount;
            }

            set
            {
                exceptionConsecutiveCount = value;
            }
        }

        public static int TotalExceptionCount
        {
            get
            {
                return totalExceptionCount;
            }

            set
            {
                totalExceptionCount = value;
            }
        }

        public R GetRequest<R>(string url, Dictionary<string, string> parameters, bool isJson = true)
        {
            return this.MakeRequest<R>(url, "GET", null, parameters, isJson);
        }

        public HttpWebResponse PostRequest(string url, object entity, Dictionary<string, string> parameters, bool isJson = true)
        {
            return this.MakeRequest(url, "POST", entity, parameters, isJson);
        }

        public HttpWebResponse PutRequest(string url, object entity, Dictionary<string, string> parameters, bool isJson = true)
        {
            return this.MakeRequest(url, "PUT", entity, parameters, isJson);
        }

        public HttpWebResponse GetRequest(string url, Dictionary<string, string> parameters, bool isJson = true)
        {
            return this.MakeRequest(url, "GET", null, parameters, isJson);
        }

        public R MakeRequest<R>(string url, string method, object request, Dictionary<string, string> parameters, bool isJson = true)
        {
            HttpWebResponse resp = this.MakeRequest(url, method, request, parameters, isJson);
            if (resp != null)
            {
                using (Stream respStream = resp.GetResponseStream())
                {
                    if (respStream != null)
                    {
                        if (isJson)
                        {
                            R obj = JSONHelper.GetObject<R>(respStream);
                            return obj;
                        }
                        else
                        {
                            StreamReader reader = new StreamReader(respStream);
                            string text = reader.ReadToEnd();
                            dynamic obj = text;
                            return (R)obj;
                        }
                    }
                }
            }

            return default(R);
        }

        public R MakeRequestStr<R>(string url, string method, string request, Dictionary<string, string> parameters, bool isJson = true)
        {
            HttpWebResponse resp = this.MakeRequestStr(url, method, request, parameters, isJson);
            if (resp != null)
            {
                using (Stream respStream = resp.GetResponseStream())
                {
                    if (respStream != null)
                    {
                        R obj = JSONHelper.GetObject<R>(respStream);
                        return obj;
                    }
                }
            }

            return default(R);
        }

        private HttpWebResponse MakeRequest(string url, string method, object request, Dictionary<string, string> parameters, bool isJson = true)
        {
            string json = string.Empty;
            if (parameters != null && parameters.Count > 0)
            {
                int i = 0;
                foreach (string key in parameters.Keys)
                {
                    if (i == 0)
                    {
                        url += string.Format("?{0}={1}", key, parameters[key]);
                    }
                    else
                    {
                        url += string.Format("&{0}={1}", key, parameters[key]);
                    }

                    i++;
                }
            }

            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(url);
            if (this.useDirectConnection)
            {
                req.Proxy = new System.Net.WebProxy();
            }
            else
            {
            }

            if (isJson)
            {
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                req.Accept = "application/json";
                req.ContentType = "application/json";
            }
            else
            {
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                req.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
                req.ContentType = "application/x-www-form-urlencoded";
            }

            req.Method = method;
            req.Timeout = 300000;
            HttpWebResponse resp;
            if (request != null)
            {
                try
                {
                    if (isJson)
                    {
                        json = JSONHelper.GetString(request);
                        byte[] jsonReq = Encoding.UTF8.GetBytes(json);
                        req.GetRequestStream().Write(jsonReq, 0, jsonReq.Length);
                        req.GetRequestStream().Close();
                        req.GetRequestStream().Dispose();
                    }
                    else
                    {
                        if (request is Dictionary<string, string>)
                        {
                            NameValueCollection outgoingQueryString = HttpUtility.ParseQueryString(string.Empty);
                            var reqObj = (Dictionary<string, string>)request;

                            foreach (var key in reqObj.Keys)
                            {
                                outgoingQueryString.Add(key, reqObj[key]);
                            }

                            string postdata = outgoingQueryString.ToString();

                            byte[] jsonReq = Encoding.UTF8.GetBytes(postdata);
                            req.GetRequestStream().Write(jsonReq, 0, jsonReq.Length);
                            req.GetRequestStream().Close();
                            req.GetRequestStream().Dispose();
                        }
                    }
                }
                catch
                {
                    resp = null;
                }
            }

            try
            {
                resp = (HttpWebResponse)req.GetResponse();
                ExceptionConsecutiveCount = 0;
            }
            catch (WebException ex)
            {
                HttpWebResponse myResponse = ex.Response as HttpWebResponse;
                string response = string.Empty;

                if (myResponse != null)
                {
                    StreamReader strm = new StreamReader(myResponse.GetResponseStream(), Encoding.UTF8);
                    response = strm.ReadToEnd();
                    string path = AppDomain.CurrentDomain.BaseDirectory + "\\Exceptions\\";
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }

                    using (TextWriter tw = new StreamWriter(path + DateTime.Now.ToString().Replace("/", "_").Replace(":", "_") + ".txt"))
                    {
                        tw.WriteLine(response);
                        tw.WriteLine("Exception json: " + json + "\n");
                    }
                }

                throw ex;
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return resp;
        }

        private HttpWebResponse MakeRequestStr(string url, string method, string request, Dictionary<string, string> parameters, bool isJson = true)
        {
            string json = string.Empty;
            if (parameters != null && parameters.Count > 0)
            {
                int i = 0;
                foreach (string key in parameters.Keys)
                {
                    if (i == 0)
                    {
                        url += string.Format("?{0}={1}", key, parameters[key]);
                    }
                    else
                    {
                        url += string.Format("&{0}={1}", key, parameters[key]);
                    }

                    i++;
                }
            }

            HttpWebRequest req = (HttpWebRequest)HttpWebRequest.Create(url);
            if (this.useDirectConnection)
            {
                req.Proxy = new System.Net.WebProxy();
            }
            else
            {
            }

            if (isJson)
            {
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                req.Accept = "application/json";
                req.ContentType = "text/json";
            }

            req.Method = method;
            req.Timeout = 300000;
            HttpWebResponse resp;
            if (request != null)
            {
                try
                {
                    json = request;
                    byte[] jsonReq = Encoding.UTF8.GetBytes(json);
                    req.GetRequestStream().Write(jsonReq, 0, jsonReq.Length);
                    req.GetRequestStream().Close();
                    req.GetRequestStream().Dispose();
                }
                catch
                {
                    resp = null;
                }
            }

            try
            {
                resp = (HttpWebResponse)req.GetResponse();
                ExceptionConsecutiveCount = 0;
            }
            catch (Exception ex)
            {
                string path = AppDomain.CurrentDomain.BaseDirectory + "\\Exceptions\\";
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }

                using (TextWriter tw = new StreamWriter(path + DateTime.Now.ToString().Replace("/", "_").Replace(":", "_") + ".txt"))
                {
                    tw.WriteLine("Exception Message: " + ex.Message + "\n");
                    tw.WriteLine("Exception json: " + json + "\n");
                }

                throw ex;
            }

            return resp;
        }
    }
}