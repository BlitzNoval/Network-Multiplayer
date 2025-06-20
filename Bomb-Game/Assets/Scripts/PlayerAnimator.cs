// PlayerAnimator.cs – Unity 6 / Mirror 2025 compatible  ✅
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

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
    [SerializeField] float handFreezeTime        = 1.0f;

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
        if (handFreezeTimer > 0f) 
        { 
            handFreezeTimer -= Time.deltaTime; 
            Debug.Log($"UpdateActiveHand: frozen, timer={handFreezeTimer:F2}", this);
            return; 
        }

        int hand = 0;
        if (bombHandler.CurrentBomb && bombHandler.CurrentBomb.Holder == gameObject)
            hand = bombHandler.CurrentBomb.IsOnRight ? 2 : 1;

        int currentHand = anim.GetInteger(ActiveHandHash);
        if (currentHand != hand)
        {
            Debug.Log($"UpdateActiveHand: changing from {currentHand} to {hand} (bomb: {(bombHandler.CurrentBomb?.IsOnRight == true ? "right" : bombHandler.CurrentBomb?.IsOnRight == false ? "left" : "null")})", this);
            anim.SetInteger(ActiveHandHash, hand);
        }
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
    public System.Action OnThrowAnimationComplete;
    
    public void PlayThrowLocal()
    {
        if (!isOwned) return;

        int hand = bombHandler.CurrentBomb.IsOnRight ? 2 : 1;
        
        // Set activeHand on main animator (NetworkAnimator will sync automatically)
        anim.SetInteger(ActiveHandHash, hand);  // This will be synced by NetworkAnimator

        /*  ▪ Call both Animators:
            • anim.SetTrigger  – so **our** client plays instantly
            • netAnim.SetTrigger – so Mirror replicates to others  */
        
        // Debug current animator state before triggering
        AnimatorStateInfo baseLayerState = anim.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo throwLayerState = anim.GetCurrentAnimatorStateInfo(1); // Throw layer
        Debug.Log($"PRE-THROW: Base layer: {baseLayerState.shortNameHash} | Throw layer: {throwLayerState.shortNameHash} | activeHand: {anim.GetInteger(ActiveHandHash)}", this);
        
        // Use a tiny delay to ensure parameters are processed before trigger
        StartCoroutine(TriggerThrowWithDelay(hand));
        
        // Start monitoring for animation completion
        StartCoroutine(MonitorThrowAnimationCompletion());

        handFreezeTimer = handFreezeTime;
        
        Debug.Log($"PlayThrowLocal: hand={hand}, bomb on {(bombHandler.CurrentBomb.IsOnRight ? "right" : "left")}", this);
    }

    public void OnPlayerStunned()
    {
        if (isOwned) netAnim.SetTrigger(StunnedHash);
    }

    // Called when bomb is actually thrown to ensure proper hand cleanup after animation
    public void OnBombThrown()
    {
        if (!isOwned) return;
        
        // Extend freeze time briefly to ensure animation completes
        handFreezeTimer = Mathf.Max(handFreezeTimer, 0.5f);
        Debug.Log($"OnBombThrown: extending freeze timer to {handFreezeTimer:F2}", this);
    }
    
    IEnumerator TriggerThrowWithDelay(int hand)
    {
        // Wait one frame to ensure parameters are set
        yield return null;
        
        anim.SetTrigger(ThrowHash);
        netAnim.SetTrigger(ThrowHash);          // required for sync
        
        // Verify the parameters are set correctly
        int currentActiveHand = anim.GetInteger(ActiveHandHash);
        Debug.Log($"POST-THROW: Triggered ThrowHash, hand={hand} | activeHand: {currentActiveHand}", this);
        
        // Check animator state after a few frames
        StartCoroutine(CheckAnimatorStateDelayed());
    }

    IEnumerator CheckAnimatorStateDelayed()
    {
        yield return new WaitForSeconds(0.1f);
        
        AnimatorStateInfo baseLayerState = anim.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo throwLayerState = anim.GetCurrentAnimatorStateInfo(1);
        
        Debug.Log($"DELAYED CHECK: Base layer: {baseLayerState.shortNameHash} (normalizedTime: {baseLayerState.normalizedTime:F2}) | Throw layer: {throwLayerState.shortNameHash} (normalizedTime: {throwLayerState.normalizedTime:F2})", this);
        
        // Check for specific throw animation hashes
        int leftThrowHash = Animator.StringToHash("rig_001_Final_Throw_Left");
        int rightThrowHash = Animator.StringToHash("rig_001_Final_Throw_Right");
        int newStateHash = Animator.StringToHash("New State");
        
        if (throwLayerState.shortNameHash == leftThrowHash)
        {
            Debug.Log("SUCCESS: Left throw animation is playing!", this);
        }
        else if (throwLayerState.shortNameHash == rightThrowHash)
        {
            Debug.Log("SUCCESS: Right throw animation is playing!", this);
        }
        else if (throwLayerState.shortNameHash == newStateHash)
        {
            int currentActiveHand = anim.GetInteger(ActiveHandHash);
            Debug.LogWarning($"ISSUE: Still in 'New State' - transition didn't occur! activeHand={currentActiveHand}, expected: 1 or 2", this);
            
            // Check if we can manually verify the conditions
            bool hasLeftCondition = (currentActiveHand == 1);
            bool hasRightCondition = (currentActiveHand == 2);
            Debug.LogWarning($"Transition conditions - Left hand ready: {hasLeftCondition}, Right hand ready: {hasRightCondition}", this);
            
            // Check if there are any transition issues
            AnimatorTransitionInfo transitionInfo = anim.GetAnimatorTransitionInfo(1);
            if (transitionInfo.nameHash != 0)
            {
                Debug.LogWarning($"Transition in progress: {transitionInfo.nameHash}, normalizedTime: {transitionInfo.normalizedTime:F2}", this);
            }
            
            // Check all current conditions
            Debug.LogWarning($"Current parameters: activeHand={anim.GetInteger(ActiveHandHash)}, isMoving={anim.GetBool("isMoving")}", this);
        }
        else
        {
            Debug.LogWarning($"PROBLEM: Expected throw animation, but got hash {throwLayerState.shortNameHash} instead", this);
        }
    }
    
    IEnumerator MonitorThrowAnimationCompletion()
    {
        // Wait a moment for the animation to start
        yield return new WaitForSeconds(0.1f);
        
        int leftThrowHash = Animator.StringToHash("rig_001_Final_Throw_Left");
        int rightThrowHash = Animator.StringToHash("rig_001_Final_Throw_Right");
        
        bool animationStarted = false;
        
        // Wait for the throw animation to actually start playing
        while (!animationStarted)
        {
            AnimatorStateInfo throwLayerState = anim.GetCurrentAnimatorStateInfo(1);
            if (throwLayerState.shortNameHash == leftThrowHash || throwLayerState.shortNameHash == rightThrowHash)
            {
                animationStarted = true;
                Debug.Log($"Throw animation started: {(throwLayerState.shortNameHash == leftThrowHash ? "Left" : "Right")}, normalizedTime: {throwLayerState.normalizedTime:F2}", this);
            }
            yield return null;
        }
        
        // Monitor animation progress until completion
        bool animationCompleted = false;
        while (!animationCompleted)
        {
            AnimatorStateInfo throwLayerState = anim.GetCurrentAnimatorStateInfo(1);
            
            // Check if we're still in a throw animation
            if (throwLayerState.shortNameHash == leftThrowHash || throwLayerState.shortNameHash == rightThrowHash)
            {
                // Check if animation reaches the throw climax (around 50-60% through when the throw motion peaks)
                if (throwLayerState.normalizedTime >= 0.55f)
                {
                    animationCompleted = true;
                    Debug.Log($"Throw animation completed at normalizedTime: {throwLayerState.normalizedTime:F2}", this);
                    
                    // Trigger the callback to actually throw the bomb
                    OnThrowAnimationComplete?.Invoke();
                }
            }
            else
            {
                // Animation has transitioned out, consider it complete
                animationCompleted = true;
                Debug.Log("Throw animation completed (transitioned out)", this);
                OnThrowAnimationComplete?.Invoke();
            }
            
            yield return null;
        }
    }
    
    // Debug method to manually test throw animations
    [ContextMenu("Test Left Throw")]
    public void TestLeftThrow()
    {
        if (!isOwned) return;
        anim.SetInteger(ActiveHandHash, 1);
        anim.SetTrigger(ThrowHash);
        Debug.Log("Manual Test: Left throw triggered", this);
        StartCoroutine(CheckAnimatorStateDelayed());
    }
    
    [ContextMenu("Test Right Throw")]  
    public void TestRightThrow()
    {
        if (!isOwned) return;
        anim.SetInteger(ActiveHandHash, 2);
        anim.SetTrigger(ThrowHash);
        Debug.Log("Manual Test: Right throw triggered", this);
        StartCoroutine(CheckAnimatorStateDelayed());
    }
    
    [ContextMenu("Force Play Left Throw")]
    public void ForceLeftThrow()
    {
        if (!isOwned) return;
        anim.Play("rig_001_Final_Throw_Left", 1); // Force play on layer 1 (Throw layer)
        Debug.Log("FORCE: Left throw animation forced", this);
    }
    
    [ContextMenu("Force Play Right Throw")]
    public void ForceRightThrow()
    {
        if (!isOwned) return;
        anim.Play("rig_001_Final_Throw_Right", 1); // Force play on layer 1 (Throw layer)
        Debug.Log("FORCE: Right throw animation forced", this);
    }
    
    [ContextMenu("Debug Layer Info")]
    public void DebugLayerInfo()
    {
        Debug.Log($"Throw layer weight: {anim.GetLayerWeight(1)}", this);
        Debug.Log($"Throw layer active: {anim.IsInTransition(1)}", this);
        
        // Check if the animation is actually playing
        AnimatorStateInfo throwLayerState = anim.GetCurrentAnimatorStateInfo(1);
        Debug.Log($"Current throw state hash: {throwLayerState.shortNameHash}, normalized time: {throwLayerState.normalizedTime:F2}", this);
    }
}
