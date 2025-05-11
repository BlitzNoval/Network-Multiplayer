// DevConsole.cs
using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro;

public class DevConsole : MonoBehaviour
{
    [SerializeField] GameObject panel;

    // Global Player Settings
    [SerializeField] TMP_InputField maxLivesInput;
    [SerializeField] TMP_InputField respawnDelayInput;
    [SerializeField] TMP_InputField fallThresholdInput;
    [SerializeField] TMP_InputField absoluteFallLimitInput;
    [SerializeField] TMP_InputField speedInput;
    [SerializeField] TMP_InputField accelerationInput;
    [SerializeField] TMP_InputField decelerationInput;
    [SerializeField] TMP_InputField rotationSpeedInput;

    // Global Bomb Settings
    [SerializeField] TMP_InputField initialTimerInput;
    [SerializeField] TMP_InputField returnPauseDurationInput;
    [SerializeField] TMP_InputField normalThrowSpeedInput;
    [SerializeField] TMP_InputField normalThrowUpwardInput;
    [SerializeField] TMP_InputField lobThrowSpeedInput;
    [SerializeField] TMP_InputField lobThrowUpwardInput;
    [SerializeField] TMP_InputField throwCooldownInput;
    [SerializeField] TMP_InputField flightMassMultiplierInput;
    [SerializeField] TMP_InputField maxBouncesInput;
    [SerializeField] TMP_InputField groundExplosionDelayInput;
    [SerializeField] TMP_InputField explosionRadiusInput;
    [SerializeField] TMP_InputField baseKnockForceInput;

    // Player-Specific Settings
    [SerializeField] TMP_Dropdown playerDropdown;
    [SerializeField] TMP_InputField currentLivesInput;
    [SerializeField] TMP_InputField knockbackMultiplierInput;
    [SerializeField] TMP_InputField totalHoldTimeInput;
    [SerializeField] TMP_InputField knockbackHitCountInput;

