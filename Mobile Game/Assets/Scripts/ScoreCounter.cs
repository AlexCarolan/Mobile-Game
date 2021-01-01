using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ScoreCounter : MonoBehaviour
{
    private TextMeshProUGUI textMesh;
    private Transform playerTransform;

    void Start()
    {
        textMesh = this.GetComponent<TextMeshProUGUI>();
        playerTransform = GameObject.FindWithTag("Player").GetComponent<Transform>();
    }

    void Update()
    {
        float score = playerTransform.position.z / 8;
        textMesh.SetText(score.ToString("0"));
    }
}
