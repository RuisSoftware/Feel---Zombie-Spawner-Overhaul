using System;
using System.Collections.Generic;
using UnityEngine;

// Managerklasse voor virtuele zombies
public static class VirtualZombieManager
{
    // Lijst van alle virtuele zombies die momenteel actief zijn
    private static List<VirtualZombie> virtualZombies = new List<VirtualZombie>();
    // Toewijzing van entityId naar virtuele zombie (voor snelle lookup bij kill events)
    private static Dictionary<int, VirtualZombie> entityToVirtualMap = new Dictionary<int, VirtualZombie>();
    // Unieke ID teller voor virtuele zombies
    private static int nextVirtualId = 1;
    // Pseudo-random generator voor posities
    private static System.Random random = new System.Random();
    // Interval voor positie-logging (in milliseconden worldTime)
    private const ulong LOG_INTERVAL = 10000;
    private static ulong lastLogTime = 0;
    // Aantal virtuele zombies om te simuleren tegelijk
    private const int INITIAL_VIRTUAL_COUNT = 10;

    // Initialiseer virtuele zombies bij start van de wereld
    public static void InitVirtualZombies()
    {
        virtualZombies.Clear();
        entityToVirtualMap.Clear();
        nextVirtualId = 1;
        // Bepaal wereldgrenzen
        GameManager.Instance.World.GetWorldExtent(out Vector3i min, out Vector3i max);
        Log.Out($"[ZombieSpawnerMod] InitVirtualZombies: Wereldgrenzen min={min}, max={max}");
        // Creëer INITIAL_VIRTUAL_COUNT virtuele zombies op willekeurige randen
        for (int i = 0; i < INITIAL_VIRTUAL_COUNT; i++)
        {
            VirtualZombie vz = CreateRandomZombieAtEdge(min, max);
            virtualZombies.Add(vz);
            Log.Out($"[ZombieSpawnerMod]  -> Virtuele zombie #{vz.id} aangemaakt aan rand, start={vz.startPos.ToString()} -> doel={vz.destPos.ToString()}");
        }
        lastLogTime = GameManager.Instance.World.worldTime;
    }

    // Creeër één virtuele zombie met een random start aan een wereldrand en een doel aan de overkant
    private static VirtualZombie CreateRandomZombieAtEdge(Vector3i min, Vector3i max)
    {
        // Kies willekeurig een van de 4 randen: 0=links, 1=rechts, 2=onder, 3=boven
        int edge = random.Next(4);
        int destEdge;
        Vector3 start = Vector3.zero;
        Vector3 dest = Vector3.zero;
        // Bepaal start- en doelcoördinaten (X,Z) op basis van gekozen rand
        if (edge == 0)  // linker rand (min X)
        {
            start.x = min.x;
            start.z = random.Next(min.z, max.z);
            dest.x = max.x;
            dest.z = random.Next(min.z, max.z);
        }
        else if (edge == 1)  // rechter rand (max X)
        {
            start.x = max.x;
            start.z = random.Next(min.z, max.z);
            dest.x = min.x;
            dest.z = random.Next(min.z, max.z);
        }
        else if (edge == 2)  // onderste rand (min Z)
        {
            start.z = min.z;
            start.x = random.Next(min.x, max.x);
            dest.z = max.z;
            dest.x = random.Next(min.x, max.x);
        }
        else  // bovenste rand (max Z)
        {
            start.z = max.z;
            start.x = random.Next(min.x, max.x);
            dest.z = min.z;
            dest.x = random.Next(min.x, max.x);
        }
        // Bereken hoogte (Y) van terrein op start- en doelpositie
        float startY = GameManager.Instance.World.ChunkCache.ChunkProvider.GetTerrainGenerator().GetTerrainHeightAt((int)start.x, (int)start.z);
        float destY = GameManager.Instance.World.ChunkCache.ChunkProvider.GetTerrainGenerator().GetTerrainHeightAt((int)dest.x, (int)dest.z);
        start.y = startY;
        dest.y = destY;
        // Maak VirtualZombie object aan
        VirtualZombie zombie = new VirtualZombie(nextVirtualId++, start, dest);
        return zombie;
    }

