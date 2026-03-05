using Unity.Netcode;
using UnityEngine;
using TMPro;

public class GameHUD : MonoBehaviour
{
    [Header("Score Texts")]
    public TMP_Text scoreP1;
    public TMP_Text scoreP2;
    public TMP_Text scoreP3;
    public TMP_Text scoreP4;

    [Header("Rule Text")]
    public TMP_Text ruleText;

    void Update()
    {
        if (GameRoundManager.Instance == null) return;

        var mgr = GameRoundManager.Instance;

        // Actualizar regla
        ruleText.text = mgr.RuleText.Value.ToString();

        // Jugadores conectados
        int connected = NetworkManager.Singleton.ConnectedClients.Count;

        UpdateScoreText(scoreP1, 0, connected);
        UpdateScoreText(scoreP2, 1, connected);
        UpdateScoreText(scoreP3, 2, connected);
        UpdateScoreText(scoreP4, 3, connected);
    }

    void UpdateScoreText(TMP_Text text, int index, int connected)
    {
        if (index < connected)
        {
            int score = GameRoundManager.Instance.Scores[index];
            text.text = $"P{index + 1}: {score}";
        }
        else
        {
            text.text = $"P{index + 1}: -";
        }
    }
}