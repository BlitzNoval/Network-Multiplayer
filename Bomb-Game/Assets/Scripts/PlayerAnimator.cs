using Mirror;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Animator), typeof(PlayerMovement), typeof(PlayerBombHandler))]
public class PlayerAnimator : NetworkBehaviour
{
    private static readonly int ActiveHandParam = Animator.StringToHash("activeHand");
    private static readonly int IsMovingParam = Animator.StringToHash("isMoving");
    private static readonly int ThrowParam = Animator.StringToHash("Throw");
    private static readonly int StunnedParam = Animator.StringToHash("Stunned");
    private static readonly int LandingParam = Animator.StringToHash("Landing");

    private Animator animator;
    private PlayerMovement movement;
    private PlayerBombHandler bombHandler;
    private PlayerLifeManager lifeManager;
    private Rigidbody rb;

    [SerializeField] private float movementThreshold = 0.1f;
    
    [SerializeField] private float fallThreshold = -2f;
    [SerializeField] private float landingVelocityThreshold = -3f;
    private bool wasFalling = false;

    [SyncVar(hook = nameof(OnActiveHandChanged))]
    private int activeHand = 0;

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
        animator.SetInteger(ActiveHandParam, 0);
        animator.SetBool(IsMovingParam, false);
    }

    private void Update()
    {
        if (!isLocalPlayer && !isServer) return;

        if (isServer)
        {
            UpdateActiveHand();

            UpdateMovementState();
        }

        if (isLocalPlayer)
        {
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
            bool isFalling = rb.linearVelocity.y < fallThreshold;

            if (wasFalling && !isFalling && rb.linearVelocity.y > landingVelocityThreshold)
            {
                TriggerLandingAnimation();
            }

            wasFalling = isFalling;
        }
    }


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


    private void OnActiveHandChanged(int oldValue, int newValue)
    {
        animator.SetInteger(ActiveHandParam, newValue);
    }

    private void OnIsMovingChanged(bool oldValue, bool newValue)
    {
        animator.SetBool(IsMovingParam, newValue);
    }


    public void OnBombThrow()
    {
        if (isLocalPlayer)
        {
            TriggerThrowAnimation();
        }
    }

    public void OnPlayerStunned()
    {
        if (isLocalPlayer)
        {
            TriggerStunnedAnimation();
        }
    }
}