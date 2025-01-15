using Rewired;
using UnityEngine;

public class OpenShishi_PlayerController : MonoBehaviour
{
    private OpenShishi_CharacterMotor motor;
    private Player playerInput; // Reference to Rewired Player input

    private Vector3 direction;

    private void Start()
    {
        motor = GetComponent<OpenShishi_CharacterMotor>();
        playerInput = OpenShishi_InputManager.Instance.GetPlayerInput();
    }

    private void Update()
    {
        HandleMovement();
        HandleActions();
    }

    private void FixedUpdate()
    {
        if (direction.magnitude > 0.1f)
        {
            motor.Move(direction); // Player-driven movement
            motor.RotateTowards(transform.position + direction);
        }
    }

    private void HandleMovement()
    {
        // Read input
        float h = playerInput.GetAxis("Move Horizontal");
        float v = playerInput.GetAxis("Move Vertical");

        // Apply dead zone
        if (Mathf.Abs(h) < 0.1f) h = 0;
        if (Mathf.Abs(v) < 0.1f) v = 0;

        // Calculate input magnitude and pass to motor
        float inputMagnitude = Mathf.Abs(h) + Mathf.Abs(v);
        motor.SetInputAmount(Mathf.Clamp01(inputMagnitude));

        // If no input, retain the current direction (to allow sliding)
        if (h == 0 && v == 0)
        {
            return;
        }

        // Base movement on camera direction
        Vector3 correctedVertical = v * Vector3.Scale(Camera.main.transform.forward, new Vector3(1, 0, 1));
        Vector3 correctedHorizontal = h * Camera.main.transform.right;

        // Calculate combined input direction
        direction = (correctedHorizontal + correctedVertical).normalized;
    }

    private void HandleActions()
    {
        if (playerInput.GetButtonDown("Jump"))
        {
            motor.Jump();
        }

        if (playerInput.GetButtonDown("Ability"))
        {
            Debug.Log("Ability activated!");
            // Trigger ability logic
        }
    }
}
