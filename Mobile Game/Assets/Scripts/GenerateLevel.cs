using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenerateLevel : MonoBehaviour
{

    public int maxActiveParts = 15;
    public Transform startBlock;

    public Transform playerPosition;

    private float endPosition;
    private float midPosition;

    public GameObject test;

    private GameObject[] blocks;


    // Start is called before the first frame update
    void Start()
    {
        endPosition = startBlock.localScale.z/2;

        for (int i = 0; i < maxActiveParts; i++)
        {
            generateNewBlock();
        }

    }

    void generateNewBlock()
    {
        endPosition = endPosition + test.GetComponent<Transform>().localScale.x;
        Instantiate(test, new Vector3(0, 0, endPosition), Quaternion.identity);
        endPosition = endPosition + test.GetComponent<Transform>().localScale.x;
    }

    // Update is called once per frame
    void Update()
    {
        GameObject[] blocks = GameObject.FindGameObjectsWithTag("Block");

        //Destroy blocks out of scope (behind player)
        foreach (GameObject block in blocks)
        {
           if (block.GetComponent<Transform>().position.z <= playerPosition.position.z-100)
            {
                Destroy(block);

                if (blocks.Length < maxActiveParts)
                {
                    generateNewBlock();
                }
            }
        }
    }
}
