using Mirror;
using UnityEngine;

[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(NetworkAnimator))]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(PlayerBombHandler))]
public class PlayerAnimator : NetworkBehaviour
{
    // Animator parameters
    private const string ActiveHandName = "activeHand";
    private const string IsMovingName   = "isMoving";
    private const string ThrowName      = "Throw";
    private const string StunnedName    = "Stunned";
    private const string LandingName    = "Landing";

    private static readonly int ActiveHandHash = Animator.StringToHash(ActiveHandName);
    private static readonly int IsMovingHash   = Animator.StringToHash(IsMovingName);

    [Header("Tuning")]
    [SerializeField] private float movementThreshold     = 0.1f;
    [SerializeField] private float fallThreshold         = -2f;
    [SerializeField] private float landingVelocityThresh = -3f;

    private Animator          animator;
    private NetworkAnimator   netAnimator;
    private PlayerMovement    movement;
    private PlayerBombHandler bombHandler;
    private Rigidbody         rb;

    private bool wasFalling;

    /* ───────────────────────────────────────────── */

    void Awake()
    {
        animator     = GetComponent<Animator>();
        netAnimator  = GetComponent<NetworkAnimator>();
        movement     = GetComponent<PlayerMovement>();
        bombHandler  = GetComponent<PlayerBombHandler>();
        rb           = GetComponent<Rigidbody>();

        if (netAnimator.animator == null)
            netAnimator.animator = animator;
    }

    /* run only on the owner */
    public override void OnStartAuthority() => enabled = true;
    public override void OnStopAuthority()  => enabled = false;

    void Update()
    {
        if (!isOwned) return;               // <─ NEW property

        UpdateActiveHand();
        UpdateMovementState();
        CheckLanding();
    }

    /* ───────── helpers ───────── */

    void UpdateActiveHand()
    {
        int hand = 0;
        if (bombHandler.CurrentBomb &&
            bombHandler.CurrentBomb.Holder == gameObject)
            hand = bombHandler.CurrentBomb.IsOnRight ? 2 : 1;

        animator.SetInteger(ActiveHandHash, hand);
    }

    void UpdateMovementState()
    {
        Vector3 horiz = new(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        animator.SetBool(IsMovingHash, horiz.magnitude > movementThreshold);
    }

    void CheckLanding()
    {
        bool falling = rb.linearVelocity.y < fallThreshold;

        if (wasFalling && !falling && rb.linearVelocity.y > landingVelocityThresh)
            netAnimator.SetTrigger(LandingName);

        wasFalling = falling;
    }

    /* ───────── public API ───────── */

    public void OnBombThrow()
    {
        if (!isOwned) return;
        netAnimator.SetTrigger(ThrowName);
    }

    public void OnPlayerStunned()
    {
        if (!isOwned) return;
        netAnimator.SetTrigger(StunnedName);
    }
}