    void Start()
    {
        panel.SetActive(false);

        // Global listeners
        maxLivesInput.onEndEdit.AddListener(v => UpdateGlobalInt(v, UpdateMaxLives));
        respawnDelayInput.onEndEdit.AddListener(v => UpdateGlobalFloat(v, UpdateRespawnDelay));
        fallThresholdInput.onEndEdit.AddListener(v => UpdateGlobalFloat(v, UpdateFallThreshold));
        absoluteFallLimitInput.onEndEdit.AddListener(v => UpdateGlobalFloat(v, UpdateAbsoluteFallLimit));
        speedInput.onEndEdit.AddListener(v => UpdateGlobalFloat(v, UpdateSpeed));
        accelerationInput.onEndEdit.AddListener(v => UpdateGlobalFloat(v, UpdateAcceleration));
        decelerationInput.onEndEdit.AddListener(v => UpdateGlobalFloat(v, UpdateDeceleration));
        rotationSpeedInput.onEndEdit.AddListener(v => UpdateGlobalFloat(v, UpdateRotationSpeed));

        // Bomb listeners
        initialTimerInput.onEndEdit.AddListener(v => UpdateBombFloat(v, UpdateInitialTimer));
        returnPauseDurationInput.onEndEdit.AddListener(v => UpdateBombFloat(v, UpdateReturnPauseDuration));
        normalThrowSpeedInput.onEndEdit.AddListener(v => UpdateBombFloat(v, UpdateNormalThrowSpeed));
        normalThrowUpwardInput.onEndEdit.AddListener(v => UpdateBombFloat(v, UpdateNormalThrowUpward));
        lobThrowSpeedInput.onEndEdit.AddListener(v => UpdateBombFloat(v, UpdateLobThrowSpeed));
        lobThrowUpwardInput.onEndEdit.AddListener(v => UpdateBombFloat(v, UpdateLobThrowUpward));
        throwCooldownInput.onEndEdit.AddListener(v => UpdateBombFloat(v, UpdateThrowCooldown));
        flightMassMultiplierInput.onEndEdit.AddListener(v => UpdateBombFloat(v, UpdateFlightMassMultiplier));
        maxBouncesInput.onEndEdit.AddListener(v => UpdateBombInt(v, UpdateMaxBounces));
        groundExplosionDelayInput.onEndEdit.AddListener(v => UpdateBombFloat(v, UpdateGroundExplosionDelay));
        explosionRadiusInput.onEndEdit.AddListener(v => UpdateBombFloat(v, UpdateExplosionRadius));
        baseKnockForceInput.onEndEdit.AddListener(v => UpdateBombFloat(v, UpdateBaseKnockForce));

        // Player-specific listeners
        currentLivesInput.onEndEdit.AddListener(v => UpdatePlayerInt(v, UpdateCurrentLives));
        knockbackMultiplierInput.onEndEdit.AddListener(v => UpdatePlayerFloat(v, UpdateKnockbackMultiplier));
        totalHoldTimeInput.onEndEdit.AddListener(v => UpdatePlayerFloat(v, UpdateTotalHoldTime));
        knockbackHitCountInput.onEndEdit.AddListener(v => UpdatePlayerInt(v, UpdateKnockbackHitCount));
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab) && IsHost())
        {
            panel.SetActive(!panel.activeSelf);
            if (panel.activeSelf) UpdatePlayerDropdown();
        }
    }

    void UpdatePlayerDropdown()
    {
        playerDropdown.ClearOptions();
        var players = Object.FindObjectsByType<PlayerLifeManager>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var options = new System.Collections.Generic.List<string>();
        foreach (var p in players)
            options.Add($"Player {p.playerNumber}");
        playerDropdown.AddOptions(options);
    }

    // --- Global Player Updates ---
    void UpdateGlobalInt(string value, System.Action<int> fn)
    {
        if (int.TryParse(value, out int v) && IsHost()) fn(v);
    }

    void UpdateGlobalFloat(string value, System.Action<float> fn)
    {
        if (float.TryParse(value, out float v) && IsHost()) fn(v);
    }

    void UpdateMaxLives(int value)
    {
        var players = Object.FindObjectsByType<PlayerLifeManager>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var p in players)
            p.SetMaxLives(value);
    }

    void UpdateRespawnDelay(float value)
    {
        var players = Object.FindObjectsByType<PlayerLifeManager>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var p in players)
            p.SetRespawnDelay(value);
    }

    void UpdateFallThreshold(float value)
    {
        var players = Object.FindObjectsByType<PlayerLifeManager>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var p in players)
            p.SetFallThreshold(value);
    }

    void UpdateAbsoluteFallLimit(float value)
    {
        var players = Object.FindObjectsByType<PlayerLifeManager>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var p in players)
            p.SetAbsoluteFallLimit(value);
    }

    void UpdateSpeed    (float v) { foreach (var m in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) m.speed        = v; }
    void UpdateAcceleration(float v) { foreach (var m in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) m.acceleration = v; }
    void UpdateDeceleration(float v) { foreach (var m in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) m.deceleration = v; }
    void UpdateRotationSpeed(float v) { foreach (var m in Object.FindObjectsByType<PlayerMovement>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) m.rotationSpeed = v; }

    // --- Global Bomb Updates ---
    void UpdateBombInt(string value, System.Action<int> fn)
    {
        if (int.TryParse(value, out int v) && IsHost()) fn(v);
    }

    void UpdateBombFloat(string value, System.Action<float> fn)
    {
        if (float.TryParse(value, out float v) && IsHost()) fn(v);
    }

    void UpdateInitialTimer(float v)
    {
        var bomb = Object.FindFirstObjectByType<Bomb>();
        if (bomb != null) bomb.SetInitialTimer(v);
    }

    void UpdateReturnPauseDuration(float v)
    {
        var bomb = Object.FindFirstObjectByType<Bomb>();
        if (bomb != null) bomb.SetReturnPauseDuration(v);
    }

    void UpdateNormalThrowSpeed(float v)
    {
        var bomb = Object.FindFirstObjectByType<Bomb>();
        if (bomb != null) bomb.SetNormalThrowSpeed(v);
    }

    void UpdateNormalThrowUpward(float v)
    {
        var bomb = Object.FindFirstObjectByType<Bomb>();
        if (bomb != null) bomb.SetNormalThrowUpward(v);
    }

    void UpdateLobThrowSpeed(float v)
    {
        var bomb = Object.FindFirstObjectByType<Bomb>();
        if (bomb != null) bomb.SetLobThrowSpeed(v);
    }

    void UpdateLobThrowUpward(float v)
    {
        var bomb = Object.FindFirstObjectByType<Bomb>();
        if (bomb != null) bomb.SetLobThrowUpward(v);
    }

    void UpdateThrowCooldown(float v)
    {
        var bomb = Object.FindFirstObjectByType<Bomb>();
        if (bomb != null) bomb.SetThrowCooldown(v);
    }

    void UpdateFlightMassMultiplier(float v)
    {
        var bomb = Object.FindFirstObjectByType<Bomb>();
        if (bomb != null) bomb.SetFlightMassMultiplier(v);
    }

    void UpdateMaxBounces(int v)
    {
        var bomb = Object.FindFirstObjectByType<Bomb>();
        if (bomb != null) bomb.SetMaxBounces(v);
    }

    void UpdateGroundExplosionDelay(float v)
    {
        var bomb = Object.FindFirstObjectByType<Bomb>();
        if (bomb != null) bomb.SetGroundExplosionDelay(v);
    }

    void UpdateExplosionRadius(float v)
    {
        var bomb = Object.FindFirstObjectByType<Bomb>();
        if (bomb != null) bomb.SetExplosionRadius(v);
    }

    void UpdateBaseKnockForce(float v)
    {
        var bomb = Object.FindFirstObjectByType<Bomb>();
        if (bomb != null) bomb.SetBaseKnockForce(v);
    }

    // --- Player-Specific Updates ---
    void UpdatePlayerInt(string value, System.Action<int> fn)
    {
        if (int.TryParse(value, out int v) && IsHost()) fn(v);
    }

    void UpdatePlayerFloat(string value, System.Action<float> fn)
    {
        if (float.TryParse(value, out float v) && IsHost()) fn(v);
    }

    void UpdateCurrentLives(int v)
    {
        var players = Object.FindObjectsByType<PlayerLifeManager>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int i = playerDropdown.value;
        if (i >= 0 && i < players.Length)
            players[i].currentLives = v;
    }

    void UpdateKnockbackMultiplier(float v)
    {
        var players = Object.FindObjectsByType<PlayerLifeManager>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int i = playerDropdown.value;
        if (i >= 0 && i < players.Length)
            players[i].knockbackMultiplier = v;
    }

    void UpdateTotalHoldTime(float v)
    {
        var players = Object.FindObjectsByType<PlayerLifeManager>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int i = playerDropdown.value;
        if (i >= 0 && i < players.Length)
            players[i].totalHoldTime = v;
    }

    void UpdateKnockbackHitCount(int v)
    {
        var players = Object.FindObjectsByType<PlayerLifeManager>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        int i = playerDropdown.value;
        if (i >= 0 && i < players.Length)
            players[i].knockbackHitCount = v;
    }

    bool IsHost() => NetworkManager.singleton.mode == NetworkManagerMode.Host;
}
