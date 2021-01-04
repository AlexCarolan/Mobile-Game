using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GenerateLevel : MonoBehaviour
{

    public int maxActiveParts = 15;
    public int blockPrefabCount;
    public Transform startBlock;

    public Transform playerPosition;

    private float endPosition;

    private GameObject[] blockPrefabs;

    private int lastRandom;

    // Start is called before the first frame update
    void Start()
    {
        //Load block prefabs into array
        blockPrefabs = new GameObject[blockPrefabCount];

        for (int i=0; i<blockPrefabCount; i++)
        {
            string path = "Prefabs/Blocks/Block" + i;
            blockPrefabs[i] = Resources.Load<GameObject>(path);
        }

        //Get offset from start block
        endPosition = startBlock.localScale.z / 2;

        //Generate inital blocks
        for (int i = 0; i < maxActiveParts; i++)
        {
            generateNewBlock();
        }

        lastRandom = Random.Range(0, blockPrefabCount);

    }

    void generateNewBlock()
    {
        //Generate randomnumber for prefab,avoid using same as last
        int random = Random.Range(0, blockPrefabCount);
        while (lastRandom == random)
        {
            random = Random.Range(0, blockPrefabCount);
        }

        lastRandom = random;

        GameObject randomBlock = blockPrefabs[random];

        endPosition = endPosition + (randomBlock.transform.Find("Floor").GetComponent<Transform>().localScale.z / 2);

        Instantiate(randomBlock, new Vector3(0, 0, endPosition), Quaternion.identity);

        endPosition = endPosition + (randomBlock.transform.Find("Floor").GetComponent<Transform>().localScale.z / 2);

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
