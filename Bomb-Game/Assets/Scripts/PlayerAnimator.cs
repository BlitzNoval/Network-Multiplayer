using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(Animator), typeof(NetworkAnimator))]
[RequireComponent(typeof(PlayerBombHandler))]
public class PlayerAnimator : NetworkBehaviour
{
    static readonly int ActiveHandHash = Animator.StringToHash("activeHand");
    static readonly int IsMovingHash   = Animator.StringToHash("isMoving");
    static readonly int ThrowHash      = Animator.StringToHash("Throw");
    static readonly int StunnedHash    = Animator.StringToHash("Stunned");
    static readonly int LandingHash    = Animator.StringToHash("Landing");
    static readonly int DirectionHash  = Animator.StringToHash("Direction");
    static readonly int EmoteHash      = Animator.StringToHash("Emote");

    [SerializeField] float movementThreshold = .1f;
    [SerializeField] float handFreezeTime    = 1.0f;

    Animator          anim;
    NetworkAnimator   netAnim;
    PlayerBombHandler bombHandler;
    PlayerMovement    movement;
    Rigidbody         rb;
    PlayerLifeManager playerLifeManager;

    [SyncVar(hook = nameof(OnEmoteChanged))] int emoteParam;
    InputAction emoteAct;

    Vector3 cachedVelocity;
    float   handFreezeTimer;

    void Awake()
    {
        anim = GetComponent<Animator>();
        netAnim = GetComponent<NetworkAnimator>();
        bombHandler = GetComponent<PlayerBombHandler>();
        movement = GetComponent<PlayerMovement>();
        rb = GetComponent<Rigidbody>();
        playerLifeManager = GetComponent<PlayerLifeManager>();

        if (!anim || !netAnim || !bombHandler || !rb || !playerLifeManager)
        {
            Debug.LogError("Missing required component(s) on PlayerAnimator!", this);
            enabled = false;
            return;
        }

        if (netAnim.animator == null) netAnim.animator = anim;

        var pi = GetComponent<PlayerInput>();
        emoteAct = pi?.actions.FindAction("Emote"); // 0-3 scale
    }

    void FixedUpdate() => cachedVelocity = rb.linearVelocity;

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

    void Update()
    {
        if (!isOwned) return;

        UpdateActiveHand();
        UpdateMovementState();
        HandleEmoteInput();
    }

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
        Vector3 hv = new(cachedVelocity.x, 0f, cachedVelocity.z);
        bool moving = hv.magnitude > movementThreshold;
        anim.SetBool(IsMovingHash, moving);

        if (!moving) return;

