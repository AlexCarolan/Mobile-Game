using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    private PlayerMovement playerMovement;
    private PlayerCameraTether playerCameraTether;

    public Material playerRed;
    public Transform groundTransform;

    public float cubeSize = 0.2f;
    public int cubesInRow = 5;

    float cubesPivotDistance;
    Vector3 cubesPivot;

    public float explosionForce = 5f;

    private void Start()
    {
        playerMovement = this.GetComponent<PlayerMovement>();
        playerCameraTether = GameObject.FindGameObjectsWithTag("MainCamera")[0].GetComponent<PlayerCameraTether>();

        //calculate pivot distance
        cubesPivotDistance = cubeSize * cubesInRow / 2;
        //use this value to create pivot vector)
        cubesPivot = new Vector3(cubesPivotDistance, cubesPivotDistance, cubesPivotDistance);
    }

    private void Update()
    {
        float playerX = this.GetComponent<Transform>().position.x;

        if (playerX >= (groundTransform.localScale.x/2))
        {
            EndGame();
        }
        else if (playerX <= -(groundTransform.localScale.x / 2))
        {
            EndGame();
        }

    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Obstacle"))
        {
            EndGame();
        }
    }

    void EndGame()
    {
        playerMovement.enabled = false;
        playerCameraTether.enabled = false;

        Explode();
    }

    public void Explode()
    {
        Vector3 endVelocity = this.GetComponent<Rigidbody>().velocity;

        //make object disappear
        gameObject.SetActive(false);

        //loop 3 times to create 5x5x5 pieces in x,y,z coordinates
        for (int x = 0; x < cubesInRow; x++)
        {
            for (int y = 0; y < cubesInRow; y++)
            {
                for (int z = 0; z < cubesInRow; z++)
                {
                    CreatePiece(x, y, z);
                }
            }
        }

        GameObject[] cubes = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject cube in cubes)
        {
            cube.GetComponent<Rigidbody>().AddForce(new Vector3(Random.Range(0.01f, 1f) + endVelocity.x / 10, Random.Range(0.01f, 0.5f), explosionForce), ForceMode.Impulse);
        }


    }

    private void CreatePiece(int x, int y, int z)
    {

        //create piece
        GameObject piece;
        piece = GameObject.CreatePrimitive(PrimitiveType.Cube);

        //set piece position and scale
        piece.transform.position = transform.position + new Vector3(cubeSize * x + 0.1f, cubeSize * y + 0.1f, cubeSize * z + 0.1f) - cubesPivot;
        piece.transform.localScale = new Vector3(cubeSize, cubeSize, cubeSize);

        //add rigidbody and set mass
        piece.AddComponent<Rigidbody>();
        piece.GetComponent<Rigidbody>().mass = cubeSize;

        //set material
        piece.GetComponent<Renderer>().material = playerRed;

        //set tag
        piece.tag = "Player";

    }

}