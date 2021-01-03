using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ScoreCounter : MonoBehaviour
{
    private TextMeshProUGUI textMesh;
    private Transform playerTransform;
    public PlayerCollision collision;
    public int score;

    void Start()
    {
        textMesh = this.GetComponent<TextMeshProUGUI>();
        playerTransform = GameObject.FindWithTag("Player").GetComponent<Transform>();
    }

    void Update()
    {
        if (!collision.gameEnded)
        {
            score = (int)playerTransform.position.z / 8;
            textMesh.SetText(score.ToString("0"));
        }
    }
}
