using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator), typeof(NetworkAnimator))]
[RequireComponent(typeof(PlayerMovement), typeof(PlayerBombHandler))]
public class PlayerAnimator : NetworkBehaviour
{
    // Animator parameters
    private const string ActiveHandName = "activeHand";
    private const string IsMovingName = "isMoving";
    private const string ThrowName = "Throw";
    private const string StunnedName = "Stunned";
    private const string LandingName = "Landing";
    private const string DirectionName = "Direction";
    private const string EmoteName = "Emote";

    private static readonly int ActiveHandHash = Animator.StringToHash(ActiveHandName);
    private static readonly int IsMovingHash = Animator.StringToHash(IsMovingName);
    private static readonly int DirectionHash = Animator.StringToHash(DirectionName);
    private static readonly int EmoteHash = Animator.StringToHash(EmoteName);

    [Header("Tuning")]
    [SerializeField] private float movementThreshold = 0.1f;
    [SerializeField] private float fallThreshold = -2f;
    [SerializeField] private float landingVelocityThresh = -3f;

    private Animator animator;
    private NetworkAnimator netAnimator;
    private PlayerMovement movement;
    private PlayerBombHandler bombHandler;
    private Rigidbody rb;

    private bool wasFalling;

    private InputAction emoteAct;
    private int lastEmote;

    void Awake()
    {
        animator = GetComponent<Animator>();
        netAnimator = GetComponent<NetworkAnimator>();
        movement = GetComponent<PlayerMovement>();
        bombHandler = GetComponent<PlayerBombHandler>();
        rb = GetComponent<Rigidbody>();

        if (netAnimator.animator == null)
            netAnimator.animator = animator;

        var pi = GetComponent<PlayerInput>();
        emoteAct = pi.actions.FindAction("Emote");
    }

    public override void OnStartAuthority()
    {
        enabled = true;
        emoteAct.Enable();
    }

    public override void OnStopAuthority()
    {
        enabled = false;
        emoteAct.Disable();
    }

    void Update()
    {
        if (!isOwned) return;

        UpdateActiveHand();
        UpdateMovementState();
        CheckLanding();

        // Poll the emote value each frame and replicate when it changes
        int val = (int)emoteAct.ReadValue<float>(); // 0,1,2,3
        if (val != lastEmote)
        {
            animator.SetInteger(EmoteHash, val); // NetworkAnimator syncs this
            lastEmote = val;
        }
    }

    void UpdateActiveHand()
    {
        int hand = 0;
        if (bombHandler.CurrentBomb && bombHandler.CurrentBomb.Holder == gameObject)
            hand = bombHandler.CurrentBomb.IsOnRight ? 2 : 1;

        animator.SetInteger(ActiveHandHash, hand);
    }

    void UpdateMovementState()
    {
        Vector3 horiz = new(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        bool moving = horiz.magnitude > movementThreshold;
        animator.SetBool(IsMovingHash, moving);

        if (moving)
        {
            Vector3 local = transform.InverseTransformDirection(horiz.normalized);
            // Map -π to π to the 4 discrete sectors: Forward (0), Left (1), Backward (2), Right (3)
            float a = Mathf.Atan2(local.x, local.z);          // radians
            int sector = Mathf.RoundToInt(a / (Mathf.PI * 0.5f)) & 3; // 0-3
            float dirParam = sector / 3f;                     // 0, 0.333, 0.666, 1
            animator.SetFloat(DirectionHash, dirParam);
        }
    }

    void CheckLanding()
    {
        bool falling = rb.linearVelocity.y < fallThreshold;

        if (wasFalling && !falling && rb.linearVelocity.y > landingVelocityThresh)
            netAnimator.SetTrigger(LandingName);

        wasFalling = falling;
    }

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