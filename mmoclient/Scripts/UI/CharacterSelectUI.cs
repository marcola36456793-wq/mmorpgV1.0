using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Newtonsoft.Json;
using System.Collections.Generic;

public class CharacterSelectUI : MonoBehaviour
{
    [Header("Character List")]
    public Transform characterListContainer;
    public GameObject characterButtonPrefab;

    [Header("Create Character Panel")]
    public GameObject createCharacterPanel;
    public TMP_InputField characterNameInput;
    public TMP_Dropdown raceDropdown;
    public TMP_Dropdown classDropdown;
    public Button createButton;
    public Button cancelButton;

    [Header("Select Panel")]
    public GameObject selectPanel;
    public Button createNewCharacterButton;
    public TextMeshProUGUI statusText;

    private int accountId;
    private List<CharacterData> characters = new List<CharacterData>();
    private bool charactersLoaded = false;

    private void Start()
    {
        accountId = PlayerPrefs.GetInt("AccountId", 0);

        if (accountId == 0)
        {
            Debug.LogError("CharacterSelect: No AccountId! Returning to Login...");
            SceneManager.LoadScene("Login");
            return;
        }

        Debug.Log($"=== CharacterSelect Started - AccountId: {accountId} ===");

        // Configura botões
        createNewCharacterButton.onClick.AddListener(ShowCreatePanel);
        createButton.onClick.AddListener(OnCreateCharacter);
        cancelButton.onClick.AddListener(ShowSelectPanel);

        // Registra eventos
        MessageHandler.Instance.OnLoginResponse += LoadCharacters;
        MessageHandler.Instance.OnCreateCharacterResponse += HandleCreateCharacterResponse;
        MessageHandler.Instance.OnSelectCharacterResponse += HandleSelectCharacterResponse;

        ShowSelectPanel();
        
        // Solicita lista de personagens
        Invoke("RequestCharacterList", 0.5f); // Pequeno delay para garantir que MessageHandler está pronto
    }

    private void RequestCharacterList()
    {
        Debug.Log("CharacterSelect: Requesting character list...");
        
        string username = PlayerPrefs.GetString("SavedUsername", "");
        string password = PlayerPrefs.GetString("SavedPassword", "");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            Debug.LogWarning("CharacterSelect: No saved credentials found!");
            statusText.text = "Nenhum personagem encontrado. Crie um novo!";
            statusText.color = Color.white;
            return;
        }

        Debug.Log($"CharacterSelect: Sending login request for user: {username}");

        var message = new
        {
            type = "login",
            username = username,
            password = password
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
        
        statusText.text = "Carregando personagens...";
        statusText.color = Color.yellow;
    }

    private void LoadCharacters(LoginResponseData data)
    {
        Debug.Log($"=== CharacterSelect: LoadCharacters Called ===");
        Debug.Log($"Success: {data.success}");
        Debug.Log($"Characters count: {data.characters?.Length ?? 0}");

        if (!data.success)
        {
            Debug.LogError($"CharacterSelect: Login failed - {data.message}");
            statusText.text = "Erro ao carregar personagens";
            statusText.color = Color.red;
            return;
        }

        // Limpa lista anterior
        characters.Clear();
        foreach (Transform child in characterListContainer)
        {
            Destroy(child.gameObject);
        }

        if (data.characters != null && data.characters.Length > 0)
        {
            Debug.Log($"CharacterSelect: Loading {data.characters.Length} characters...");

            foreach (var character in data.characters)
            {
                characters.Add(character);
                CreateCharacterButton(character);
                Debug.Log($"  - {character.nome} (ID: {character.id}, {character.raca} {character.classe})");
            }
            
            charactersLoaded = true;
            statusText.text = $"{data.characters.Length} personagem(ns) encontrado(s)";
            statusText.color = Color.green;

            Debug.Log($"CharacterSelect: ✅ {data.characters.Length} characters loaded successfully!");
        }
        else
        {
            Debug.Log("CharacterSelect: No characters found for this account");
            statusText.text = "Nenhum personagem. Crie um novo!";
            statusText.color = Color.white;
        }
    }

    private void CreateCharacterButton(CharacterData character)
    {
        if (characterButtonPrefab == null)
        {
            Debug.LogError("CharacterSelect: characterButtonPrefab is NULL! Assign it in Inspector!");
            return;
        }

        if (characterListContainer == null)
        {
            Debug.LogError("CharacterSelect: characterListContainer is NULL! Assign it in Inspector!");
            return;
        }

        GameObject buttonObj = Instantiate(characterButtonPrefab, characterListContainer);
        buttonObj.name = $"CharButton_{character.nome}";

        TextMeshProUGUI buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = $"{character.nome}\n{character.raca} - {character.classe}\nNível {character.level}";
        }
        else
        {
            Debug.LogWarning("CharacterSelect: Button prefab doesn't have TextMeshProUGUI child!");
        }

