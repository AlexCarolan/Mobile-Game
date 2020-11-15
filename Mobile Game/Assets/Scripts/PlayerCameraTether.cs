using UnityEngine;

public class PlayerCameraTether : MonoBehaviour
{

    private Transform playerTransform;
    private Vector3 cameraOffest = new Vector3(0f, 1f, -5f);

    // Start is called before the first frame update
    void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        playerTransform = player.GetComponent<Transform>();
    }

    // Update is called once per frame
    void Update()
    {
        this.transform.position = playerTransform.position + cameraOffest;

    }
}
