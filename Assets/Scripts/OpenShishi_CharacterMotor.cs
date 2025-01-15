using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
public class OpenShishi_CharacterMotor : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 4f;       // Base speed of movement
    public float runSpeed = 8f;       // Base speed of movement
    public bool canRun = true;
    public bool canJump = true;
    public bool isRunning = false;
    public float rotationSpeed = 10f;
    public float jumpForce = 5f;       // Jump strength
    public float maxSlopeAngle = 45f;  // Maximum slope angle the character can climb
    public float slopeInfluence = 5f; // Minimum slope angle influencing the character speed

    [Header("Physics Settings")]
    public LayerMask groundLayer;      // Layer to check for ground collisions
    public float groundCheckDistance = 0.1f; // Distance to check below for ground

    private Rigidbody rb;
    private Animator animator;
    private float inputAmount;
    private bool isGrounded;
    private PhysicMaterial currentPhysicsMaterial; // Store the current surface material
    private float slopeAngle;           // Store the current slope angle
    private Vector3 groundNormal = Vector3.up; // Store the ground's normal vector

    // Drift state variables
    private int currentDriftDirection = 0; // -1 for left, +1 for right

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false; // Disable root motion explicitly
    }

    private void FixedUpdate()
    {
        // Check if the character is grounded and update material
        UpdateGroundedState();

        // Apply extra downward force when airborne
        if (!isGrounded || slopeAngle >= 0.1f)
        {
            rb.AddForce(Vector3.up * rb.mass * Physics.gravity.y, ForceMode.Acceleration);
        }

        // Unified sliding and deceleration logic
        if (isGrounded)
        {
            HandleSlidingAndDeceleration();
        }

        // Update Animator with grounded state
        animator.SetBool("IsGrounded", isGrounded);

        // Synchronize Animator Speed with Rigidbody velocity
        UpdateAnimatorSpeed();
    }

    /// <summary>
    /// Updates the speed parameter in the Animator based on horizontal velocity.
    /// </summary>
    private void UpdateAnimatorSpeed()
    {
        float movementSpeed = new Vector3(rb.velocity.x, 0, rb.velocity.z).magnitude;

        if (movementSpeed < 0.2f) movementSpeed = 0f;

        if (isGrounded && slopeAngle >= maxSlopeAngle)
        {
            // Stop walking animation during sliding
            animator.SetFloat("Speed", 0f, 0.2f, Time.deltaTime);
        }
        else
        {
            // Adjust walking animation speed for moderate slopes
            animator.SetFloat("Speed", movementSpeed, 0.2f, Time.deltaTime);
        }

        //animator.SetFloat("SlopeNormal", slopeAngle, 0.2f, Time.deltaTime);
    }

    /// <summary>
    /// Checks if the character is grounded and retrieves ground properties.
    /// </summary>
    private void UpdateGroundedState()
    {
        float raycastWidth = 0.25f; // Adjust based on character size
        float raycastDistance = groundCheckDistance + 0.25f; // Safety margin for uneven ground
        float originOffset = 0.5f; // Offset origins slightly above the feet

        // Raycast origins: four corners and a center
        Vector3[] raycastOrigins = new Vector3[]
        {
        transform.position + new Vector3(raycastWidth, originOffset, raycastWidth),  // Front-right
        transform.position + new Vector3(-raycastWidth, originOffset, raycastWidth), // Front-left
        transform.position + new Vector3(raycastWidth, originOffset, -raycastWidth), // Back-right
        transform.position + new Vector3(-raycastWidth, originOffset, -raycastWidth), // Back-left
        transform.position + new Vector3(0, originOffset, 0) // Center
        };

        int hitCount = 0; // Count of rays hitting the ground
        Vector3 averageNormal = Vector3.zero; // To calculate combined ground normal
        PhysicMaterial detectedMaterial = null; // Temporary variable to store detected material

        foreach (Vector3 origin in raycastOrigins)
        {
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastDistance, groundLayer))
            {
                hitCount++;
                averageNormal += hit.normal;

                // Assign the physics material from the hit collider
                detectedMaterial = hit.collider.sharedMaterial;

                Debug.DrawLine(origin, hit.point, Color.green); // Visualize successful ray
            }
            else
            {
                Debug.DrawLine(origin, origin + Vector3.down * raycastDistance, Color.red); // Visualize missed ray
            }
        }

        // Determine if the character is grounded
        isGrounded = hitCount > 2; // Grounded if majority of rays hit the ground
        groundNormal = isGrounded ? averageNormal.normalized : Vector3.up;
        slopeAngle = isGrounded ? Vector3.Angle(groundNormal, Vector3.up) : 0f;

        // Assign the detected material to currentPhysicsMaterial
        currentPhysicsMaterial = isGrounded ? detectedMaterial : null;

        // If the slope is too steep, treat as not grounded
        if (isGrounded && slopeAngle >= maxSlopeAngle)
        {
            isGrounded = false;
        }
    }

    /// <summary>
    /// Moves the character in a specified direction while accounting for slopes and friction.
    /// </summary>
    /// <param name="direction">The movement direction as a Vector3.</param>
    public void Move(Vector3 direction)
    {
        if (inputAmount < 0.1f)
        {
            return; // Skip movement if input is too low
        }

        direction = direction.normalized;

        // Prevent movement input when sliding
        if (slopeAngle >= maxSlopeAngle)
        {
            return;
        }

        // Project movement direction onto the slope
        if (isGrounded && slopeAngle > 0f)
        {
            direction = Vector3.ProjectOnPlane(direction, groundNormal).normalized;
        }

        // Directly use dynamicFriction for calculations
        float friction = currentPhysicsMaterial != null
            ? Mathf.Clamp01(1f - currentPhysicsMaterial.dynamicFriction)
            : 0.9f; // Default to 0.9f for surfaces without a material

        Vector3 velocity = direction * GetMoveSpeed(direction) * inputAmount * friction;
        velocity.y = rb.velocity.y; // Preserve vertical velocity

        // Introduce drifting mechanic on low-friction surfaces
        if (currentPhysicsMaterial != null && currentPhysicsMaterial.dynamicFriction < 0.3f)
        {
            // Calculate lateral drift
            Vector3 lateralDrift = Vector3.Cross(direction, Vector3.up) * currentDriftDirection * 0.1f * rb.velocity.magnitude;

            // Add drift force to the velocity
            velocity += lateralDrift;
        }

        SetVelocity(velocity);
    }

    /// <summary>
    /// Rotates the character to face a specified target position.
    /// </summary>
    /// <param name="targetPosition">The target position as a Vector3.</param>
    public void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0; // Ignore vertical component for rotation

        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * inputAmount * Time.fixedDeltaTime);
        }
    }

    /// <summary>
    /// Makes the character jump if it is grounded.
    /// </summary>
    public void Jump()
    {
        if (!isGrounded || (slopeAngle >= maxSlopeAngle) || !canJump)
        {
            return;
        }

        rb.velocity = new Vector3(rb.velocity.x, 0, rb.velocity.z);

        // Apply upward force for jump
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);

        animator.SetTrigger("Jump");
    }

    /// <summary>
    /// Applies an external force to the character.
    /// </summary>
    /// <param name="force">The force to apply as a Vector3.</param>
    public void ApplyForce(Vector3 force)
    {
        rb.AddForce(force, ForceMode.Impulse);

        // Optionally trigger an animation for knockback or hit
        animator.SetTrigger("Hit");

        Debug.Log($"Force applied: {force}");
    }

    /// <summary>
    /// Checks if the character is grounded.
    /// </summary>
    /// <returns>True if the character is grounded, false otherwise.</returns>
    public bool IsGrounded()
    {
        return isGrounded;
    }

    float GetMoveSpeed(Vector3 movementDirection)
    {
        float baseSpeed = canRun ? runSpeed : moveSpeed;

        isRunning = canRun;

        if (slopeAngle <= slopeInfluence)
        {
            return baseSpeed; // Full speed on shallow slopes
        }

        // Calculate speed adjustment for uphill/downhill
        float slopeFactor = Mathf.Clamp01(1f - (slopeAngle - slopeInfluence) / (maxSlopeAngle - slopeInfluence));
        float currentMoveSpeed = baseSpeed * slopeFactor;

        // Add downhill speed boost
        Vector3 downhillDirection = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
        float downhillAlignment = Vector3.Dot(movementDirection, downhillDirection);

        if (downhillAlignment > 0.1f) // Ensure the character is moving downhill
        {
            // Scale boost by slope steepness and downhill alignment
            float steepnessFactor = Mathf.Clamp01((slopeAngle - slopeInfluence) / (maxSlopeAngle - slopeInfluence));
            float downhillBoost = baseSpeed * steepnessFactor * downhillAlignment; // Combine slope steepness and alignment
            currentMoveSpeed += downhillBoost;
        }

        // Ensure a minimum speed for uphill and downhill movement
        currentMoveSpeed = Mathf.Max(currentMoveSpeed, baseSpeed * 0.2f);

        return currentMoveSpeed;
    }

    /// <summary>
    /// Gets the current velocity of the character.
    /// </summary>
    /// <returns>The current velocity as a Vector3.</returns>
    public Vector3 GetVelocity()
    {
        return rb.velocity;
    }

    /// <summary>
    /// Sets the velocity of the character.
    /// </summary>
    /// <param name="velocity">The new velocity to set as a Vector3.</param>
    public void SetVelocity(Vector3 velocity)
    {
        rb.velocity = velocity;
    }

    /// <summary>
    /// Gets the current friction of the surface.
    /// </summary>
    /// <returns>The current friction as a float.</returns>
    public float GetCurrentFriction()
    {
        if (currentPhysicsMaterial != null)
        {
            return currentPhysicsMaterial.dynamicFriction;
        }
        return 0.9f; // Default friction when no material is present
    }

    public void SetInputAmount(float amount)
    {
        inputAmount = amount;
    }

    private void HandleSlidingAndDeceleration()
    {
        Vector3 slideDirection = Vector3.zero;
        float slideForce = 0f;
        bool isSliding = false;

        // Sliding decay and speed limits
        float maxSlidingSpeed = 10f;   // Cap for sliding speed
        float minVelocityThreshold = 0.1f; // Velocity below this is treated as stopped

        // Determine if sliding is active
        if (slopeAngle > slopeInfluence)
        {
            // Sliding on slopes
            slideDirection = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;

            // Calculate sliding force for slopes
            if (slopeAngle > maxSlopeAngle)
            {
                slideForce = Mathf.Lerp(moveSpeed * 2f, moveSpeed * 4f, (slopeAngle - maxSlopeAngle) / 15f);
            }
            else
            {
                slideForce = Mathf.Lerp(moveSpeed, moveSpeed * 2f, (slopeAngle - slopeInfluence) / (maxSlopeAngle - slopeInfluence));
            }

            isSliding = true;
        }
        else if (currentPhysicsMaterial != null && currentPhysicsMaterial.dynamicFriction < 0.3f)
        {
            // Sliding on flat low-friction surfaces (e.g., ice)
            bool isStationary = rb.velocity.magnitude < 0.1f;
            slideDirection = isStationary ? transform.forward : rb.velocity.normalized;
            slideForce = isStationary ? moveSpeed : Mathf.Clamp(rb.velocity.magnitude, 0, moveSpeed * 4f * Mathf.Clamp01(1 - Time.fixedDeltaTime) * 0.01f); // Amplify sliding force

            // Introduce drifting force
            if (rb.velocity.magnitude > 0.1f)
            {
                Vector3 lateralDrift = Vector3.Cross(rb.velocity.normalized, Vector3.up) * currentDriftDirection * slideForce * 2f; // Lateral drift
                rb.AddForce(lateralDrift, ForceMode.Acceleration);
            }

            // Stop sliding if velocity is below the threshold
            if (isStationary)
            {
                isSliding = false;
                // Determine random lateral drift direction (-1 for left, +1 for right)
                currentDriftDirection = Random.Range(0, 2) * 2 - 1; // Outputs -1 or 1
            }
            else
            {
                isSliding = true;
            }
        }

        // Apply sliding force if active
        if (isSliding)
        {
            Vector3 slidingForce = slideDirection * slideForce;
            rb.AddForce(slidingForce, ForceMode.Acceleration);

            // Cap the velocity to prevent excessive sliding speed
            if (rb.velocity.magnitude > maxSlidingSpeed)
            {
                Vector3 cappedVelocity = rb.velocity.normalized * maxSlidingSpeed;
                rb.velocity = new Vector3(cappedVelocity.x, rb.velocity.y, cappedVelocity.z); // Preserve vertical velocity
            }
        }

        isRunning = false;

        // Decelerate velocity if not sliding or input is minimal
        Vector3 velocity = rb.velocity;

        if (!isSliding && inputAmount < 0.1f)
        {
            // Get friction from the physics material
            float friction = currentPhysicsMaterial != null
                ? currentPhysicsMaterial.dynamicFriction
                : 0.9f;

            if (friction < 0.3f)
            {
                // Low-friction surfaces: Gradual decay
                velocity.x *= Mathf.Max(0f, 1f - Time.fixedDeltaTime * 0.1f);
                velocity.z *= Mathf.Max(0f, 1f - Time.fixedDeltaTime * 0.1f);
            }
            else
            {
                // High-friction surfaces: Faster decay
                velocity.x = Mathf.Lerp(velocity.x, 0, Time.fixedDeltaTime * 5f);
                velocity.z = Mathf.Lerp(velocity.z, 0, Time.fixedDeltaTime * 5f);
            }
        }

        // Clamp very small velocities to zero
        if (velocity.magnitude <= minVelocityThreshold)
        {
            velocity.x = 0;
            velocity.z = 0;
        }

        // Preserve vertical velocity
        velocity.y = rb.velocity.y;

        // Apply updated velocity
        rb.velocity = velocity;
    }
}