    // Update wordt elke game tick aangeroepen vanuit de mod event (GameUpdate)
    public static void UpdateVirtualZombies()
    {
        if (virtualZombies.Count == 0) return;  // niets te doen als er geen virtuele zombies zijn

        // Skip update als er geen spelers actief zijn
        if (GameManager.Instance.World.Players.list.Count == 0)
        {
            return;
        }


        World world = GameManager.Instance.World;
        ulong currentTime = world.worldTime;
        // Itereer over een kopie van de lijst om veilig te kunnen verwijderen tijdens iteratie
        var zombiesSnapshot = new List<VirtualZombie>(virtualZombies);
        foreach (VirtualZombie vz in zombiesSnapshot)
        {
            if (vz.isSpawned)
            {
                // Als zombie fysiek gespawned is, synchroniseer de virtuele positie met de werkelijke entity positie
                Entity entity = world.GetEntity(vz.spawnedEntityId);
                if (entity != null)
                {
                    vz.currentPos = entity.GetPosition();
                }
                // Als de speler niet meer in de buurt is, despawn de entity en hervat virtueel lopen
                if (!world.IsChunkAreaLoaded(vz.currentPos))
                {
                    Log.Out($"[ZombieSpawnerMod] Despawn: Zombie #{vz.id} verdwijnt weer virtueel (pos={vz.currentPos})");
                    DespawnZombieEntity(vz);
                }
            }
            else
            {
                // Zombie is virtueel: verplaats een stap richting het doel
                MoveVirtualZombie(vz);
                // Controleer of de zombie nu in de buurt van een speler komt (chunks geladen rondom)
                if (world.IsChunkAreaLoaded(vz.currentPos))
                {
                    // Spawn de zombie nu echt in de wereld
                    SpawnZombieEntity(vz);
                }
            }
            // Controleer of het doel bereikt is
            if (vz.ReachedDestination())
            {
                Log.Out($"[ZombieSpawnerMod] Finish: Zombie #{vz.id} heeft de overkant bereikt bij {vz.currentPos}, verwijderen...");
                // Als er nog een fysieke entity bestaat (bv. zombie bereikt rand terwijl speler nabij is), verwijder die ook
                if (vz.isSpawned)
                {
                    DespawnZombieEntity(vz);
                }
                // Verwijder de virtuele zombie uit de lijst
                virtualZombies.Remove(vz);
            }
        }
        // Indien er virtuele zombies zijn verwijderd, spawn nieuwe om populatie constant te houden
        // (Zo blijft het aantal rondzwervende zombies ongeveer gelijk)
        while (virtualZombies.Count < INITIAL_VIRTUAL_COUNT)
        {
            GameManager.Instance.World.GetWorldExtent(out Vector3i min, out Vector3i max);
            VirtualZombie newZombie = CreateRandomZombieAtEdge(min, max);
            virtualZombies.Add(newZombie);
            Log.Out($"[ZombieSpawnerMod]  -> Nieuwe virtuele zombie #{newZombie.id} toegevoegd (vervanging), start={newZombie.startPos} -> doel={newZombie.destPos}");
        }
        // Log elke LOG_INTERVAL ms een statusoverzicht van posities
        if (currentTime - lastLogTime >= LOG_INTERVAL)
        {
            lastLogTime = currentTime;
            foreach (VirtualZombie vz in virtualZombies)
            {
                string state = vz.isSpawned ? "GESPAWNED" : "virtueel";
                Log.Out($"[ZombieSpawnerMod] Status: Zombie #{vz.id} ({state}) op {vz.currentPos.ToCultureInvariantString()} richting {vz.destPos}");
            }
        }
    }

    // Verplaatst een virtuele zombie een stapje verder richting het doel (houdt rekening met terrein en obstakels)
    private static void MoveVirtualZombie(VirtualZombie vz)
    {
        Vector3 pos = vz.currentPos;
        Vector3 target = vz.destPos;

        // Bereken directionele vector naar het doel
        Vector3 direction = new Vector3(target.x - pos.x, 0, target.z - pos.z);
        float distance = direction.magnitude;

        if (distance < vz.moveStep)
        {
            vz.currentPos = target;
            vz.currentPos.y = GameManager.Instance.World.ChunkCache.ChunkProvider.GetTerrainGenerator().GetTerrainHeightAt((int)target.x, (int)target.z);
            return;
        }

        direction.Normalize();
        Vector3 newPos = pos + direction * vz.moveStep;

        // Alleen de hoogte bepalen op het nieuwe punt, verder geen obstakel-check
        newPos.y = GameManager.Instance.World.ChunkCache.ChunkProvider.GetTerrainGenerator().GetTerrainHeightAt((int)newPos.x, (int)newPos.z);

        vz.currentPos = newPos;
    }

