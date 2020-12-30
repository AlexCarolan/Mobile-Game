using UnityEngine;

public class playerCollision : MonoBehaviour
{
    private PlayerMovement playerMovement;
    private PlayerCameraTether playerCameraTether;

    private void Start()
    {
        playerMovement = this.GetComponent<PlayerMovement>();
        playerCameraTether = GameObject.FindGameObjectsWithTag("MainCamera")[0].GetComponent<PlayerCameraTether>();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.tag == "Obstacle")
        {
           playerMovement.enabled = false;
           playerCameraTether.enabled = false;
        }
    }

}
