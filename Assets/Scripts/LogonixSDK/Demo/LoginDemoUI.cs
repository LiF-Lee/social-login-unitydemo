using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using LogonixSDK.src.Core;
using TMPro;

[Serializable]
public class JwtPayload
{
    public string sub;
    public string email;
    public string name;
    public long iat;
    public string jti;
    public long nbf;
    public long exp;
    public string iss;
    public string aud;
}

public class LoginDemoUI : MonoBehaviour
{
    [SerializeField] private Button loginButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private TextMeshProUGUI loginText;

    private AuthCallbackListener _authListener;
    private bool _isAuthenticating;
    private string _originalLoginLabel;

    private void Awake()
    {
        _authListener = FindFirstObjectByType<AuthCallbackListener>();
        if (_authListener)
        {
            _authListener.OnSuccess += HandleLoginSuccess;
            _authListener.OnFail    += HandleLoginFail;

            loginButton.onClick.AddListener(OnLoginClicked);
            logoutButton.onClick.AddListener(OnLogoutClicked);
        }

        logoutButton.gameObject.SetActive(false);

        _originalLoginLabel = loginButton.GetComponentInChildren<TextMeshProUGUI>().text;
    }

    private void OnLoginClicked()
    {
        if (!_isAuthenticating)
        {
            _isAuthenticating = true;
            loginButton.interactable = true;
            
            loginText.alignment = TextAlignmentOptions.Top;
            loginText.text = "로그인 대기 중";
            loginButton.GetComponentInChildren<TextMeshProUGUI>().text = "Cancel";
            _authListener.Login();
        }
        else
        {
            _authListener.CancelLogin();
            HandleLoginFail("CANCEL");
        }
    }

    private void HandleLoginSuccess(string token)
    {
        Debug.Log($"[SocialLoginUI] 로그인 성공! Token: {token}");

        _isAuthenticating = false;
        loginButton.GetComponentInChildren<TextMeshProUGUI>().text = _originalLoginLabel;
        loginButton.gameObject.SetActive(false);
        logoutButton.gameObject.SetActive(true);

        var parts = token.Split('.');
        if (parts.Length < 2) { Debug.LogError("[SocialLoginUI] JWT 형식 오류"); return; }

        string payloadBase64 = parts[1]
            .Replace('-', '+')
            .Replace('_', '/');
        switch (payloadBase64.Length % 4)
        {
            case 2: payloadBase64 += "=="; break;
            case 3: payloadBase64 += "=";  break;
        }

        var payloadBytes = Convert.FromBase64String(payloadBase64);
        var jsonPayload  = Encoding.UTF8.GetString(payloadBytes);
        var payload      = JsonUtility.FromJson<JwtPayload>(jsonPayload);

        loginText.alignment = TextAlignmentOptions.TopLeft;
        loginText.text =
            $"ID:    {payload.sub}\n" +
            $"EMAIL: {payload.email}\n" +
            $"NAME:  {payload.name}\n" +
            $"ISS:   {payload.iss}\n" +
            $"AUD:   {payload.aud}";
    }

    private void HandleLoginFail(string reason)
    {
        loginText.alignment = TextAlignmentOptions.Top;
        loginText.text = reason == "CANCEL" ? "사용자 요청 취소" : reason;

        _isAuthenticating = false;
        loginButton.GetComponentInChildren<TextMeshProUGUI>().text = _originalLoginLabel;
        loginButton.interactable = true;
        logoutButton.gameObject.SetActive(false);
    }

    private void OnLogoutClicked()
    {
        loginText.text = "";
        loginButton.interactable = true;
        loginButton.gameObject.SetActive(true);
        logoutButton.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (!_authListener) return;
        _authListener.OnSuccess -= HandleLoginSuccess;
        _authListener.OnFail    -= HandleLoginFail;
    }
}
