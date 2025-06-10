# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity 6 multiplayer bomb-passing elimination game built with Mirror Networking. Players throw bombs between each other, building up knockback percentage until someone gets eliminated by falling off the map. Last player standing wins.

## Architecture

### Networking Pattern
- **Mirror Framework**: Uses NetworkRoomManager for lobby system with three-scene flow (MainMenu → Room → Game)
- **Server Authority**: GameManager runs server-side game loop, validates all actions, maintains authoritative state
- **Reconnection Support**: Players can reconnect during active games, preserving their game state and lives
- **Custom Authentication**: Room-based auth system links player names across scenes

### Core Game Systems

**Game Flow (GameManager.cs)**
- Singleton pattern with server-authoritative round loop
- Handles countdown, bomb spawning, win conditions, pause/resume
- Manages player registration and life tracking
- Uses SyncVars for critical state (GameActive, IsPaused, Pauser)

**Player Lifecycle**
- Players spawn via MyRoomManager.OnRoomServerSceneLoadedForPlayer()  
- Assigned unique PlayerNumber (1-4, cycling) and spawn positions
- PlayerLifeManager tracks lives (3), knockback percentage (0-350%), disconnection state
- PlayerInfo component carries player name across scenes

**Bomb Mechanics**
- Hot potato gameplay: bomb spawns on random player, explodes after timer
- Two throw types: normal (blue arc) vs lob (yellow arc) with trajectory prediction
- Auto-return to thrower after 2 seconds if uncaught
- Explosion creates damage sectors with percentage-based knockback

### Key Components

**PlayerMovement.cs**: Input-driven movement that respects pause state
**PlayerBombHandler.cs**: Bomb throwing with trajectory prediction and hand switching  
**Bomb.cs**: Core bomb logic with timer, assignment, throwing, explosion
**KnockbackCalc.cs**: Sector-based damage calculation around explosion center
**SpawnManager.cs**: Handles spawn point assignment and fall-off detection

### Scene Structure
- **MainMenu**: Authentication, room joining UI
- **Room**: Lobby with player list, ready-up system
- **Game**: Main gameplay with GameManager, SpawnManager, players

### Input System
- Uses Unity's new Input System with PlayerInputActions.inputactions
- Supports both keyboard (WASD/arrows, space, F, ESC) and gamepad
- Movement, throwing, hand switching, pause controls

### Debug Patterns
- Extensive Debug.Log statements throughout for networking troubleshooting
- Component references logged with `this` parameter for context
- Player state changes, bomb assignments, and network events tracked
- Visual trajectory prediction and knockback sector debugging available

## Development Notes

### Mirror Networking Conventions
- Use `[Server]` for server-only methods, `[ServerCallback]` for conditional execution
- `[Command]` for client-to-server, `[ClientRpc]` for server-to-clients, `[TargetRpc]` for server-to-specific-client
- SyncVars with hooks for state that needs client notification
- NetworkServer.Spawn() for dynamic objects like bombs

### Player State Management
- PlayerLifeManager.IsDisconnected distinguishes between eliminated and temporarily disconnected
- GameManager.playerObjects dictionary enables reconnection by player name
- Authority assignment/removal during disconnect/reconnect handled in MyRoomManager

### Common Patterns
- Null checks before component access due to network object lifecycle
- ToArray() when iterating collections that may be modified during iteration
- Instance pattern for singletons (GameManager, SpawnManager) with proper cleanup
- Component-based architecture with clear separation of concerns

### Audio System
- PersistentAudioManager survives scene transitions for continuous background music
- AudioManager handles per-scene sound effects (explosions, UI sounds, etc.)
- Audio sources configured per component (bomb explosions, UI clicks)