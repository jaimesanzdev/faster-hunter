using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles the main menu UI for creating and joining multiplayer sessions.
/// Communicates with SessionManager to host or join lobbies via Unity Gaming Services.
/// </summary>
public class SessionUI : MonoBehaviour
{
    [Header("Host")]
    /// <summary>Input field where the player enters the desired session name.</summary>
    public TMP_InputField sessionNameInput;

    /// <summary>Button that triggers the host flow.</summary>
    public Button hostButton;

    [Header("Join")]
    /// <summary>Input field where the player enters the lobby code to join.</summary>
    public TMP_InputField lobbyCodeInput;

    /// <summary>Button that triggers the join flow.</summary>
    public Button joinButton;

    [Header("Status")]
    /// <summary>Text element used to display feedback messages to the player.</summary>
    public TMP_Text statusText;

    /// <summary>
    /// Initializes Unity Gaming Services on startup and registers button listeners.
    /// Displays status feedback while services are being initialized.
    /// </summary>
    async void Start()
    {
        statusText.text = "Initializing services...";
        try
        {
            // Ensure UGS is initialized and the player is anonymously signed in
            await SessionManager.Instance.EnsureServicesAsync();
            statusText.text = "Services are ready.";
        }
        catch (Exception e)
        {
            statusText.text = $"UGS init failed: {e.Message}";
        }

        // Register UI button callbacks
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    /// <summary>
    /// Called when the host button is clicked.
    /// Creates a new lobby and relay allocation, then starts the host.
    /// Falls back to a default session name if the input field is empty.
    /// </summary>
    async void OnHostClicked()
    {
        // Use a default name if the player left the input blank
        string name = string.IsNullOrWhiteSpace(sessionNameInput.text) ? "Faster Hunter" : sessionNameInput.text;

        statusText.text = "Creating session...";
        try
        {
            await SessionManager.Instance.HostSessionAsync(name);

            // Display the lobby code so the host can share it with other players
            var lobby = SessionManager.Instance.CurrentLobby;
            statusText.text = $"Hosting! Share this code to join: {lobby.LobbyCode}.";
        }
        catch (Exception e)
        {
            statusText.text = $"Host failed: {e.Message}";
        }
    }

    /// <summary>
    /// Called when the join button is clicked.
    /// Validates the lobby code input and attempts to join an existing session.
    /// </summary>
    async void OnJoinClicked()
    {
        // Normalize the lobby code: trim whitespace and convert to uppercase
        string code = lobbyCodeInput.text?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(code))
        {
            statusText.text = "Enter a Lobby Code.";
            return;
        }

        statusText.text = "Joining session...";
        try
        {
            await SessionManager.Instance.JoinSessionByLobbyCodeAsync(code);
            statusText.text = $"Joined lobby {code}!";
        }
        catch (Exception e)
        {
            statusText.text = $"Join failed: {e.Message}";
        }
    }
}