    private static float GetTerrainHeight(float x, float z)
    {
        return GameManager.Instance.World.ChunkCache.ChunkProvider
            .GetTerrainGenerator().GetTerrainHeightAt((int)x, (int)z);
    }

    // Spawnt een virtuele zombie daadwerkelijk in de wereld als Entity
    private static void SpawnZombieEntity(VirtualZombie vz)
    {
        // Kies een willekeurige zombie-entity class (we nemen een generieke zombie)
        int entityClassId = EntityClass.FromString("zombieBoe");
        Entity zombieEntity = EntityFactory.CreateEntity(entityClassId, vz.currentPos, Vector3.zero);
        if (zombieEntity != null)
        {
            // Registreer in world
            GameManager.Instance.World.SpawnEntityInWorld(zombieEntity);
            vz.spawnedEntityId = zombieEntity.entityId;
            vz.isSpawned = true;
            // Map entityId naar virtuele zombie voor snelle lookup
            entityToVirtualMap[zombieEntity.entityId] = vz;
            Log.Out($"[ZombieSpawnerMod] Spawn: Virtuele zombie #{vz.id} gespawned als entity (entityId={zombieEntity.entityId}) op {vz.currentPos}");
        }
    }

    // Despawnt een zombie-entity terug naar virtueel (verwijdert de fysieke entity uit de wereld)
    private static void DespawnZombieEntity(VirtualZombie vz)
    {
        if (!vz.isSpawned) return;
        // Verwijder de entity uit de wereld met reden Unloaded (alsof de chunk ontlaadt)
        GameManager.Instance.World.RemoveEntity(vz.spawnedEntityId, EnumRemoveEntityReason.Unloaded);
        // Uit administratie halen
        entityToVirtualMap.Remove(vz.spawnedEntityId);
        vz.spawnedEntityId = -1;
        vz.isSpawned = false;
        // (De virtuele zombie blijft bestaan en zal verder lopen vanuit de huidige positie)
    }

    // Markeer dat een entity (zombie) gedood is, voor verwijdering uit virtuele lijst
    public static void MarkZombieAsDead(int entityId)
    {
        if (entityToVirtualMap.TryGetValue(entityId, out VirtualZombie vz))
        {
            // Verwijder fysieke entity (indien nog niet gedaan)
            entityToVirtualMap.Remove(entityId);
            virtualZombies.Remove(vz);
            Log.Out($"[ZombieSpawnerMod] VirtualZombieManager: Zombie #{vz.id} verwijderd uit virtuele lijst (gedood)");
        }
    }

    // Controleer of een gegeven entityId correspondeert met een door deze mod gespawnede virtuele zombie
    public static bool IsVirtualZombieEntity(int entityId)
    {
        return entityToVirtualMap.ContainsKey(entityId);
    }

    // Verkrijg het virtuele zombie ID horende bij een entityId (voor logging doeleinden)
    public static int GetVirtualId(int entityId)
    {
        if (entityToVirtualMap.TryGetValue(entityId, out VirtualZombie vz))
            return vz.id;
        return -1;
    }

    // Maak alle gegevens leeg (bij wereld afsluiten)
    public static void ClearAll()
    {
        virtualZombies.Clear();
        entityToVirtualMap.Clear();
        nextVirtualId = 1;
    }

    public static VirtualZombie CreateZombieForExternalCommand(Vector3i min, Vector3i max)
    {
        VirtualZombie vz = CreateRandomZombieAtEdge(min, max);
        virtualZombies.Add(vz);
        Log.Out($"[ZombieSpawnerMod] [Cmd] Zombie #{vz.id} toegevoegd via commando, start={vz.startPos} -> doel={vz.destPos}");
        return vz;
    }
}