        Button button = buttonObj.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => OnSelectCharacter(character));
        }
        else
        {
            Debug.LogError("CharacterSelect: Button prefab doesn't have Button component!");
        }

        Debug.Log($"CharacterSelect: Created button for '{character.nome}'");
    }

    private void ShowCreatePanel()
    {
        Debug.Log("CharacterSelect: Showing create panel");
        selectPanel.SetActive(false);
        createCharacterPanel.SetActive(true);
        statusText.text = "";
        characterNameInput.text = "";
    }

    private void ShowSelectPanel()
    {
        Debug.Log("CharacterSelect: Showing select panel");
        selectPanel.SetActive(true);
        createCharacterPanel.SetActive(false);
        
        if (charactersLoaded && characters.Count > 0)
        {
            statusText.text = $"{characters.Count} personagem(ns) disponível(is)";
            statusText.color = Color.green;
        }
        else
        {
            statusText.text = "";
        }
    }

    private void OnCreateCharacter()
    {
        string name = characterNameInput.text.Trim();
        string race = raceDropdown.options[raceDropdown.value].text;
        string characterClass = classDropdown.options[classDropdown.value].text;

        if (string.IsNullOrEmpty(name))
        {
            statusText.text = "Digite um nome para o personagem!";
            statusText.color = Color.red;
            return;
        }

        if (name.Length < 3)
        {
            statusText.text = "Nome deve ter pelo menos 3 caracteres";
            statusText.color = Color.red;
            return;
        }

        Debug.Log($"CharacterSelect: Creating character - Name: {name}, Race: {race}, Class: {characterClass}");

        statusText.text = "Criando personagem...";
        statusText.color = Color.yellow;

        var message = new
        {
            type = "createCharacter",
            accountId = accountId,
            nome = name,
            raca = race,
            classe = characterClass
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    private void HandleCreateCharacterResponse(CreateCharacterResponseData data)
    {
        Debug.Log($"=== CharacterSelect: CreateCharacterResponse ===");
        Debug.Log($"Success: {data.success}");

        if (data.success && data.character != null)
        {
            statusText.text = $"Personagem '{data.character.nome}' criado!";
            statusText.color = Color.green;

            Debug.Log($"CharacterSelect: Character created - {data.character.nome} (ID: {data.character.id})");

            // Adiciona à lista
            characters.Add(data.character);
            CreateCharacterButton(data.character);

            characterNameInput.text = "";
            Invoke("ShowSelectPanel", 1.5f);
        }
        else
        {
            statusText.text = data.message ?? "Erro ao criar personagem";
            statusText.color = Color.red;
            Debug.LogError($"CharacterSelect: Create failed - {data.message}");
        }
    }

    private void OnSelectCharacter(CharacterData character)
    {
        Debug.Log($"=== CharacterSelect: Selecting character '{character.nome}' (ID: {character.id}) ===");

        statusText.text = $"Entrando no mundo com {character.nome}...";
        statusText.color = Color.yellow;

        var message = new
        {
            type = "selectCharacter",
            characterId = character.id
        };

        string json = JsonConvert.SerializeObject(message);
        ClientManager.Instance.SendMessage(json);
    }

    private void HandleSelectCharacterResponse(SelectCharacterResponseData data)
    {
        Debug.Log($"=== CharacterSelect: SelectCharacterResponse ===");
        Debug.Log($"Success: {data.success}");

        if (data.success && data.character != null)
        {
            statusText.text = $"Entrando no mundo com {data.character.nome}...";
            statusText.color = Color.green;

            Debug.Log($"CharacterSelect: Selection successful - {data.character.nome}");
            Debug.Log($"CharacterSelect: PlayerId: {data.playerId}");

            // Salva dados
            PlayerPrefs.SetInt("SelectedCharacterId", data.character.id);
            PlayerPrefs.SetString("SelectedCharacterName", data.character.nome);
            
            string jsonData = JsonUtility.ToJson(data);
            PlayerPrefs.SetString("PendingCharacterData", jsonData);
            PlayerPrefs.Save();
            
            Debug.Log("CharacterSelect: Data saved, loading World...");

            Invoke("LoadWorld", 0.5f);
        }
        else
        {
            statusText.text = data.message ?? "Erro ao selecionar personagem";
            statusText.color = Color.red;
            Debug.LogError($"CharacterSelect: Selection failed - {data.message}");
        }
    }

    private void LoadWorld()
    {
        Debug.Log("CharacterSelect: Loading World scene...");
        SceneManager.LoadScene("World");
    }

    private void OnDestroy()
    {
        Debug.Log("CharacterSelect: OnDestroy called");

        if (MessageHandler.Instance != null)
        {
            MessageHandler.Instance.OnLoginResponse -= LoadCharacters;
            MessageHandler.Instance.OnCreateCharacterResponse -= HandleCreateCharacterResponse;
            MessageHandler.Instance.OnSelectCharacterResponse -= HandleSelectCharacterResponse;
        }
    }
}