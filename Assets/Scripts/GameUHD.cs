using Unity.Netcode;
using UnityEngine;
using TMPro;

/// <summary>
/// In-game HUD that displays real-time session information to all players.
/// Reads replicated state from GameRoundManager and SessionManager every frame.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Score Texts")]
    /// <summary>Score display for Player 1.</summary>
    public TMP_Text scoreP1;

    /// <summary>Score display for Player 2.</summary>
    public TMP_Text scoreP2;

    /// <summary>Score display for Player 3.</summary>
    public TMP_Text scoreP3;

    /// <summary>Score display for Player 4.</summary>
    public TMP_Text scoreP4;

    [Header("Session Info")]
    /// <summary>Displays the current session name and lobby code.</summary>
    public TMP_Text sessionInfoText;

    [Header("Rule Text")]
    /// <summary>Displays the current round rule (e.g. "Find 3 Red Circles").</summary>
    public TMP_Text ruleText;

    /// <summary>
    /// Updates all HUD elements every frame with the latest replicated game state.
    /// Skips update if the GameRoundManager is not yet available.
    /// </summary>
    void Update()
    {
        if (GameRoundManager.Instance == null) return;

        var mgr = GameRoundManager.Instance;

        // Display the current round rule from the replicated NetworkVariable
        ruleText.text = mgr.RuleText.Value.ToString();

        // Only show scores for clients that are currently connected
        int connected = NetworkManager.Singleton.ConnectedClients.Count;

        UpdateScoreText(scoreP1, 0, connected);
        UpdateScoreText(scoreP2, 1, connected);
        UpdateScoreText(scoreP3, 2, connected);
        UpdateScoreText(scoreP4, 3, connected);

        // Display lobby name and code if a session is active
        if (SessionManager.Instance != null && SessionManager.Instance.CurrentLobby != null)
        {
            string name = SessionManager.Instance.CurrentLobby.Name;
            string code = SessionManager.Instance.CurrentLobby.LobbyCode;

            sessionInfoText.text = $"Session: {name}\nCode: {code}";
        }
        else
        {
            sessionInfoText.text = "";
        }
    }

    /// <summary>
    /// Updates a single player's score text element.
    /// Shows the score if the player is connected, or a dash placeholder otherwise.
    /// </summary>
    /// <param name="text">The TMP_Text element to update.</param>
    /// <param name="index">The player index (0-based), matching the Scores NetworkList.</param>
    /// <param name="connected">Total number of currently connected clients.</param>
    void UpdateScoreText(TMP_Text text, int index, int connected)
    {
        if (index < connected)
        {
            int score = GameRoundManager.Instance.Scores[index];
            text.text = $"P{index + 1}: {score}";
        }
        else
        {
            // Player slot is empty — show a placeholder
            text.text = $"P{index + 1}: -";
        }
    }
}