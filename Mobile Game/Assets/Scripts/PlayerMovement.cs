using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{

    private readonly float forwardSpeed = 500f;
    private readonly float sideSpeed = 500f;

    private Rigidbody rigidBody;

    // Start is called before the first frame update
    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        //Forward momentum
        rigidBody.AddForce(0, 0, forwardSpeed * Time.fixedDeltaTime);

        //Directional Controls
        //RIGHT
        if (Input.GetKey("d"))
        {
            rigidBody.AddForce(sideSpeed * Time.fixedDeltaTime, 0, 0);
        }

        //LEFT
        if (Input.GetKey("a"))
        {
            rigidBody.AddForce(-sideSpeed * Time.fixedDeltaTime, 0, 0);
        }
    }
}
