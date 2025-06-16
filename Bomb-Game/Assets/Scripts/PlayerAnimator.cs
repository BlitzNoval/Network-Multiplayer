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

    private InputAction emote1Act, emote2Act, emote3Act;

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
        emote1Act = pi.actions.FindAction("Emote1");
        emote2Act = pi.actions.FindAction("Emote2");
        emote3Act = pi.actions.FindAction("Emote3");
    }

    public override void OnStartAuthority()
    {
        enabled = true;
        emote1Act.Enable();
        emote2Act.Enable();
        emote3Act.Enable();
        emote1Act.started += OnEmote1Started;
        emote1Act.canceled += OnEmote1Canceled;
        emote2Act.started += OnEmote2Started;
        emote2Act.canceled += OnEmote2Canceled;
        emote3Act.started += OnEmote3Started;
        emote3Act.canceled += OnEmote3Canceled;
    }

    public override void OnStopAuthority()
    {
        enabled = false;
        emote1Act.Disable();
        emote2Act.Disable();
        emote3Act.Disable();
        emote1Act.started -= OnEmote1Started;
        emote1Act.canceled -= OnEmote1Canceled;
        emote2Act.started -= OnEmote2Started;
        emote2Act.canceled -= OnEmote2Canceled;
        emote3Act.started -= OnEmote3Started;
        emote3Act.canceled -= OnEmote3Canceled;
    }

    private void OnEmote1Started(InputAction.CallbackContext ctx) => animator.SetInteger(EmoteHash, 1);
    private void OnEmote1Canceled(InputAction.CallbackContext ctx) => animator.SetInteger(EmoteHash, 0);
    private void OnEmote2Started(InputAction.CallbackContext ctx) => animator.SetInteger(EmoteHash, 2);
    private void OnEmote2Canceled(InputAction.CallbackContext ctx) => animator.SetInteger(EmoteHash, 0);
    private void OnEmote3Started(InputAction.CallbackContext ctx) => animator.SetInteger(EmoteHash, 3);
    private void OnEmote3Canceled(InputAction.CallbackContext ctx) => animator.SetInteger(EmoteHash, 0);

    void Update()
    {
        if (!isOwned) return;

        UpdateActiveHand();
        UpdateMovementState();
        CheckLanding();
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
        if (horiz.magnitude > movementThreshold)
        {
            animator.SetBool(IsMovingHash, true);
            Vector3 localVel = transform.InverseTransformDirection(horiz);
            float angle = Mathf.Atan2(localVel.x, localVel.z) * Mathf.Rad2Deg;
            float direction = (angle + 180f) / 360f;
            animator.SetFloat(DirectionHash, direction);
        }
        else
        {
            animator.SetBool(IsMovingHash, false);
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