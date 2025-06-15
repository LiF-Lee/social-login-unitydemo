using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace LogonixSDK.src.Core
{
    public class AuthCallbackListener : MonoBehaviour
    {
        [SerializeField] private string providerOrigin;
        [SerializeField] private string providerOauthPath;
        [SerializeField] private string callbackUri;
        [SerializeField] private string clientId;
        
        private static string ProviderOrigin;
        private static string ProviderOauthPath;
        private static string CallbackPrefix;
        private static string ClientID;
        private HttpListener _listener;

        public event Action<string> OnSuccess;
        public event Action<string> OnFail;

        private void Awake()
        {
            ProviderOrigin = providerOrigin;
            ProviderOauthPath = providerOauthPath;
            CallbackPrefix = callbackUri;
            ClientID = clientId;
        }

        private void OnApplicationQuit()
        {
            StopListener();
        }

        public void Login()
        {
            StartListener();
            Application.OpenURL($"{ProviderOrigin}{ProviderOauthPath}?clientId={ClientID}&callback={CallbackPrefix}");
        }
        
        public void CancelLogin()
        {
            StopListener();
        }

        private void StartListener()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(CallbackPrefix);
                _listener.Start();
                Task.Run(ProcessRequests);
            }
            catch (Exception ex)
            {
                Dispatcher.Enqueue(() =>
                {
                    OnFail?.Invoke($"Listener start error: {ex.Message}");
                });
            }
        }

        private void StopListener()
        {
            if (_listener == null) return;
            try
            {
                if (_listener.IsListening)
                {
                    _listener.Stop();
                }
                _listener.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthCallbackListener] StopListener 에러: {ex}");
            }
            finally
            {
                _listener = null;
            }
        }

        private async Task ProcessRequests()
        {
            while (_listener is { IsListening: true })
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    HandleRequest(context);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AuthCallbackListener] ProcessRequests 에러: {ex}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                AddCorsHeaders(context.Response);

                if (context.Request.HttpMethod == "OPTIONS")
                {
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.OutputStream.Close();
                    return;
                }

                Debug.Log(context.Request.HttpMethod);
                if (context.Request.HttpMethod != "POST")
                {
                    SendStatus(context.Response, HttpStatusCode.BadRequest);
                    return;
                }

                string body = ReadRequestBody(context.Request);
                var tokenReq = JsonUtility.FromJson<TokenRequest>(body);

                if (tokenReq == null || string.IsNullOrEmpty(tokenReq.status))
                {
                    SendStatus(context.Response, HttpStatusCode.BadRequest);
                    Dispatcher.Enqueue(() =>
                    {
                        OnFail?.Invoke("MISSING_TOKEN_REQUEST");
                    });
                    return;
                }

                if (tokenReq.status != "OK")
                {
                    SendStatus(context.Response, HttpStatusCode.OK);
                    Dispatcher.Enqueue(() =>
                    {
                        OnFail?.Invoke(tokenReq.status);
                    });
                }
                else if (string.IsNullOrEmpty(tokenReq.token))
                {
                    SendStatus(context.Response, HttpStatusCode.BadRequest);
                    Dispatcher.Enqueue(() =>
                    {
                        OnFail?.Invoke("MISSING_ID_TOKEN");
                    });
                }
                else
                {
                    Dispatcher.Enqueue(() =>
                    {
                        OnSuccess?.Invoke(tokenReq.token);
                    });

                    var responseJson = JsonUtility.ToJson(new CallbackResponse { success = true });
                    var buffer = Encoding.UTF8.GetBytes(responseJson);
                    context.Response.StatusCode = (int)HttpStatusCode.OK;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    context.Response.ContentLength64 = buffer.Length;
                    context.Response.OutputStream.Write(buffer, 0, buffer.Length);
                    context.Response.OutputStream.Close();
                }

                StopListener();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AuthCallbackListener] HandleRequest 에러: {ex}");
                SendStatus(context.Response, HttpStatusCode.InternalServerError);
                Dispatcher.Enqueue(() =>
                {
                    OnFail?.Invoke($"HandleRequest error: {ex.Message}");
                });
            }
        }

        private static string ReadRequestBody(HttpListenerRequest request)
        {
            using var reader = new System.IO.StreamReader(request.InputStream, request.ContentEncoding);
            return reader.ReadToEnd();
        }

        private static void AddCorsHeaders(HttpListenerResponse response)
        {
            response.AddHeader("Access-Control-Allow-Origin", ProviderOrigin);
            response.AddHeader("Access-Control-Allow-Methods", "POST, OPTIONS");
            response.AddHeader("Access-Control-Allow-Headers", "Content-Type");
        }

        private static void SendStatus(HttpListenerResponse response, HttpStatusCode code)
        {
            response.StatusCode = (int)code;
            response.OutputStream.Close();
        }

        [Serializable]
        private class TokenRequest { public string status; public string token; }

        [Serializable]
        private class CallbackResponse { public bool success; }
    }
}
