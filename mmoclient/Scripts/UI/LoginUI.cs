using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Newtonsoft.Json;

public class LoginUI : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public Button loginButton;
    public Button registerButton;
    public TextMeshProUGUI statusText;

    [Header("Panels")]
    public GameObject loginPanel;
    public GameObject registerPanel;
    public TMP_InputField registerUsernameInput;
    public TMP_InputField registerPasswordInput;
    public TMP_InputField registerConfirmPasswordInput;
    public Button confirmRegisterButton;
    public Button backButton;

    private void Start()
    {
        ClientManager.Instance.Connect();

        loginButton.onClick.AddListener(OnLoginClick);
        registerButton.onClick.AddListener(ShowRegisterPanel);
        confirmRegisterButton.onClick.AddListener(OnRegisterClick);
        backButton.onClick.AddListener(ShowLoginPanel);

        MessageHandler.Instance.OnLoginResponse += HandleLoginResponse;
        MessageHandler.Instance.OnRegisterResponse += HandleRegisterResponse;

        ShowLoginPanel();
    }

    private void ShowLoginPanel()
    {
        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
        statusText.text = "";
    }

    private void ShowRegisterPanel()
    {
        loginPanel.SetActive(false);
        registerPanel.SetActive(true);
        statusText.text = "";
    }

    private void OnLoginClick()
    {
        string username = usernameInput.text;
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            statusText.text = "Por favor, preencha todos os campos.";
            statusText.color = Color.red;
            return;
        }

        statusText.text = "Conectando...";
        statusText.color = Color.yellow;

        var message = new
        {
            type = "login",
            username = username,
            password = password
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    private void OnRegisterClick()
    {
        string username = registerUsernameInput.text;
        string password = registerPasswordInput.text;
        string confirmPassword = registerConfirmPasswordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            statusText.text = "Por favor, preencha todos os campos.";
            statusText.color = Color.red;
            return;
        }

        if (password != confirmPassword)
        {
            statusText.text = "As senhas não coincidem.";
            statusText.color = Color.red;
            return;
        }

        statusText.text = "Criando conta...";
        statusText.color = Color.yellow;

        var message = new
        {
            type = "register",
            username = username,
            password = password
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    private void HandleLoginResponse(LoginResponseData data)
    {
        if (data.success)
        {
            statusText.text = "Login bem-sucedido!";
            statusText.color = Color.green;

            PlayerPrefs.SetInt("AccountId", data.accountId);
            
            // IMPORTANTE: Salva credenciais para recarregar personagens
            PlayerPrefs.SetString("SavedUsername", usernameInput.text);
            PlayerPrefs.SetString("SavedPassword", passwordInput.text);
            PlayerPrefs.Save();
            
            Debug.Log($"Login: Saved credentials and AccountId: {data.accountId}");

            Invoke("LoadCharacterSelect", 1f);
        }
        else
        {
            statusText.text = data.message;
            statusText.color = Color.red;
        }
    }

    private void HandleRegisterResponse(RegisterResponseData data)
    {
        if (data.success)
        {
            statusText.text = "Conta criada! Faça login.";
            statusText.color = Color.green;
            Invoke("ShowLoginPanel", 2f);
        }
        else
        {
            statusText.text = data.message;
            statusText.color = Color.red;
        }
    }

    private void LoadCharacterSelect()
    {
        SceneManager.LoadScene("CharacterSelect");
    }

    private void OnDestroy()
    {
        if (MessageHandler.Instance != null)
        {
            MessageHandler.Instance.OnLoginResponse -= HandleLoginResponse;
            MessageHandler.Instance.OnRegisterResponse -= HandleRegisterResponse;
        }
    }
}