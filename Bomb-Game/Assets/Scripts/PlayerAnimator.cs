using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Animator), typeof(NetworkAnimator))]
[RequireComponent(typeof(PlayerBombHandler))]
public class PlayerAnimator : NetworkBehaviour
{
    /* ─ hashes ─ */
    static readonly int ActiveHandHash = Animator.StringToHash("activeHand");
    static readonly int IsMovingHash   = Animator.StringToHash("isMoving");
    static readonly int ThrowHash      = Animator.StringToHash("Throw");      // Trigger
    static readonly int StunnedHash    = Animator.StringToHash("Stunned");
    static readonly int LandingHash    = Animator.StringToHash("Landing");
    static readonly int DirectionHash  = Animator.StringToHash("Direction");
    static readonly int EmoteHash      = Animator.StringToHash("Emote");

    [Header("Tuning")]
    [SerializeField] float movementThreshold     = .1f;
    [SerializeField] float landingVelocityThresh = -3f;
    [SerializeField] float handFreezeTime        = .15f;   // keep activeHand stable for 1 frame

    Animator        anim;
    NetworkAnimator netAnim;
    PlayerBombHandler bombHandler;
    PlayerMovement  movement;
    Rigidbody       rb;

    /* emote replication */
    [SyncVar(hook = nameof(OnEmoteChanged))] int emoteParam;          // 0-3
    InputAction emoteAct;

    float handFreezeTimer;

    bool wasFalling;

    /* ───────── life-cycle ───────── */
    void Awake()
    {
        anim        = GetComponent<Animator>();
        netAnim     = GetComponent<NetworkAnimator>();
        bombHandler = GetComponent<PlayerBombHandler>();
        movement    = GetComponent<PlayerMovement>();
        rb          = GetComponent<Rigidbody>();

        if (netAnim.animator == null)
            netAnim.animator = anim;

        var pi = GetComponent<PlayerInput>();
        emoteAct = pi.actions.FindAction("Emote");   // value (Scale) binding 0-3
        
        // Ensure components are assigned
        if (anim == null) Debug.LogError("Animator component not found on " + gameObject.name);
        if (netAnim == null) Debug.LogError("NetworkAnimator component not found on " + gameObject.name);
    }

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
        Vector3 hv = new(rb.linearVelocity.x, 0, rb.linearVelocity.z);
        bool moving = hv.magnitude > movementThreshold;
        anim.SetBool(IsMovingHash, moving);

        if (!moving) return;

        Vector3 local = transform.InverseTransformDirection(hv.normalized);
        float   a     = Mathf.Atan2(local.x, local.z);         // −π..π
        int     sec   = Mathf.RoundToInt(a / (Mathf.PI * .5f)) & 3;
        float   dir   = sec / 3f;
        anim.SetFloat(DirectionHash, dir, 0.15f, Time.deltaTime);
    }

    void CheckLanding()
    {
        bool ground = Physics.Raycast(transform.position + Vector3.up*.1f, Vector3.down, .2f, ~0);
        if (wasFalling && ground && rb.linearVelocity.y > landingVelocityThresh)
            netAnim.SetTrigger(LandingHash);
        wasFalling = !ground;
    }

    /* ────── emotes ────── */
    void HandleEmoteInput()
    {
        int v = (int)emoteAct.ReadValue<float>();
        if (v == emoteParam) return;

        ApplyEmote(v);     // local
        CmdSetEmote(v);    // replicate
    }
    [Command] void CmdSetEmote(int v) => emoteParam = v;
    void OnEmoteChanged(int _, int v)  => ApplyEmote(v);

    void ApplyEmote(int v)
    {
        anim.SetInteger(EmoteHash, v);
        movement?.SetEmoteState(v != 0);   // lock movement while emote plays
    }

    /* ────── throw entry points ───── */
    /// <summary>Owner calls this *before* sending the Cmd.</summary>
    public void PlayThrowLocal()
    {
        if (!isOwned) return;
        handFreezeTimer = handFreezeTime;      // keep activeHand as 1/2 for a frame
        netAnim.SetTrigger(ThrowHash);         // Set trigger locally; NetworkAnimator syncs it
        Debug.Log("PlayThrowLocal called - Set throw trigger locally on " + gameObject.name);
    }

    /* external callback */
    public void OnPlayerStunned() { if (isOwned) netAnim.SetTrigger(StunnedHash); }
}

// Add this as a new script in Unity and attach to the Throw animation state
public class ThrowStateLogger : StateMachineBehaviour
{
    override public void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        Debug.Log("Entered throw animation state on " + animator.gameObject.name);
    }
}