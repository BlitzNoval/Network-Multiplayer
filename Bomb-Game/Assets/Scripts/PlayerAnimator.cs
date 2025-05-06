using Mirror;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(PlayerMovement), typeof(PlayerBombHandler))]
public class PlayerAnimator : NetworkBehaviour
{
    // Animation parameter names
    private static readonly int ActiveHandParam = Animator.StringToHash("activeHand");
    private static readonly int IsMovingParam = Animator.StringToHash("isMoving");
    private static readonly int ThrowParam = Animator.StringToHash("Throw");
    private static readonly int StunnedParam = Animator.StringToHash("Stunned");
    private static readonly int LandingParam = Animator.StringToHash("Landing");

    // Component references
    private Animator animator;
    private PlayerMovement movement;
    private PlayerBombHandler bombHandler;
    private PlayerLifeManager lifeManager;
    private Rigidbody rb;

    // Movement detection
    [SerializeField] private float movementThreshold = 0.1f;
    
    // Landing detection
    [SerializeField] private float fallThreshold = -2f;
    [SerializeField] private float landingVelocityThreshold = -3f;
    private bool wasFalling = false;

    // Networked animation state
    [SyncVar(hook = nameof(OnActiveHandChanged))]
    private int activeHand = 0; // 0 = none, 1 = left, 2 = right

    [SyncVar(hook = nameof(OnIsMovingChanged))]
    private bool isMoving = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<PlayerMovement>();
        bombHandler = GetComponent<PlayerBombHandler>();
        lifeManager = GetComponent<PlayerLifeManager>();
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // Initialize animation parameters
        animator.SetInteger(ActiveHandParam, 0);
        animator.SetBool(IsMovingParam, false);
    }

    private void Update()
    {
        if (!isLocalPlayer && !isServer) return;

        // Only the server updates the SyncVars
        if (isServer)
        {
            // Update active hand based on bomb holding state
            UpdateActiveHand();

            // Update movement state
            UpdateMovementState();
        }

        // Local player can handle some visual-only animations that don't need to be synced
        if (isLocalPlayer)
        {
            // Handle landing animation detection (client-side prediction)
            CheckForLanding();
        }
    }

    [Server]
    private void UpdateActiveHand()
    {
        int newActiveHand = 0;
        
        if (bombHandler && bombHandler.CurrentBomb != null && 
            bombHandler.CurrentBomb.Holder == gameObject)
        {
            newActiveHand = bombHandler.CurrentBomb.IsOnRight ? 2 : 1;
        }
        
        if (activeHand != newActiveHand)
        {
            activeHand = newActiveHand;
        }
    }

    [Server]
    private void UpdateMovementState()
    {
        if (movement)
        {
            Vector3 horizVelocity = new Vector3(rb.linearVelocity.x, 0, rb.linearVelocity.z);
            bool newIsMoving = horizVelocity.magnitude > movementThreshold;
            
            if (isMoving != newIsMoving)
            {
                isMoving = newIsMoving;
            }
        }
    }

    private void CheckForLanding()
    {
        if (rb)
        {
            // Check if we're falling
            bool isFalling = rb.linearVelocity.y < fallThreshold;

            // If we were falling but now we're not, and we hit with enough force
            if (wasFalling && !isFalling && rb.linearVelocity.y > landingVelocityThreshold)
            {
                // We've landed
                TriggerLandingAnimation();
            }

            wasFalling = isFalling;
        }
    }

    // Animation triggers that can be called from other components

    [Client]
    public void TriggerThrowAnimation()
    {
        animator.SetTrigger(ThrowParam);
    }

    [Client]
    public void TriggerStunnedAnimation()
    {
        animator.SetTrigger(StunnedParam);
    }

    [Client]
    private void TriggerLandingAnimation()
    {
        animator.SetTrigger(LandingParam);
    }

    // SyncVar hooks for networked animation state

    private void OnActiveHandChanged(int oldValue, int newValue)
    {
        animator.SetInteger(ActiveHandParam, newValue);
    }

    private void OnIsMovingChanged(bool oldValue, bool newValue)
    {
        animator.SetBool(IsMovingParam, newValue);
    }

    // Public methods to be called from other components

    // Called by PlayerBombHandler when throwing a bomb
    public void OnBombThrow()
    {
        if (isLocalPlayer)
        {
            TriggerThrowAnimation();
        }
    }

    // Called by PlayerLifeManager when hit by an explosion
    public void OnPlayerStunned()
    {
        if (isLocalPlayer)
        {
            TriggerStunnedAnimation();
        }
    }
}