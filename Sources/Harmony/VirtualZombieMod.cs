using System;
using UnityEngine;
using zombiespawner.Sources.Scripts;

// Hoofdklasse van de mod die de mod initialiseert via het IModApi interface
public class VirtualZombieMod : IModApi
{
    // Deze methode wordt door het spel aangeroepen tijdens het laden van mods
    public void InitMod(Mod modInstance)
    {
        Log.Out("[ZombieSpawnerMod] InitMod: VirtualZombieMod wordt geladen");  // Log het laden van de mod
        // Abonneer op belangrijke game-events via ModEvents
        ModEvents.GameStartDone.RegisterHandler(eventGameStartDone);
        ModEvents.GameUpdate.RegisterHandler(eventGameUpdate);
        ModEvents.EntityKilled.RegisterHandler(eventEntityKilled);
        ModEvents.WorldShuttingDown.RegisterHandler(eventWorldShuttingDown);
        ModEvents.GameStartDone.RegisterHandler(delegate {
            Log.Out("[ZombieSpawnerMod] Registreer server commando 'spawnzombies'");
            SdtdConsole.Instance.m_Commands.Add(new CmdSpawnZombies());
        });
    }

    // Event-handler: Wordt aangeroepen zodra de wereld volledig is geladen en het spel start
    private void eventGameStartDone()
    {
        Log.Out("[ZombieSpawnerMod] GameStartDone: Initialiseer virtuele zombies");
        VirtualZombieManager.InitVirtualZombies();  // initialiseert virtuele zombies aan de maprand
    }

    // Event-handler: Aangeroepen bij elke game-update tick (continu tijdens het spel)
    private void eventGameUpdate()
    {
        VirtualZombieManager.UpdateVirtualZombies();  // werk alle virtuele zombies bij (bewegen/spawnen/despawnen)
    }

    // Event-handler: Aangeroepen wanneer een entity sterft in de wereld
    private void eventEntityKilled(Entity entity, Entity killer)
    {
        // Controleer of de gestorven entity een virtuele zombie van deze mod is
        if (entity != null && VirtualZombieManager.IsVirtualZombieEntity(entity.entityId))
        {
            Log.Out($"[ZombieSpawnerMod] EntityKilled: Virtuele zombie #{VirtualZombieManager.GetVirtualId(entity.entityId)} gedood (entityId={entity.entityId})");
            VirtualZombieManager.MarkZombieAsDead(entity.entityId);
        }
    }

    // Event-handler: Aangeroepen wanneer de wereld gaat sluiten (bij uitloggen of nieuw spel laden)
    private void eventWorldShuttingDown()
    {
        Log.Out("[ZombieSpawnerMod] WorldShuttingDown: Opschonen virtuele zombies");
        VirtualZombieManager.ClearAll();  // maak alle gegevens van virtuele zombies leeg
    }
}
