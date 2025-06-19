// PlayerAnimator.cs – Unity 6 / Mirror 2025 compatible  ✅
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator), typeof(NetworkAnimator))]
[RequireComponent(typeof(PlayerBombHandler))]
public class PlayerAnimator : NetworkBehaviour
{
    /* ─ parameters ─ */
    static readonly int ActiveHandHash = Animator.StringToHash("activeHand");
    static readonly int IsMovingHash   = Animator.StringToHash("isMoving");
    static readonly int ThrowHash      = Animator.StringToHash("Throw");
    static readonly int StunnedHash    = Animator.StringToHash("Stunned");
    static readonly int LandingHash    = Animator.StringToHash("Landing");
    static readonly int DirectionHash  = Animator.StringToHash("Direction");
    static readonly int EmoteHash      = Animator.StringToHash("Emote");

    [Header("Tuning")]
    [SerializeField] float movementThreshold     = .1f;
    [SerializeField] float landingVelocityThresh = -3f;
    [SerializeField] float handFreezeTime        = .15f;

    Animator        anim;
    NetworkAnimator netAnim;
    PlayerBombHandler bombHandler;
    PlayerMovement  movement;
    Rigidbody       rb;

    /* networking */
    [SyncVar(hook = nameof(OnEmoteChanged))] int emoteParam;
    InputAction emoteAct;

    /* cached-per-frame data */
    Vector3 cachedVelocity;      // <-- ADDED
    float   handFreezeTimer;
    bool    wasFalling;

    /* ───────── life-cycle ───────── */
    void Awake()
    {
        anim        = GetComponent<Animator>();
        netAnim     = GetComponent<NetworkAnimator>();
        bombHandler = GetComponent<PlayerBombHandler>();
        movement    = GetComponent<PlayerMovement>();
        rb          = GetComponent<Rigidbody>();

        if (netAnim.animator == null) netAnim.animator = anim;

        var pi   = GetComponent<PlayerInput>();
        emoteAct = pi.actions.FindAction("Emote");     // 0-3 scale
    }

    void FixedUpdate() => cachedVelocity = rb.linearVelocity;    // Unity 6 API :contentReference[oaicite:1]{index=1}

    public override void OnStartAuthority()
    {
        enabled = true;
        emoteAct.Enable();
    }
    public override void OnStopAuthority()
    {
        emoteAct.Disable();
        enabled = false;
    }

    /* ───────── main loop ───────── */
    void Update()
    {
        if (!isOwned) return;

        UpdateActiveHand();
        UpdateMovementState();
        CheckLanding();
        HandleEmoteInput();
    }

    /* ────── hand / move / land ────── */
    void UpdateActiveHand()
    {
        if (handFreezeTimer > 0f) { handFreezeTimer -= Time.deltaTime; return; }

        int hand = 0;
        if (bombHandler.CurrentBomb && bombHandler.CurrentBomb.Holder == gameObject)
            hand = bombHandler.CurrentBomb.IsOnRight ? 2 : 1;

        anim.SetInteger(ActiveHandHash, hand);
    }

    void UpdateMovementState()
    {
        Vector3 hv = new(cachedVelocity.x, 0f, cachedVelocity.z);   // <-- uses cached velocity
        bool moving = hv.magnitude > movementThreshold;
        anim.SetBool(IsMovingHash, moving);

        if (!moving) return;

        Vector3 local = transform.InverseTransformDirection(hv.normalized);
        float a   = Mathf.Atan2(local.x, local.z);          // −π..π
        int   sec = (Mathf.RoundToInt(a / (Mathf.PI * .5f)) & 3);
        float dir = sec / 3f;
        anim.SetFloat(DirectionHash, dir, 0.15f, Time.deltaTime);
    }

    void CheckLanding()
    {
        bool ground = Physics.Raycast(transform.position + Vector3.up * .1f,
                                      Vector3.down, .2f, ~0);
        if (wasFalling && ground && rb.linearVelocity.y > landingVelocityThresh)
            netAnim.SetTrigger(LandingHash);
        wasFalling = !ground;
    }

    /* ────── emotes ────── */
    void HandleEmoteInput()
    {
        int v = (int)emoteAct.ReadValue<float>();      // InputSystem API :contentReference[oaicite:2]{index=2}
        if (v == emoteParam) return;

        ApplyEmote(v);  // local
        CmdSetEmote(v); // network
    }
    [Command] void CmdSetEmote(int v) => emoteParam = v;
    void OnEmoteChanged(int _, int v)  => ApplyEmote(v);

    void ApplyEmote(int v)
    {
        anim.SetInteger(EmoteHash, v);
        movement?.SetEmoteState(v != 0);
    }

    /* ────── throw entry point ────── */
    public void PlayThrowLocal()
    {
        if (!isOwned) return;

        int hand = bombHandler.CurrentBomb.IsOnRight ? 2 : 1;
        anim.SetInteger(ActiveHandHash, hand);  // local frame guarantee

        /*  ▪ Call both Animators:
            • anim.SetTrigger  – so **our** client plays instantly
            • netAnim.SetTrigger – so Mirror replicates to others  */
        anim.SetTrigger(ThrowHash);
        netAnim.SetTrigger(ThrowHash);          // required for sync :contentReference[oaicite:3]{index=3}

        handFreezeTimer = handFreezeTime;
    }

    public void OnPlayerStunned()
    {
        if (isOwned) netAnim.SetTrigger(StunnedHash);
    }
}