        Vector3 local = transform.InverseTransformDirection(hv.normalized);
        float a   = Mathf.Atan2(local.x, local.z);
        int   sec = (Mathf.RoundToInt(a / (Mathf.PI * .5f)) & 3);
        float dir = sec / 3f;
        anim.SetFloat(DirectionHash, dir, 0.15f, Time.deltaTime);
    }

    void HandleEmoteInput()
    {
        if (playerLifeManager != null && playerLifeManager.isInKnockback) return;

        int v = (int)emoteAct.ReadValue<float>();
        if (v == emoteParam) return;

        ApplyEmote(v);
        CmdSetEmote(v);
    }
    [Command] void CmdSetEmote(int v) => emoteParam = v;
    void OnEmoteChanged(int _, int v)  => ApplyEmote(v);

    void ApplyEmote(int v)
    {
        anim.SetInteger(EmoteHash, v);
        movement?.SetEmoteState(v != 0);
    }

    public System.Action OnThrowAnimationComplete;
    
    public void PlayThrowLocal()
    {
        if (!isOwned) return;

        int hand = bombHandler.CurrentBomb.IsOnRight ? 2 : 1;
        
        anim.SetInteger(ActiveHandHash, hand);

        AnimatorStateInfo baseLayerState = anim.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo throwLayerState = anim.GetCurrentAnimatorStateInfo(1);
        Debug.Log($"PRE-THROW: Base layer: {baseLayerState.shortNameHash} | Throw layer: {throwLayerState.shortNameHash} | activeHand: {anim.GetInteger(ActiveHandHash)}", this);
        
        StartCoroutine(TriggerThrowWithDelay(hand));
        StartCoroutine(MonitorThrowAnimationCompletion());

        handFreezeTimer = handFreezeTime;
        
        Debug.Log($"PlayThrowLocal: hand={hand}, bomb on {(bombHandler.CurrentBomb.IsOnRight ? "right" : "left")}", this);
    }

    public void OnPlayerStunned()
    {
        if (isOwned)
        {
            Debug.Log("Setting Stunned trigger", this);
            netAnim.SetTrigger(StunnedHash);
        }
    }

    public void OnPlayerLanded()
    {
        if (isOwned)
        {
            Debug.Log("Setting Landing trigger", this);
            anim.SetTrigger(LandingHash);
            netAnim.SetTrigger(LandingHash);
            SetStunned(false);
        }
    }

    public void OnBombThrown()
    {
        if (!isOwned) return;
        
        handFreezeTimer = Mathf.Max(handFreezeTimer, 0.5f);
        Debug.Log($"OnBombThrown: extending freeze timer to {handFreezeTimer:F2}", this);
    }
    
    IEnumerator TriggerThrowWithDelay(int hand)
    {
        yield return null;
        
        anim.SetTrigger(ThrowHash);
        netAnim.SetTrigger(ThrowHash);
        
        int currentActiveHand = anim.GetInteger(ActiveHandHash);
        Debug.Log($"POST-THROW: Triggered ThrowHash, hand={hand} | activeHand: {currentActiveHand}", this);
        
        StartCoroutine(CheckAnimatorStateDelayed());
    }

    IEnumerator CheckAnimatorStateDelayed()
    {
        yield return new WaitForSeconds(0.1f);
        
        AnimatorStateInfo baseLayerState = anim.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo throwLayerState = anim.GetCurrentAnimatorStateInfo(1);
        
        Debug.Log($"DELAYED CHECK: Base layer: {baseLayerState.shortNameHash} (normalizedTime: {baseLayerState.normalizedTime:F2}) | Throw layer: {throwLayerState.shortNameHash} (normalizedTime: {throwLayerState.normalizedTime:F2})", this);
        
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
        }
        else
        {
            Debug.LogWarning($"PROBLEM: Expected throw animation, but got hash {throwLayerState.shortNameHash} instead", this);
        }
    }
    
    IEnumerator MonitorThrowAnimationCompletion()
    {
        yield return new WaitForSeconds(0.1f);
        
        int leftThrowHash = Animator.StringToHash("rig_001_Final_Throw_Left");
        int rightThrowHash = Animator.StringToHash("rig_001_Final_Throw_Right");
        
        bool animationStarted = false;
        
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
        
        bool animationCompleted = false;
        while (!animationCompleted)
        {
            AnimatorStateInfo throwLayerState = anim.GetCurrentAnimatorStateInfo(1);
            
            if (throwLayerState.shortNameHash == leftThrowHash || throwLayerState.shortNameHash == rightThrowHash)
            {
                if (throwLayerState.normalizedTime >= 0.55f)
                {
                    animationCompleted = true;
                    Debug.Log($"Throw animation completed at normalizedTime: {throwLayerState.normalizedTime:F2}", this);
                    OnThrowAnimationComplete?.Invoke();
                }
            }
            else
            {
                animationCompleted = true;
                Debug.Log("Throw animation completed (transitioned out)", this);
                OnThrowAnimationComplete?.Invoke();
            }
            
            yield return null;
        }
    }
    
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
        anim.Play("rig_001_Final_Throw_Left", 1);
        Debug.Log("FORCE: Left throw animation forced", this);
    }
    
    [ContextMenu("Force Play Right Throw")]
    public void ForceRightThrow()
    {
        if (!isOwned) return;
        anim.Play("rig_001_Final_Throw_Right", 1);
        Debug.Log("FORCE: Right throw animation forced", this);
    }
    
    [ContextMenu("Debug Layer Info")]
    public void DebugLayerInfo()
    {
        Debug.Log($"Throw layer weight: {anim.GetLayerWeight(1)}", this);
        Debug.Log($"Throw layer active: {anim.IsInTransition(1)}", this);
        
        AnimatorStateInfo throwLayerState = anim.GetCurrentAnimatorStateInfo(1);
        Debug.Log($"Current throw state hash: {throwLayerState.shortNameHash}, normalized time: {throwLayerState.normalizedTime:F2}", this);
    }

    public void SetStunned(bool stunned)
    {
        anim.SetBool("IsStunned", stunned);
    }
}