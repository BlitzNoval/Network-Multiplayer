using UnityEngine;
using System.Collections;

public class PlayerLifeManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int maxLives = 3;
    [SerializeField] private float respawnDuration = 2f;

    private int currentLives;
    private PlayerBombHandler bombHandler;
    private PlayerMovement movement;
    private Collider playerCollider;
    private Rigidbody rb;

    public int CurrentLives => currentLives;
    public bool IsDead { get; private set; }

    void Awake()
    {
        bombHandler = GetComponent<PlayerBombHandler>();
        movement = GetComponent<PlayerMovement>();
        playerCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();
    }

    void Start() => InitializeLives();

    public void InitializeLives()
    {
        currentLives = maxLives;
        IsDead = false;
    }

    public void HandleDeath()
    {
        if (IsDead) return;

        currentLives--;
        
        if (currentLives <= 0)
        {
            HandleFinalDeath();
        }
        else
        {
            StartCoroutine(RespawnSequence());
        }
    }

    private IEnumerator RespawnSequence()
    {
        IsDead = true;
        ToggleComponents(false);
        
        yield return new WaitForSeconds(respawnDuration);
        
        ToggleComponents(true);
        IsDead = false;
        HandleBombOnRespawn();
    }

    private void HandleFinalDeath()
    {
        HandleBombOnDeath();
        Destroy(gameObject);
    }

    private void HandleBombOnDeath()
    {
        if (bombHandler == null || bombHandler.CurrentBomb == null) return;
        
        Bomb bomb = bombHandler.CurrentBomb;
        bomb.DetachFromPlayer();
        bomb.TriggerImmediateExplosion();
    }

    private void HandleBombOnRespawn()
    {
        if (bombHandler == null || bombHandler.CurrentBomb == null) return;
        
        Bomb bomb = bombHandler.CurrentBomb;
        bomb.DetachFromPlayer();
        GameManager.Instance?.ReassignOrphanedBomb(bomb);
    }

    private void ToggleComponents(bool state)
    {
        movement.enabled = state;
        playerCollider.enabled = state;
        rb.isKinematic = !state;
        rb.linearVelocity = Vector3.zero;
    }
}